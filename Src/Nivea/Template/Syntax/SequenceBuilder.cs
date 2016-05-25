//==========================================================================
//
//  File:        SequenceBuilder.cs
//  Location:    Nivea <Visual C#>
//  Description: 序列构建器
//  Version:     2016.05.25.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;

namespace Nivea.Template.Syntax
{
    public class SequenceBuilder
    {
        private List<StackNode> Nodes = new List<StackNode>();
        private Firefly.Texting.TreeFormat.Optional<TextRange> LineRangeStart = Firefly.Texting.TreeFormat.Optional<TextRange>.Empty;
    
        public void PushToken(Token t, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count == 0)
            {
                LineRangeStart = nm.GetRange(t);
            }
     
            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
            {
                var Range = nm.GetRange(SyntaxObj);
                if (Range.OnHasValue)
                {
                    Positions.Add(SemanticsObj, Range.Value);
                }
            };
            Action<Object, Object, Object> Mark2 = (SemanticsObj, SyntaxObjStart, SyntaxObjEnd) =>
            {
                var RangeStart = nm.GetRange(SyntaxObjStart);
                var RangeEnd = nm.GetRange(SyntaxObjEnd);
                if (RangeStart.OnHasValue && RangeEnd.OnHasValue)
                {
                    Positions.Add(SemanticsObj, new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End });
                }
            };

