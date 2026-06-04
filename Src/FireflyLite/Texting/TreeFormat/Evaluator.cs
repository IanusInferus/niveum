using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Firefly.Texting.TreeFormat.Syntax;

namespace Firefly.Texting.TreeFormat
{
    public interface ISyntaxMarker
    {
        Text Text { get; }
        Optional<TextRange> GetRange(object Obj);
        Optional<FileTextRange> GetFileRange(object Obj);
        T Mark<T>(T Obj, Optional<TextRange> Range);
        T Mark<T>(T Obj, object SyntaxRule);
    }

    public interface ISemanticsNodeMaker
    {
        Text Text { get; }
        Optional<TextRange> GetRange(object Obj);
        Optional<FileTextRange> GetFileRange(object Obj);
        Semantics.Node MakeEmptyNode(Optional<TextRange> Range);
        Semantics.Node MakeLeafNode(string Value, Optional<TextRange> Range);
        Semantics.Node MakeStemNode(string Name, List<Semantics.Node> Children, Optional<TextRange> Range);
        Semantics.Node MakeEmptyNode(object SyntaxRule);
        Semantics.Node MakeLeafNode(string Value, object SyntaxRule);
        Semantics.Node MakeStemNode(string Name, List<Semantics.Node> Children, object SyntaxRule);
    }

    public class TreeFormatEvaluateSetting
    {
        public Func<RawFunctionCall, ISyntaxMarker, List<Semantics.Node>> TokenParameterEvaluator;
        public Func<FunctionCall, ISemanticsNodeMaker, List<Semantics.Node>> FunctionCallEvaluator;
    }

    public class TreeFormatEvaluator : ISyntaxMarker, ISemanticsNodeMaker
    {
        private TreeFormatParseResult pr;
        private Func<RawFunctionCall, ISyntaxMarker, List<Semantics.Node>> TokenParameterEvaluator;
        private Func<FunctionCall, ISemanticsNodeMaker, List<Semantics.Node>> FunctionCallEvaluator;

        private Dictionary<object, FileTextRange> Positions;

        public TreeFormatEvaluator(TreeFormatEvaluateSetting Setting, TreeFormatParseResult pr)
        {
            this.pr = pr;
            this.TokenParameterEvaluator = Setting.TokenParameterEvaluator;
            this.FunctionCallEvaluator = Setting.FunctionCallEvaluator;
            this.Positions = new Dictionary<object, FileTextRange>(pr.Positions.ToDictionary(p => p.Key, p => new FileTextRange { Text = pr.Text, Range = p.Value }));
        }

        public Text Text
        {
            get { return pr.Text; }
        }

        public TreeFormatResult Evaluate()
        {
            var Nodes = EvaluateMultiNodesList(pr.Value.MultiNodesList);
            var F = Mark(new Semantics.Forest { Nodes = Nodes }, pr.Value);
            return new TreeFormatResult { Value = F, Positions = Positions };
        }

        public Optional<TextRange> GetRange(object Obj)
        {
            if (!Positions.ContainsKey(Obj)) return Optional<TextRange>.Empty;
            return Positions[Obj].Range.Value;
        }
        public Optional<FileTextRange> GetFileRange(object Obj)
        {
            if (!Positions.ContainsKey(Obj)) return Optional<FileTextRange>.Empty;
            return new FileTextRange { Text = pr.Text, Range = GetRange(Obj) };
        }
        public T Mark<T>(T Obj, Optional<TextRange> Range)
        {
            if (Range.OnSome)
            {
                Positions.Add(Obj, new FileTextRange { Text = pr.Text, Range = Range });
            }
            return Obj;
        }
        public T Mark<T>(T Obj, object SyntaxRule)
        {
            var Range = GetRange(SyntaxRule);
            if (Range.OnSome)
            {
                Positions.Add(Obj, new FileTextRange { Text = pr.Text, Range = Range });
            }
            return Obj;
        }
        public Semantics.Node MakeEmptyNode(Optional<TextRange> Range)
        {
            var n = Mark(Semantics.Node.CreateEmpty(), Range);
            return n;
        }
        public Semantics.Node MakeLeafNode(string Value, Optional<TextRange> Range)
        {
            var n = Mark(Semantics.Node.CreateLeaf(Value), Range);
            return n;
        }
        public Semantics.Node MakeStemNode(string Name, List<Semantics.Node> Children, Optional<TextRange> Range)
        {
            var s = Mark(new Semantics.Stem { Name = Name, Children = Children }, Range);
            var n = Mark(Semantics.Node.CreateStem(s), Range);
            return n;
        }
        public Semantics.Node MakeEmptyNode(object SyntaxRule)
        {
            var Range = GetRange(SyntaxRule);
            var n = Mark(Semantics.Node.CreateEmpty(), Range);
            return n;
        }
        public Semantics.Node MakeLeafNode(string Value, object SyntaxRule)
        {
            var Range = GetRange(SyntaxRule);
            var n = Mark(Semantics.Node.CreateLeaf(Value), Range);
            return n;
        }
        public Semantics.Node MakeStemNode(string Name, List<Semantics.Node> Children, object SyntaxRule)
        {
            var Range = GetRange(SyntaxRule);
            var s = Mark(new Semantics.Stem { Name = Name, Children = Children }, Range);
            var n = Mark(Semantics.Node.CreateStem(s), Range);
            return n;
        }

