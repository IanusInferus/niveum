//==========================================================================
//
//  File:        SequenceBuilder.cs
//  Location:    Nivea <Visual C#>
//  Description: 序列构建器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

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
        private Optional<TextRange> LineRangeStart = Optional<TextRange>.Empty;

        public void PushToken(Token t, Text Text, Dictionary<Object, TextRange> TokenPositions, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count == 0)
            {
                if (TokenPositions.ContainsKey(t))
                {
                    LineRangeStart = TokenPositions[t];
                }
            }

            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
            {
                if (TokenPositions.ContainsKey(SyntaxObj))
                {
                    var Range = TokenPositions[SyntaxObj];
                    Positions.Add(SemanticsObj, Range);
                }
            };
            Action<Object, Object, Object> Mark2 = (SemanticsObj, SyntaxObjStart, SyntaxObjEnd) =>
            {
                if ((TokenPositions.ContainsKey(SyntaxObjStart) || Positions.ContainsKey(SyntaxObjStart)) && TokenPositions.ContainsKey(SyntaxObjEnd))
                {
                    var RangeStart = TokenPositions.ContainsKey(SyntaxObjStart) ? TokenPositions[SyntaxObjStart] : Positions[SyntaxObjStart];
                    var RangeEnd = TokenPositions[SyntaxObjEnd];
                    Positions.Add(SemanticsObj, new TextRange { Start = RangeStart.Start, End = RangeEnd.End });
                }
            };

            if (t.Type.OnDirect || (t.Type.OnOperator && (t.Type.Operator != ".")))
            {
                if (Nodes.Count >= 2)
                {
                    var PrevNode = Nodes.Last();
                    if (PrevNode.OnNode && PrevNode.Node.OnOperator && (PrevNode.Node.Operator == "."))
                    {
                        var PrevPrevNode = Nodes[Nodes.Count - 2];
                        if (PrevPrevNode.OnNode)
                        {
                            var Parent = PrevPrevNode.Node;
                            var Child = t.Type.OnDirect ? ExprNode.CreateDirect(t.Type.Direct) : ExprNode.CreateOperator(t.Type.Operator);
                            Mark(Child, t);
                            var Member = new ExprNodeMember { Parent = Parent, Child = Child };
                            var nn = ExprNode.CreateMember(Member);
                            if (Positions.ContainsKey(Parent))
                            {
                                Mark2(Member, Positions[Parent], t);
                                Mark2(nn, Positions[Parent], t);
                            }
                            Nodes.RemoveRange(Nodes.Count - 2, 2);
                            Nodes.Add(StackNode.CreateNode(nn));
                            return;
                        }
                    }
                }
            }

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
                Reduce(t, Text, TokenPositions, Positions);
                var InnerNodes = Nodes.AsEnumerable().Reverse().TakeWhile(n => !(n.OnToken && n.Token.Type.OnLeftParenthesis)).Reverse().ToList();
                if (Nodes.Count - InnerNodes.Count - 1 < 0)
                {
                    throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = Text, Range = TokenPositions.ContainsKey(t) ? TokenPositions[t] : Firefly.Texting.TreeFormat.Optional<TextRange>.Empty });
                }
                var LeftParenthesis = Nodes[Nodes.Count - InnerNodes.Count - 1].Token;
                var Children = new List<ExprNode>();
                if (InnerNodes.Count > 0)
                {
                    Children = Split(InnerNodes, n => n.OnToken && n.Token.Type.OnComma).Select(Part => Part.Single().Node).ToList();
                }
                if (!LeftParenthesis.IsAfterSpace && !LeftParenthesis.IsLeadingToken)
                {
                    var ParentNode = Nodes[Nodes.Count - InnerNodes.Count - 2];
                    if (ParentNode.OnNode)
                    {
                        if (Children.Count == 1)
                        {
                            var One = Children.Single();
                            if (One.OnStem && !One.Stem.Head.OnSome && One.Stem.CanMerge)
                            {
                                Children = One.Stem.Nodes;
                            }
                            else
                            {
                                Mark2(Children, LeftParenthesis, t);
                            }
                        }
                        else
                        {
                            Mark2(Children, LeftParenthesis, t);
                        }
                        var Stem = new ExprNodeStem { Head = ParentNode.Node, Nodes = Children, CanMerge = false };
                        Mark2(Stem, ParentNode.Node, t);
                        var Node = ExprNode.CreateStem(Stem);
                        Mark2(Node, ParentNode.Node, t);
                        Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 2, InnerNodes.Count + 2);
                        Nodes.Add(StackNode.CreateNode(Node));
                        return;
                    }
                }
                if (Children.Count == 1)
                {
                    var One = Children.Single();
                    if (One.OnStem && One.Stem.CanMerge)
                    {
                        var Stem = new ExprNodeStem { Head = One.Stem.Head, Nodes = One.Stem.Nodes, CanMerge = false };
                        Mark2(Stem, LeftParenthesis, t);
                        var Node = ExprNode.CreateStem(Stem);
                        Mark2(Node, LeftParenthesis, t);
                        Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 1, InnerNodes.Count + 1);
                        Nodes.Add(StackNode.CreateNode(Node));
                        return;
                    }
                    else if (One.OnUndetermined)
                    {
                        Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 1, InnerNodes.Count + 1);
                        Nodes.Add(StackNode.CreateNode(One));
                        return;
                    }
                }
                {
                    Mark2(Children, LeftParenthesis, t);
                    var Stem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = Children, CanMerge = false };
                    Mark2(Stem, LeftParenthesis, t);
                    var Node = ExprNode.CreateStem(Stem);
                    Mark2(Node, LeftParenthesis, t);
                    Nodes.RemoveRange(Nodes.Count - InnerNodes.Count - 1, InnerNodes.Count + 1);
                    Nodes.Add(StackNode.CreateNode(Node));
                }
            }
            else if (t.Type.OnComma)
            {
                Reduce(t, Text, TokenPositions, Positions);
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

        private void Reduce(Token t, Text Text, Dictionary<Object, TextRange> TokenPositions, Dictionary<Object, TextRange> Positions)
        {
            var InnerNodes = Nodes.AsEnumerable().Reverse().TakeWhile(n => !(n.OnToken && (n.Token.Type.OnLeftParenthesis || n.Token.Type.OnComma))).Reverse().ToList();
            var RangeStart = LineRangeStart;
            if (Nodes.Count - InnerNodes.Count - 1 >= 0)
            {
                var LeftParenthesisOrComma = Nodes[Nodes.Count - InnerNodes.Count - 1].Token;
                if (LeftParenthesisOrComma.Type.OnComma && (InnerNodes.Count == 0))
                {
                    throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = Text, Range = TokenPositions.ContainsKey(t) ? TokenPositions[t] : Firefly.Texting.TreeFormat.Optional<TextRange>.Empty });
                }
                if (TokenPositions.ContainsKey(LeftParenthesisOrComma))
                {
                    RangeStart = TokenPositions[LeftParenthesisOrComma];
                }
            }

            foreach (var n in InnerNodes)
            {
                if (!n.OnNode)
                {
                    throw new InvalidSyntaxException("InvalidSyntaxRule", new FileTextRange { Text = Text, Range = TokenPositions.ContainsKey(n.Token) ? TokenPositions[n.Token] : Firefly.Texting.TreeFormat.Optional<TextRange>.Empty });
                }
            }
            if (InnerNodes.Count > 1)
            {
                var RangeEnd = Optional<TextRange>.Empty;
                if (TokenPositions.ContainsKey(t))
                {
                    RangeEnd = TokenPositions[t];
                }

                var Children = InnerNodes.Select(Part => Part.Node).ToList();
                var Undetermined = new ExprNodeUndetermined { Nodes = Children };
                var Node = ExprNode.CreateUndetermined(Undetermined);
                if (RangeStart.OnSome && RangeEnd.OnSome)
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

        public Boolean IsCurrentLeftParenthesis
        {
            get
            {
                if (Nodes.Count == 0) { return false; }
                var Last = Nodes.Last();
                if (!Last.OnToken) { return false; }
                return Last.Token.Type.OnLeftParenthesis;
            }
        }

        public Boolean TryReducePreprocessDirective(Func<Token, List<ExprNode>, List<ExprNode>> Transform, Text Text, Dictionary<Object, TextRange> TokenPositions, Dictionary<Object, TextRange> Positions)
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
                    throw new InvalidSyntaxException("InvalidSyntaxRule", new FileTextRange { Text = Text, Range = TokenPositions.ContainsKey(n.Token) ? TokenPositions[n.Token] : Firefly.Texting.TreeFormat.Optional<TextRange>.Empty });
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

        public List<ExprNode> GetResult(Text Text, Dictionary<Object, TextRange> TokenPositions, Dictionary<Object, TextRange> Positions)
        {
            foreach (var n in Nodes)
            {
                if (!n.OnNode)
                {
                    throw new InvalidSyntaxException("InvalidSyntaxRule", new FileTextRange { Text = Text, Range = TokenPositions.ContainsKey(n.Token) ? TokenPositions[n.Token] : Firefly.Texting.TreeFormat.Optional<TextRange>.Empty });
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