            if (t.Type.OnDirect)
            {
                var n = ExprNode.CreateDirect(t.Type.Direct);
                Mark(n, t);
                Nodes.Add(StackNode.CreateNode(n));
            }
            else if (t.Type.OnQuoted)
            {
                var n = ExprNode.CreateLiteral(t.Type.Quoted);
                Mark(n, t);
                Nodes.Add(StackNode.CreateNode(n));
            }
            else if (t.Type.OnEscaped)
            {
                var n = ExprNode.CreateLiteral(t.Type.Escaped);
                Mark(n, t);
                Nodes.Add(StackNode.CreateNode(n));
            }
            else if (t.Type.OnLeftParenthesis)
            {
                Nodes.Add(StackNode.CreateToken(t));
            }
            else if (t.Type.OnRightParenthesis)
            {
                Reduce(t, nm, Positions);
                var InnerNodes = Nodes.AsEnumerable().Reverse().TakeWhile(n => !(n.OnToken && n.Token.Type.OnLeftParenthesis)).Reverse().ToList();
                if (Nodes.Count - InnerNodes.Count - 1 < 0)
                {
                    throw new InvalidSyntaxException("InvalidParenthesis", nm.GetFileRange(t));
                }
                var LeftParenthesis = Nodes[Nodes.Count - InnerNodes.Count - 1].Token;
                var Children = new List<ExprNode>();
                if (InnerNodes.Count > 0)
                {
                    Children = Split(InnerNodes, n => n.OnToken && n.Token.Type.OnComma).Select(Part => Part.Single().Node).ToList();
                }
                if (LeftParenthesis.IsAfterSpace && !LeftParenthesis.IsLeadingToken)
                {
                    var ParentNode = Nodes[Nodes.Count - InnerNodes.Count - 2];
                    if (ParentNode.OnNode)
                    {
                        var Stem = new ExprNodeStem { Head = ParentNode.Node, Nodes = Children };
                        Mark2(Stem, ParentNode, t);
                        var Node = ExprNode.CreateStem(Stem);
                        Mark2(Node, ParentNode, t);
                        Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 2, InnerNodes.Count + 2);
                        Nodes.Add(StackNode.CreateNode(Node));
                        return;
                    }
                }
                if (Children.Count == 1)
                {
                    Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 1, InnerNodes.Count + 1);
                    Nodes.Add(StackNode.CreateNode(Children.Single()));
                }
                else
                {
                    var Stem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = Children };
                    Mark2(Stem, LeftParenthesis, t);
                    var Node = ExprNode.CreateStem(Stem);
                    Mark2(Node, LeftParenthesis, t);
                    Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 1, InnerNodes.Count + 1);
                    Nodes.Add(StackNode.CreateNode(Node));
                }
            }
            else if (t.Type.OnComma)
            {
                Reduce(t, nm, Positions);
                Nodes.Add(StackNode.CreateToken(t));
            }
            else if (t.Type.OnPreprocessDirective)
            {
                Nodes.Add(StackNode.CreateToken(t));
            }
            else if (t.Type.OnOperator)
            {
                var n = ExprNode.CreateOperator(t.Type.Operator);
                Mark(n, t);
                Nodes.Add(StackNode.CreateNode(n));
            }
            else if (t.Type.OnSingleLineComment)
            {
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public void PushNode(ExprNode n)
        {
            Nodes.Add(StackNode.CreateNode(n));
        }

        private void Reduce(Token t, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var InnerNodes = Nodes.AsEnumerable().Reverse().TakeWhile(n => !(n.OnToken && (n.Token.Type.OnLeftParenthesis || n.Token.Type.OnComma))).Reverse().ToList();
            var RangeStart = LineRangeStart;
            if (Nodes.Count - InnerNodes.Count - 1 >= 0)
            {
                var LeftParenthesisOrComma = Nodes[Nodes.Count - InnerNodes.Count - 1].Token;
                if (LeftParenthesisOrComma.Type.OnComma && (InnerNodes.Count == 0))
                {
                    throw new InvalidSyntaxException("InvalidParenthesis", nm.GetFileRange(t));
                }
                RangeStart = nm.GetRange(LeftParenthesisOrComma);
            }

            foreach (var n in InnerNodes)
            {
                if (!n.OnNode)
                {
                    throw new InvalidSyntaxException("InvalidSyntaxRule", nm.GetFileRange(n.Token));
                }
            }
            if (InnerNodes.Count > 1)
            {
                var RangeEnd = nm.GetRange(t);

                var Children = InnerNodes.Select(Part => Part.Node).ToList();
                var Undetermined = new ExprNodeUndetermined { Nodes = Children };
                var Node = ExprNode.CreateUndetermined(Undetermined);
                if (RangeStart.OnHasValue && RangeEnd.OnHasValue)
                {
                    Positions.Add(Undetermined, new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End });
                    Positions.Add(Node, new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End });
                }
                Nodes.RemoveRange(Nodes.Count - InnerNodes.Count, InnerNodes.Count);
                Nodes.Add(StackNode.CreateNode(Node));
            }
        }

        public Boolean IsInParenthesis
        {
            get
            {
                return Nodes.AsEnumerable().Reverse().Any(n => n.OnToken && n.Token.Type.OnLeftParenthesis);
            }
        }

        public Boolean TryReducePreprocessDirective(Func<Token, List<ExprNode>, List<ExprNode>> Transform, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var InnerNodes = Nodes.AsEnumerable().Reverse().TakeWhile(n => !(n.OnToken && (n.Token.Type.OnLeftParenthesis || n.Token.Type.OnComma))).Reverse().ToList();
            if (InnerNodes.Count == 0) { return false; }
            var FirstNode = InnerNodes.First();
            if (!FirstNode.OnToken) { return false; }
            if (!FirstNode.Token.Type.OnPreprocessDirective) { return false; }
            foreach (var n in InnerNodes.Skip(1))
            {
                if (!n.OnNode)
                {
                    throw new InvalidSyntaxException("InvalidSyntaxRule", nm.GetFileRange(n.Token));
                }
            }
            var Transformed = Transform(FirstNode.Token, InnerNodes.Skip(1).Select(n => n.Node).ToList());
            Nodes.RemoveRange(Nodes.Count - InnerNodes.Count, InnerNodes.Count);
            foreach (var n in Transformed)
            {
                Nodes.Add(StackNode.CreateNode(n));
            }
            return true;
        }

        public List<ExprNode> GetResult(ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            foreach (var n in Nodes)
            {
                if (!n.OnNode)
                {
                    throw new InvalidSyntaxException("InvalidSyntaxRule", nm.GetFileRange(n.Token));
                }
            }
            return Nodes.Select(n => n.Node).ToList();
        }

        private List<List<T>> Split<T>(IEnumerable<T> Sequenece, Func<T, Boolean> IsSeparator)
        {
            var ll = new List<List<T>>();
            var l = new List<T>();
            foreach (var v in Sequenece)
            {
                if (IsSeparator(v))
                {
                    ll.Add(l);
                    l = new List<T>();
                }
                else
                {
                    l.Add(v);
                }
            }
            ll.Add(l);
            return ll;
        }
    }
}