        private List<Semantics.Node> EvaluateMultiNodesList(List<MultiNodes> MultiNodesList)
        {
            var l = new List<Semantics.Node>();
            foreach (var mn in MultiNodesList)
            {
                switch (mn._Tag)
                {
                    case MultiNodesTag.Node:
                        {
                            var n = EvaluateNode(mn.Node);
                            if (n.OnSome)
                            {
                                l.Add(n.Value);
                            }
                            break;
                        }
                    case MultiNodesTag.ListNodes:
                        {
                            var ln = mn.ListNodes;
                            var Children = EvaluateMultiNodesList(ln.Children);
                            foreach (var c in Children)
                            {
                                var n = MakeStemNode(ln.ChildHead.Text, new List<Semantics.Node> { c }, c);
                                l.Add(n);
                            }
                            break;
                        }
                    case MultiNodesTag.TableNodes:
                        {
                            var tn = mn.TableNodes;
                            foreach (var c in tn.Children)
                            {
                                if (c.Nodes.Count == 0) continue;
                                if (c.Nodes.Count != tn.ChildFields.Count)
                                {
                                    throw new InvalidEvaluationException("TableLineNodeCountNotMatchHead", new FileTextRange { Text = pr.Text, Range = GetRange(c) }, c);
                                }
                                Func<TableLineNode, SingleLineLiteral, Semantics.Node> MakeField = (TableLineNode, Field) => MakeStemNode(Field.Text, new List<Semantics.Node> { EvaluateTableLineNode(TableLineNode) }, TableLineNode);
                                var Fields = c.Nodes.Zip(tn.ChildFields, MakeField).ToList();
                                var n = MakeStemNode(tn.ChildHead.Text, Fields, c);
                                l.Add(n);
                            }
                            break;
                        }
                    case MultiNodesTag.FunctionNodes:
                        {
                            var fn = mn.FunctionNodes;
                            if (!pr.RawFunctionCalls.ContainsKey(fn)) throw new InvalidEvaluationException("FunctionCallNotFound", new FileTextRange { Text = pr.Text, Range = GetRange(fn) }, fn);
                            var rfc = pr.RawFunctionCalls[fn];
                            if (rfc.ReturnValueMode != FunctionCallReturnValueMode.MultipleNodes) throw new InvalidEvaluationException("FunctionCallReturnValueModeUnexpected", new FileTextRange { Text = pr.Text, Range = GetRange(fn) }, fn);
                            l.AddRange(EvaluateFunction(rfc));
                            break;
                        }
                    default:
                        throw new ArgumentException();
                }
            }
            return l;
        }

        private Optional<Semantics.Node> EvaluateNode(Node Node)
        {
            switch (Node._Tag)
            {
                case NodeTag.SingleLineNodeLine:
                    return EvaluateSingleLineNode(Node.SingleLineNodeLine.SingleLineNode);
                case NodeTag.MultiLineLiteral:
                    return MakeLeafNode(Node.MultiLineLiteral.Content.Text, Node);
                case NodeTag.SingleLineComment:
                    return Optional<Semantics.Node>.Empty;
                case NodeTag.MultiLineComment:
                    return Optional<Semantics.Node>.Empty;
                case NodeTag.MultiLineNode:
                    {
                        var mln = Node.MultiLineNode;
                        var Children = EvaluateMultiNodesList(mln.Children);
                        return MakeStemNode(mln.Head.Text, Children, mln);
                    }
                default:
                    throw new ArgumentException();
            }
        }

        private Semantics.Node EvaluateSingleLineNode(SingleLineNode SingleLineNode)
        {
            switch (SingleLineNode._Tag)
            {
                case SingleLineNodeTag.EmptyNode:
                    return MakeEmptyNode(SingleLineNode.EmptyNode);
                case SingleLineNodeTag.SingleLineFunctionNode:
                    return EvaluateSingleLineFunctionNode(SingleLineNode.SingleLineFunctionNode);
                case SingleLineNodeTag.SingleLineLiteral:
                    {
                        var sll = SingleLineNode.SingleLineLiteral;
                        return MakeLeafNode(sll.Text, sll);
                    }
                case SingleLineNodeTag.ParenthesisNode:
                    return EvaluateSingleLineNode(SingleLineNode.ParenthesisNode.SingleLineNode);
                case SingleLineNodeTag.SingleLineNodeWithParameters:
                    {
                        var slnwp = SingleLineNode.SingleLineNodeWithParameters;
                        var Children = new List<Semantics.Node>();
                        foreach (var c in slnwp.Children)
                        {
                            Children.Add(EvaluateSingleLineNode(c.SingleLineNode));
                        }
                        if (slnwp.LastChild.OnSome)
                        {
                            var lc = slnwp.LastChild.Value;
                            Children.Add(EvaluateSingleLineNode(lc));
                        }
                        return MakeStemNode(slnwp.Head.Text, Children, slnwp);
                    }
                default:
                    throw new ArgumentException();
            }
        }

