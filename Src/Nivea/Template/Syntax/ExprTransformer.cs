//==========================================================================
//
//  File:        ExprTransformer.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式转换器
//  Version:     2016.06.01.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Firefly.Texting.TreeFormat.Syntax;
using Nivea.Template.Semantics;

namespace Nivea.Template.Syntax
{
    public static class ExprTransformer
    {
        public static Expr Transform(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Node.OnDirect)
            {
                var s = Node.Direct;

                if (s == "Throw")
                {
                    return Mark(Expr.CreateThrow(Optional<Expr>.Empty), Node, NodePositions, Positions);
                }
                else if (s == "Continue")
                {
                    return Mark(Expr.CreateContinue(Optional<int>.Empty), Node, NodePositions, Positions);
                }
                else if (s == "Break")
                {
                    return Mark(Expr.CreateBreak(Optional<int>.Empty), Node, NodePositions, Positions);
                }
                else if (s == "Return")
                {
                    return Mark(Expr.CreateReturn(Optional<Expr>.Empty), Node, NodePositions, Positions);
                }
                else if (s == "Null")
                {
                    return Mark(Expr.CreateNull(), Node, NodePositions, Positions);
                }
                else if (s == "Default")
                {
                    return Mark(Expr.CreateDefault(), Node, NodePositions, Positions);
                }
                else if (s == "This")
                {
                    var e = Mark(VariableRef.CreateThis(), Node, NodePositions, Positions);
                    return Mark(Expr.CreateVariableRef(e), Node, NodePositions, Positions);
                }
                else if (s == "True")
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = "Boolean", Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = "True" }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (s == "False")
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = "Boolean", Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = "False" }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (TokenParser.IsIntLiteral(s))
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = "Int", Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = s }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (TokenParser.IsFloatLiteral(s))
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = "Real", Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = s }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }

                var Ambiguous = new List<Expr> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Node, Text, NodePositions, Positions);
                if (otvmc.OnHasValue)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    Ambiguous.Add(Mark(Expr.CreateTypeLiteral(t), Node, NodePositions, Positions));
                    Ambiguous.Add(tvmc.Variable);

                    if (t.OnTypeRef || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Version == "")))
                    {
                        var tule = Mark(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name, Expr = Optional<Expr>.Empty }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateTaggedUnionLiteral(tule), Node, NodePositions, Positions));

                        var ele = Mark(new EnumLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Name = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateEnumLiteral(ele), Node, NodePositions, Positions));
                    }
                }

                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
                    Mark(Ambiguous, Node, NodePositions, Positions);
                    return Mark(Expr.CreateAmbiguous(Ambiguous), Node, NodePositions, Positions);
                }
            }
            else if (Node.OnLiteral)
            {
                var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = "String", Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                return Mark(Expr.CreatePrimitiveLiteral(Mark(new PrimitiveLiteralExpr { Type = t, Value = Node.Literal }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
            }
            else if (Node.OnOperator)
            {
                var e = Mark(VariableRef.CreateName(Node.Operator), Node, NodePositions, Positions);
                return Mark(Expr.CreateVariableRef(e), Node, NodePositions, Positions);
            }
            else if (Node.OnTemplate)
            {
                return Mark(Expr.CreateTemplate(Node.Template), Node, NodePositions, Positions);
            }
            else if (Node.OnYieldTemplate)
            {
                return Mark(Expr.CreateYieldTemplate(Node.YieldTemplate), Node, NodePositions, Positions);
            }
            else if (Node.OnStem)
            {
                var Stem = Node.Stem;
                if (Stem.Head.OnHasValue)
                {
                    var Transformed = TransformStem(Stem.Head.Value, Stem.Nodes, NodePositions.ContainsKey(Node) ? NodePositions[Node] : Optional<TextRange>.Empty, NodePositions.ContainsKey(Stem.Nodes) ? NodePositions[Stem.Nodes] : Optional<TextRange>.Empty, Text, NodePositions, Positions);
                    return Transformed;
                }
                else
                {
                    var Transformed = Stem.Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
                    Mark(Transformed, Node, NodePositions, Positions);
                    return Mark(Expr.CreateSequence(Transformed), Node, NodePositions, Positions);
                }
            }
            else if (Node.OnUndetermined)
            {
                var Nodes = Node.Undetermined.Nodes;
                var Transformed = TransformNodes(Nodes, NodePositions.ContainsKey(Nodes) ? NodePositions[Nodes] : Optional<TextRange>.Empty, Text, NodePositions, Positions);
                return Transformed;
            }
            else if (Node.OnMember)
            {
                var ParentExpr = Transform(Node.Member.Parent, Text, NodePositions, Positions);
                var Child = Node.Member.Child;
                if (Child.OnDirect)
                {
                    var oe = TryTransformVariableMemberChain(Child.Direct, Child, ParentExpr, Text, NodePositions, Positions);
                    if (oe.OnHasValue)
                    {
                        return oe.Value;
                    }
                }
                else if (Child.OnOperator)
                {
                    var ChildExpr = VariableRef.CreateName(Child.Operator);
                    var ma = new MemberAccess { Parent = ParentExpr, Child = ChildExpr };
                    var vma = VariableRef.CreateMemberAccess(ma);
                    var vv = Expr.CreateVariableRef(vma);
                    Mark(ChildExpr, Node, NodePositions, Positions);
                    Mark(ma, Node, NodePositions, Positions);
                    Mark(vma, Node, NodePositions, Positions);
                    Mark(vv, Node, NodePositions, Positions);
                    return vv;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }

            return Mark(Expr.CreateError(), Node, NodePositions, Positions);
        }

        private static Expr TransformNodes(List<ExprNode> Nodes, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            return TransformNodes(Nodes, GetRange(Nodes, NodePositions), Text, NodePositions, Positions);
        }
        private static Expr TransformNodes(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count == 0)
            {
                return MarkRange(Expr.CreateSequence(MarkRange(new List<Expr> { }, Range, Positions)), Range, Positions);
            }
            if (Nodes.Count >= 1)
            {
                var First = Nodes.First();
                if (First.OnDirect)
                {
                    var s = First.Direct;
                    if (s == "Let")
                    {
                        if (Nodes.Count >= 4)
                        {
                            var Third = Nodes[2];
                            if (Third.OnOperator && Third.Operator == "=")
                            {
                                var SecondExpr = TransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                                var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), GetRange(Nodes.Skip(3), NodePositions), Text, NodePositions, Positions);
                                var e = MarkRange(new LetExpr { Left = SecondExpr, Right = FourthExpr }, Range, Positions);
                                return MarkRange(Expr.CreateLet(e), Range, Positions);
                            }
                        }
                    }
                    else if (s == "Var")
                    {
                        if (Nodes.Count >= 4)
                        {
                            var Third = Nodes[2];
                            if (Third.OnOperator && Third.Operator == "=")
                            {
                                var SecondExpr = TransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                                var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), GetRange(Nodes.Skip(3), NodePositions), Text, NodePositions, Positions);
                                var e = MarkRange(new VarExpr { Left = SecondExpr, Right = FourthExpr }, Range, Positions);
                                return MarkRange(Expr.CreateVar(e), Range, Positions);
                            }
                        }
                    }
                    else if (s == "If")
                    {
                        if (Nodes.Count == 2)
                        {
                            var Branches = TransformIfBranchList(Nodes.Last(), Text, NodePositions, Positions);
                            var e = MarkRange(new IfExpr { Branches = Branches }, Range, Positions);
                            return MarkRange(Expr.CreateIf(e), Range, Positions);
                        }
                        else if (Nodes.Count >= 3)
                        {
                            var Branch = TransformIfBranch(Nodes.Skip(1).ToList(), GetRange(Nodes.Skip(1), NodePositions), Text, NodePositions, Positions);
                            var Branches = MarkRange(new List<IfBranch> { Branch }, Range, Positions);
                            var e = MarkRange(new IfExpr { Branches = Branches }, Range, Positions);
                            return MarkRange(Expr.CreateIf(e), Range, Positions);
                        }
                    }
                    else if (s == "Match")
                    {
                        if (Nodes.Count >= 3)
                        {
                            var MiddleExpr = TransformNodes(Nodes.Skip(1).Take(Nodes.Count - 2).ToList(), Text, NodePositions, Positions);
                            var LastExpr = TransformMatchAlternativeList(Nodes.Last(), Text, NodePositions, Positions);
                            var e = MarkRange(new MatchExpr { Target = MiddleExpr, Alternatives = LastExpr }, Range, Positions);
                            return MarkRange(Expr.CreateMatch(e), Range, Positions);
                        }
                    }
                    else if (s == "For")
                    {
                        if (Nodes.Count >= 5)
                        {
                            var SecondExpr = TransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                            var MiddleExpr = TransformNodes(Nodes.Skip(3).Take(Nodes.Count - 4).ToList(), Text, NodePositions, Positions);
                            var LastExpr = Transform(Nodes.Last(), Text, NodePositions, Positions);
                            var e = MarkRange(new ForExpr { EnumeratedValue = SecondExpr, Enumerable = MiddleExpr, Body = LastExpr }, Range, Positions);
                            return MarkRange(Expr.CreateFor(e), Range, Positions);
                        }
                    }
                    else if (s == "While")
                    {
                        if (Nodes.Count >= 3)
                        {
                            var MiddleExpr = TransformNodes(Nodes.Skip(1).Take(Nodes.Count - 2).ToList(), Text, NodePositions, Positions);
                            var LastExpr = Transform(Nodes.Last(), Text, NodePositions, Positions);
                            var e = MarkRange(new WhileExpr { Condition = MiddleExpr, Body = LastExpr }, Range, Positions);
                            return MarkRange(Expr.CreateWhile(e), Range, Positions);
                        }
                    }
                }
            }
            if (Nodes.Count >= 3)
            {
                foreach (var i in Enumerable.Range(1, Nodes.Count - 2))
                {
                    var n = Nodes[i];
                    if (n.OnOperator)
                    {
                        var s = n.Operator;
                        if (s == "=")
                        {
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), GetRange(Nodes.Take(i), NodePositions), Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new AssignExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateAssign(e), Range, Positions);
                        }
                        else if (s == "+=")
                        {
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), GetRange(Nodes.Take(i), NodePositions), Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new IncreaseExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateIncrease(e), Range, Positions);
                        }
                        else if (s == "-=")
                        {
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), GetRange(Nodes.Take(i), NodePositions), Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new DecreaseExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateDecrease(e), Range, Positions);
                        }
                        else if (s == "=>")
                        {
                            if (i == 1)
                            {
                                var LeftExpr = TransformLeftValueDefList(Nodes[0], Text, NodePositions, Positions);
                                var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Text, NodePositions, Positions);
                                var e = MarkRange(new LambdaExpr { Parameters = LeftExpr, Body = RightExpr }, Range, Positions);
                                return MarkRange(Expr.CreateLambda(e), Range, Positions);
                            }
                        }
                    }
                }
            }
            if (Nodes.Count == 3)
            {
                var Second = Nodes[1];
                if (Second.OnOperator)
                {
                    var s = Second.Operator;
                    var Operator = Mark(Expr.CreateVariableRef(Mark(VariableRef.CreateName(s), Second, NodePositions, Positions)), Second, NodePositions, Positions);
                    var LeftExpr = Transform(Nodes[0], Text, NodePositions, Positions);
                    var RightExpr = Transform(Nodes[2], Text, NodePositions, Positions);
                    var l = MarkRange(new List<Expr> { LeftExpr, RightExpr }, GetRange(Nodes, NodePositions), Positions);
                    var e = MarkRange(new FunctionCallExpr { Func = Operator, Parameters = l }, Range, Positions);
                    return MarkRange(Expr.CreateFunctionCall(e), Range, Positions);
                }
            }
            if (Nodes.Count >= 2)
            {
                var First = Nodes.First();
                var Rest = Nodes.Skip(1).ToList();
                var RestRange = GetRange(Rest, NodePositions);
                if (Rest.Count == 1)
                {
                    var One = Rest.Single();
                    if (One.OnStem && One.Stem.Head.OnNotHasValue && One.Stem.CanMerge)
                    {
                        Rest = One.Stem.Nodes;
                        if (NodePositions.ContainsKey(Rest))
                        {
                            RestRange = NodePositions[Rest];
                        }
                    }
                }

                if (First.OnOperator)
                {
                    var s = First.Operator;
                    var Operator = Mark(Expr.CreateVariableRef(Mark(VariableRef.CreateName(s), First, NodePositions, Positions)), First, NodePositions, Positions);
                    var OperandExpr = Rest.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
                    MarkRange(OperandExpr, RestRange, Positions);
                    var fce = MarkRange(new FunctionCallExpr { Func = Operator, Parameters = OperandExpr }, Range, Positions);
                    return MarkRange(Expr.CreateFunctionCall(fce), Range, Positions);
                }

                return TransformStem(First, Rest, Range, RestRange, Text, NodePositions, Positions);
            }
            else if (Nodes.Count == 1)
            {
                return Transform(Nodes.Single(), Text, NodePositions, Positions);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Expr TransformStem(ExprNode Head, List<ExprNode> Nodes, Optional<TextRange> Range, Optional<TextRange> NodesRange, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var Transformed = Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
            MarkRange(Transformed, NodesRange, Positions);

            if (Head.OnDirect)
            {
                var s = Head.Direct;
                if (s == "Yield")
                {
                    if (Transformed.Count == 1)
                    {
                        return MarkRange(Expr.CreateYield(Transformed.Single()), Range, Positions);
                    }
                }
                else if (s == "YieldMany")
                {
                    if (Transformed.Count == 1)
                    {
                        return MarkRange(Expr.CreateYieldMany(Transformed.Single()), Range, Positions);
                    }
                }
                else if (s == "Throw")
                {
                    if (Transformed.Count == 1)
                    {
                        return MarkRange(Expr.CreateThrow(Transformed.Single()), Range, Positions);
                    }
                }
                else if (s == "Continue")
                {
                    if (Nodes.Count == 1)
                    {
                        var One = Nodes.Single();
                        if (One.OnDirect)
                        {
                            var i = 0;
                            if (int.TryParse(One.Direct, out i))
                            {
                                return MarkRange(Expr.CreateContinue(i), Range, Positions);
                            }
                        }
                    }
                }
                else if (s == "Break")
                {
                    if (Nodes.Count == 1)
                    {
                        var One = Nodes.Single();
                        if (One.OnDirect)
                        {
                            var i = 0;
                            if (int.TryParse(One.Direct, out i))
                            {
                                return MarkRange(Expr.CreateBreak(i), Range, Positions);
                            }
                        }
                    }
                }
                else if (s == "Return")
                {
                    if (Transformed.Count == 1)
                    {
                        return MarkRange(Expr.CreateReturn(Transformed.Single()), Range, Positions);
                    }
                }
                else if (s == "Cast")
                {
                    if (Transformed.Count == 2)
                    {
                        var First = Transformed[0];
                        var Second = Transformed[1];
                        if (Second.OnTypeLiteral)
                        {
                            var ce = MarkRange(new CastExpr { Operand = First, Type = Second.TypeLiteral }, NodesRange, Positions);
                            return MarkRange(Expr.CreateCast(ce), Range, Positions);
                        }
                        else if (Second.OnAmbiguous)
                        {
                            var TypeLiterals = Second.Ambiguous.Where(a => a.OnTypeLiteral).ToList();
                            if (TypeLiterals.Count == 1)
                            {
                                var ce = MarkRange(new CastExpr { Operand = First, Type = TypeLiterals.Single().TypeLiteral }, NodesRange, Positions);
                                return MarkRange(Expr.CreateCast(ce), Range, Positions);
                            }
                        }
                    }
                }

                var Ambiguous = new List<Expr> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Head, Text, NodePositions, Positions);
                if (otvmc.OnHasValue)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    if (Nodes.Count == 0)
                    {
                        var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = Optional<String>.Empty }, NodesRange, NodePositions, Positions);
                        Ambiguous.Add(MarkRange(Expr.CreatePrimitiveLiteral(ple), Range, Positions));
                    }
                    else if (Nodes.Count == 1)
                    {
                        var One = Nodes.Single();
                        if (One.OnDirect || One.OnLiteral || One.OnOperator)
                        {
                            var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = One.OnDirect ? One.Direct : One.OnLiteral ? One.Literal : One.OnOperator ? One.Operator : "" }, NodesRange, NodePositions, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreatePrimitiveLiteral(ple), Range, Positions));
                        }
                    }
                    {
                        var FieldAssigns = new List<FieldAssign>();
                        var FieldAssignRanges = new Dictionary<FieldAssign, TextRange>();
                        foreach (var e in Transformed)
                        {
                            if (e.OnAssign)
                            {
                                var a = e.Assign;
                                if (a.Left.Count == 1)
                                {
                                    var al = a.Left.Single();
                                    if (al.OnVariable && al.Variable.OnName)
                                    {
                                        var fa = new FieldAssign { Name = al.Variable.Name, Expr = a.Right };
                                        FieldAssigns.Add(fa);
                                        if (Positions.ContainsKey(a))
                                        {
                                            FieldAssignRanges.Add(fa, Positions[a]);
                                        }
                                    }
                                    else
                                    {
                                        FieldAssigns.Clear();
                                        break;
                                    }
                                }
                                else
                                {
                                    FieldAssigns.Clear();
                                    break;
                                }
                            }
                            else
                            {
                                FieldAssigns.Clear();
                                break;
                            }
                        }

                        if (FieldAssigns.Count > 0)
                        {
                            foreach (var fa in FieldAssigns)
                            {
                                if (FieldAssignRanges.ContainsKey(fa))
                                {
                                    Positions.Add(fa, FieldAssignRanges[fa]);
                                }
                            }
                            var rle = MarkRange(new RecordLiteralExpr { Type = t, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateRecordLiteral(rle), Range, Positions));
                        }
                        else
                        {
                            if (Transformed.Count == 0)
                            {
                                var rle = MarkRange(new RecordLiteralExpr { Type = t, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                                Ambiguous.Add(MarkRange(Expr.CreateRecordLiteral(rle), Range, Positions));
                            }
                            if (Transformed.Count >= 2)
                            {
                                if (t.OnTuple)
                                {
                                    var tle = MarkRange(new TupleLiteralExpr { Type = t, Parameters = Transformed }, Range, Positions);
                                    return MarkRange(Expr.CreateTupleLiteral(tle), Range, Positions);
                                }
                                else if (t.OnTypeRef && (t.TypeRef.Name == "Tuple") && (t.TypeRef.Version == ""))
                                {
                                    var tle = MarkRange(new TupleLiteralExpr { Type = Optional<TypeSpec>.Empty, Parameters = Transformed }, Range, Positions);
                                    Ambiguous.Add(MarkRange(Expr.CreateTupleLiteral(tle), Range, Positions));
                                }
                            }
                            var lle = MarkRange(new ListLiteralExpr { Type = t, Parameters = Transformed }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateListLiteral(lle), Range, Positions));
                            var fce = MarkRange(new FunctionCallExpr { Func = tvmc.Variable, Parameters = Transformed }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateFunctionCall(fce), Range, Positions));
                            var ia = MarkRange(new IndexerAccess { Expr = tvmc.Variable, Index = Transformed }, Range, Positions);
                            var vr = MarkRange(VariableRef.CreateIndexerAccess(ia), Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateVariableRef(vr), Range, Positions));
                        }

                        if (Transformed.Count == 1)
                        {
                            var One = Transformed.Single();
                            if (t.OnTypeRef || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Version == "")))
                            {
                                var tule = MarkRange(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name, Expr = One }, Range, Positions);
                                Ambiguous.Add(MarkRange(Expr.CreateTaggedUnionLiteral(tule), Range, Positions));
                            }
                        }
                    }
                }

                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
                    MarkRange(Ambiguous, Range, Positions);
                    return MarkRange(Expr.CreateAmbiguous(Ambiguous), Range, Positions);
                }
            }
            else
            {
                var HeadExpr = Transform(Head, Text, NodePositions, Positions);

                var Ambiguous = new List<Expr> { };

                var fce = MarkRange(new FunctionCallExpr { Func = HeadExpr, Parameters = Transformed }, Range, Positions);
                Ambiguous.Add(MarkRange(Expr.CreateFunctionCall(fce), Range, Positions));
                var ia = MarkRange(new IndexerAccess { Expr = HeadExpr, Index = Transformed }, Range, Positions);
                var vr = MarkRange(VariableRef.CreateIndexerAccess(ia), Range, Positions);
                Ambiguous.Add(MarkRange(Expr.CreateVariableRef(vr), Range, Positions));
                
                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
                    MarkRange(Ambiguous, Range, Positions);
                    return MarkRange(Expr.CreateAmbiguous(Ambiguous), Range, Positions);
                }
            }

            return MarkRange(Expr.CreateError(), Range, Positions);
        }

        private static List<LeftValueDef> TransformLeftValueDefList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new List<LeftValueDef> { };
        }

        private static List<IfBranch> TransformIfBranchList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new List<IfBranch> { };
        }
        private static IfBranch TransformIfBranch(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new IfBranch { Condition = Expr.CreateNull(), Expr = Expr.CreateNull() };
        }

        private static List<MatchAlternative> TransformMatchAlternativeList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new List<MatchAlternative> { };
        }

        private static List<LeftValueRef> TransformLeftValueRefList(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new List<LeftValueRef> { };
        }

        private class TypeVariableMemberChain
        {
            public TypeSpec Type;
            public Expr TypeLiteral;
            public Expr Variable;
        }
        private static Optional<TypeVariableMemberChain> TryTransformTypeVariableMemberChain(String NodeString, ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            Action<Object, int, int> Mark = (o, Start, End) =>
            {
                if (NodePositions.ContainsKey(Node))
                {
                    var Range = NodePositions[Node];
                    var TypeRange = new TextRange { Start = Text.Calc(Range.Start, Start), End = Text.Calc(Range.Start, End) };
                    Positions.Add(o, TypeRange);
                }
            };

            int InvalidCharIndex;
            var osml = TokenParser.TrySplitSymbolMemberChain(NodeString, out InvalidCharIndex);
            if (osml.OnNotHasValue)
            {
                return Optional<TypeVariableMemberChain>.Empty;
            }
            var sml = osml.Value;

            var tTotal = Optional<TypeSpec>.Empty;
            var tlTotal = Optional<Expr>.Empty;
            var vTotal = Optional<Expr>.Empty;
            Expr Ambiguous = null;
            var FirstStart = 0;
            foreach (var s in sml)
            {
                var LocalInvalidCharIndex = 0;
                var oName = TokenParser.TryUnescapeSymbolName(s.Name, out LocalInvalidCharIndex);
                if (oName.OnNotHasValue)
                {
                    InvalidCharIndex = s.NameStartIndex + LocalInvalidCharIndex;
                    return Optional<TypeVariableMemberChain>.Empty;
                }
                var Name = oName.Value;

                var l = new List<TypeSpec>();
                foreach (var p in s.Parameters)
                {
                    var LocalLocalInvalidCharIndex = 0;
                    var ov = TypeParser.TryParseTypeSpec(p.Key, (o, Start, End) => Mark(o, p.Value + Start, p.Value + End), out LocalLocalInvalidCharIndex);
                    if (ov.OnNotHasValue)
                    {
                        InvalidCharIndex = p.Value + LocalLocalInvalidCharIndex;
                        return Optional<TypeVariableMemberChain>.Empty;
                    }
                    l.Add(ov.Value);
                }
                Mark(l, s.NameEndIndex, s.SymbolEndIndex);

                TypeSpec t;
                if (Name.StartsWith("'"))
                {
                    Name = new String(Name.Skip(1).ToArray());
                    t = TypeSpec.CreateGenericParameterRef(Name);
                }
                else
                {
                    var Ref = TypeParser.ParseTypeRef(Name);
                    t = TypeSpec.CreateTypeRef(Ref);
                }
                Mark(t, s.NameStartIndex, s.NameEndIndex);

                var v = VariableRef.CreateName(Name);
                Mark(v, s.NameStartIndex, s.NameEndIndex);

                if (s.Parameters.Count > 0)
                {
                    if (tTotal.OnNotHasValue && String.Equals(Name, "Tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        t = TypeSpec.CreateTuple(l);
                    }
                    else
                    {
                        if (!t.OnTypeRef)
                        {
                            InvalidCharIndex = s.NameStartIndex;
                            return Optional<TypeVariableMemberChain>.Empty;
                        }
                        var gts = new GenericTypeSpec { TypeSpec = t, ParameterValues = l };
                        Mark(gts, s.SymbolStartIndex, s.SymbolEndIndex);
                        t = TypeSpec.CreateGenericTypeSpec(gts);
                    }
                    Mark(t, s.SymbolStartIndex, s.SymbolEndIndex);

                    var gfs = new GenericFunctionSpec { Func = v, Parameters = l };
                    Mark(gfs, s.SymbolStartIndex, s.SymbolEndIndex);
                    v = VariableRef.CreateGenericFunctionSpec(gfs);
                    Mark(v, s.SymbolStartIndex, s.SymbolEndIndex);
                }

                if (tTotal.OnNotHasValue)
                {
                    tTotal = t;
                    var tl = Expr.CreateTypeLiteral(t);
                    tlTotal = tl;
                    Mark(tl, s.SymbolStartIndex, s.SymbolEndIndex);

                    var vv = Expr.CreateVariableRef(v);
                    vTotal = vv;
                    Mark(vv, s.SymbolStartIndex, s.SymbolEndIndex);

                    var al = new List<Expr> { tl, vv };
                    Ambiguous = Expr.CreateAmbiguous(al);
                    Mark(al, s.SymbolStartIndex, s.SymbolEndIndex);
                    Mark(Ambiguous, s.SymbolStartIndex, s.SymbolEndIndex);

                    FirstStart = s.SymbolStartIndex;
                }
                else
                {
                    var tms = new TypeMemberSpec { Parent = tTotal.Value, Child = t };
                    var tt = TypeSpec.CreateMember(tms);
                    tTotal = tt;
                    Mark(tms, FirstStart, s.SymbolEndIndex);
                    Mark(tt, FirstStart, s.SymbolEndIndex);
                    var tl = Expr.CreateTypeLiteral(t);
                    tlTotal = tl;
                    Mark(tl, FirstStart, s.SymbolEndIndex);

                    var ma = new MemberAccess { Parent = Ambiguous, Child = v };
                    var vma = VariableRef.CreateMemberAccess(ma);
                    var vv = Expr.CreateVariableRef(vma);
                    vTotal = vv;
                    Mark(ma, FirstStart, s.SymbolEndIndex);
                    Mark(vma, FirstStart, s.SymbolEndIndex);
                    Mark(vv, FirstStart, s.SymbolEndIndex);

                    var al = new List<Expr> { tl, vv };
                    Ambiguous = Expr.CreateAmbiguous(al);
                    Mark(al, FirstStart, s.SymbolEndIndex);
                    Mark(Ambiguous, FirstStart, s.SymbolEndIndex);
                }
            }

            return new TypeVariableMemberChain { Type = tTotal.Value, TypeLiteral = tlTotal.Value, Variable = vTotal.Value };
        }

        private static Optional<Expr> TryTransformVariableMemberChain(String NodeString, ExprNode Node, Expr Parent, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            Action<Object, int, int> Mark = (o, Start, End) =>
            {
                if (NodePositions.ContainsKey(Node))
                {
                    var Range = NodePositions[Node];
                    var TypeRange = new TextRange { Start = Text.Calc(Range.Start, Start), End = Text.Calc(Range.Start, End) };
                    Positions.Add(o, TypeRange);
                }
            };

            Action<Object, int> MarkEnd = (o, End) =>
            {
                if (Positions.ContainsKey(Parent) && NodePositions.ContainsKey(Node))
                {
                    var Range = NodePositions[Node];
                    var TypeRange = new TextRange { Start = Positions[Parent].Start, End = Text.Calc(Range.Start, End) };
                    Positions.Add(o, TypeRange);
                }
            };

            int InvalidCharIndex;
            var osml = TokenParser.TrySplitSymbolMemberChain(NodeString, out InvalidCharIndex);
            if (osml.OnNotHasValue)
            {
                return Optional<Expr>.Empty;
            }
            var sml = osml.Value;

            var vTotal = Parent;
            foreach (var s in sml)
            {
                var LocalInvalidCharIndex = 0;
                var oName = TokenParser.TryUnescapeSymbolName(s.Name, out LocalInvalidCharIndex);
                if (oName.OnNotHasValue)
                {
                    InvalidCharIndex = s.NameStartIndex + LocalInvalidCharIndex;
                    return Optional<Expr>.Empty;
                }
                var Name = oName.Value;

                var l = new List<TypeSpec>();
                foreach (var p in s.Parameters)
                {
                    var LocalLocalInvalidCharIndex = 0;
                    var ov = TypeParser.TryParseTypeSpec(p.Key, (o, Start, End) => Mark(o, p.Value + Start, p.Value + End), out LocalLocalInvalidCharIndex);
                    if (ov.OnNotHasValue)
                    {
                        InvalidCharIndex = p.Value + LocalLocalInvalidCharIndex;
                        return Optional<Expr>.Empty;
                    }
                    l.Add(ov.Value);
                }
                Mark(l, s.NameEndIndex, s.SymbolEndIndex);

                var v = VariableRef.CreateName(Name);
                Mark(v, s.NameStartIndex, s.NameEndIndex);

                if (s.Parameters.Count > 0)
                {
                    var gfs = new GenericFunctionSpec { Func = v, Parameters = l };
                    Mark(gfs, s.SymbolStartIndex, s.SymbolEndIndex);
                    v = VariableRef.CreateGenericFunctionSpec(gfs);
                    Mark(v, s.SymbolStartIndex, s.SymbolEndIndex);
                }

                var ma = new MemberAccess { Parent = vTotal, Child = v };
                var vma = VariableRef.CreateMemberAccess(ma);
                var vv = Expr.CreateVariableRef(vma);
                vTotal = vv;
                MarkEnd(ma, s.SymbolEndIndex);
                MarkEnd(vma, s.SymbolEndIndex);
                MarkEnd(vv, s.SymbolEndIndex);
            }

            return vTotal;
        }

        private static T Mark<T>(T SemanticsObj, Object SyntaxObj, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (!NodePositions.ContainsKey(SyntaxObj)) { return SemanticsObj; }
            var Range = NodePositions[SyntaxObj];
            Positions.Add(SemanticsObj, Range);
            return SemanticsObj;
        }
        private static T MarkRange<T>(T SemanticsObj, Optional<TextRange> Range, Dictionary<Object, TextRange> Positions)
        {
            if (Range.OnHasValue)
            {
                Positions.Add(SemanticsObj, Range.Value);
            }
            return SemanticsObj;
        }
        private static Optional<TextRange> GetRange<T>(IEnumerable<T> SyntaxObjs, Dictionary<Object, TextRange> NodePositions)
        {
            if (!SyntaxObjs.Any()) { return Optional<TextRange>.Empty; }
            var First = SyntaxObjs.First();
            var Last = SyntaxObjs.Last();
            if (NodePositions.ContainsKey(First) && NodePositions.ContainsKey(Last))
            {
                return new TextRange { Start = NodePositions[First].Start, End = NodePositions[Last].End };
            }
            return Optional<TextRange>.Empty;
        }
    }
}