        private Semantics.Node EvaluateTableLineNode(TableLineNode TableLineNode)
        {
            switch (TableLineNode._Tag)
            {
                case TableLineNodeTag.EmptyNode:
                    return MakeEmptyNode(TableLineNode.EmptyNode);
                case TableLineNodeTag.SingleLineFunctionNode:
                    return EvaluateSingleLineFunctionNode(TableLineNode.SingleLineFunctionNode);
                case TableLineNodeTag.SingleLineLiteral:
                    {
                        var sll = TableLineNode.SingleLineLiteral;
                        return MakeLeafNode(sll.Text, sll);
                    }
                case TableLineNodeTag.ParenthesisNode:
                    return EvaluateSingleLineNode(TableLineNode.ParenthesisNode.SingleLineNode);
                default:
                    throw new ArgumentException();
            }
        }

        private Semantics.Node EvaluateSingleLineFunctionNode(SingleLineFunctionNode SingleLineFunctionNode)
        {
            var fn = SingleLineFunctionNode;
            if (!pr.RawFunctionCalls.ContainsKey(fn)) throw new InvalidEvaluationException("FunctionCallNotFound", new FileTextRange { Text = pr.Text, Range = GetRange(fn) }, fn);
            var rfc = pr.RawFunctionCalls[fn];
            if (rfc.ReturnValueMode != FunctionCallReturnValueMode.SingleNode) throw new InvalidEvaluationException("FunctionCallReturnValueModeUnexpected", new FileTextRange { Text = pr.Text, Range = GetRange(fn) }, fn);
            var Nodes = EvaluateFunction(rfc);
            if (Nodes.Count != 1) throw new InvalidEvaluationException("FunctionCallNotReturnSingleNode", new FileTextRange { Text = pr.Text, Range = GetRange(fn) }, fn);
            return Nodes.Single();
        }

        private List<Semantics.Node> EvaluateFunction(RawFunctionCall rfc)
        {
            List<Semantics.Node> Parameters;
            switch (rfc.Parameters._Tag)
            {
                case RawFunctionCallParametersTag.TokenParameters:
                    if (TokenParameterEvaluator == null) throw new InvalidEvaluationException("TokenParameterEvaluatorIsNull", new FileTextRange { Text = pr.Text, Range = GetRange(rfc) }, rfc);
                    Parameters = TokenParameterEvaluator(rfc, this);
                    break;
                case RawFunctionCallParametersTag.TreeParameter:
                    {
                        var tp = rfc.Parameters.TreeParameter;
                        if (!tp.OnSome)
                        {
                            Parameters = new List<Semantics.Node> { };
                        }
                        else
                        {
                            Parameters = new List<Semantics.Node> { EvaluateSingleLineNode(tp.Value) };
                        }
                        break;
                    }
                case RawFunctionCallParametersTag.TableParameters:
                    {
                        var tp = rfc.Parameters.TableParameters;
                        Parameters = tp.Select(p => EvaluateTableLineNode(p)).ToList();
                        break;
                    }
                default:
                    throw new ArgumentException();
            }

            var Content = Optional<FunctionCallContent>.Empty;
            if (rfc.Content.OnSome)
            {
                var rfcc = rfc.Content.Value;
                switch (rfcc._Tag)
                {
                    case RawFunctionCallContentTag.LineContent:
                        Content = Mark(FunctionCallContent.CreateLineContent(rfcc.LineContent), rfcc);
                        break;
                    case RawFunctionCallContentTag.TreeContent:
                        {
                            var TreeContent = EvaluateMultiNodesList(rfcc.TreeContent);
                            Content = Mark(FunctionCallContent.CreateTreeContent(TreeContent), rfcc);
                            break;
                        }
                    case RawFunctionCallContentTag.TableContent:
                        {
                            var TableContent = rfcc.TableContent.Select(Line => Mark(new FunctionCallTableLine { Nodes = Line.Nodes.Select(LineNode => EvaluateTableLineNode(LineNode)).ToList() }, Line)).ToList();
                            Content = Mark(FunctionCallContent.CreateTableContent(TableContent), rfcc);
                            break;
                        }
                    default:
                        throw new ArgumentException();
                }
            }

            var F = Mark(new FunctionCall { Name = rfc.Name, ReturnValueMode = rfc.ReturnValueMode, Parameters = Parameters, Content = Content }, rfc);

            if (FunctionCallEvaluator == null) throw new InvalidEvaluationException("FunctionCallEvaluatorIsNull", new FileTextRange { Text = pr.Text, Range = GetRange(rfc) }, rfc);
            return FunctionCallEvaluator(F, this);
        }
    }
}
