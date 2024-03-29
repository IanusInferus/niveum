﻿//==========================================================================
//
//  File:        ExprTransformer.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式转换器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

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
                else if (s == "_")
                {
                    return Mark(Expr.CreateError(), Node, NodePositions, Positions);
                }
                else if (s == "True")
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = new List<String> { "Boolean" }, Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = "True" }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (s == "False")
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = new List<String> { "Boolean" }, Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = "False" }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (TokenParser.IsIntLiteral(s))
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = new List<String> { "Int" }, Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = s }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }
                else if (TokenParser.IsFloatLiteral(s))
                {
                    var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = new List<String> { "Real" }, Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                    var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = s }, Node, NodePositions, Positions);
                    return Mark(Expr.CreatePrimitiveLiteral(ple), Node, NodePositions, Positions);
                }

                var Ambiguous = new List<Expr> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Node, Text, NodePositions, Positions);
                if (otvmc.OnSome)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    Ambiguous.Add(Mark(Expr.CreateTypeLiteral(t), Node, NodePositions, Positions));
                    Ambiguous.Add(Mark(Expr.CreateVariableRef(tvmc.Variable), Node, NodePositions, Positions));

                    if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                    {
                        var tule = Mark(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = Optional<Expr>.Empty }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateTaggedUnionLiteral(tule), Node, NodePositions, Positions));

                        var ele = Mark(new EnumLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Name = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single() }, Node, NodePositions, Positions);
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
                var t = Mark(TypeSpec.CreateTypeRef(Mark(new TypeRef { Name = new List<String> { "String" }, Version = "" }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
                return Mark(Expr.CreatePrimitiveLiteral(Mark(new PrimitiveLiteralExpr { Type = t, Value = Node.Literal }, Node, NodePositions, Positions)), Node, NodePositions, Positions);
            }
            else if (Node.OnOperator)
            {
                var e = Mark(VariableRef.CreateName(Node.Operator), Node, NodePositions, Positions);
                return Mark(Expr.CreateVariableRef(e), Node, NodePositions, Positions);
            }
            else if (Node.OnYieldTemplate)
            {
                return Mark(Expr.CreateYieldTemplate(Node.YieldTemplate), Node, NodePositions, Positions);
            }
            else if (Node.OnStem)
            {
                var Stem = Node.Stem;
                if (Stem.Head.OnSome)
                {
                    var Transformed = TransformStem(Stem.Head.Value, Stem.Nodes, GetRange(Node, NodePositions), GetRange(Stem.Nodes, NodePositions), Text, NodePositions, Positions);
                    return Transformed;
                }
                else
                {
                    var Ambiguous = new List<Expr> { };

                    var Transformed = Stem.Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
                    Mark(Transformed, Node, NodePositions, Positions);
                    Ambiguous.Add(Mark(Expr.CreateSequence(Transformed), Node, NodePositions, Positions));
                    var lle = Mark(new ListLiteralExpr { Type = Optional<TypeSpec>.Empty, Elements = Transformed }, Node, NodePositions, Positions);
                    Ambiguous.Add(Mark(Expr.CreateListLiteral(lle), Node, NodePositions, Positions));
                    if (Transformed.Count >= 1)
                    {
                        var tle = Mark(new TupleLiteralExpr { Type = Optional<TypeSpec>.Empty, Elements = Transformed }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateTupleLiteral(tle), Node, NodePositions, Positions));
                    }
                    var Nodes = Stem.Nodes;
                    var Range = GetRange(Node, NodePositions);
                    var NodesRange = GetRange(Stem.Nodes, NodePositions);
                    if (Nodes.All(Child => (Child.OnUndetermined && (Child.Undetermined.Nodes.Count == 2) && Child.Undetermined.Nodes[0].OnDirect) || (Child.OnStem && Child.Stem.Head.OnSome && (Child.Stem.Nodes.Count == 1))))
                    {
                        var FieldAssigns = new List<FieldAssign>();
                        foreach (var Child in Nodes)
                        {
                            var ChildHead = Child.OnUndetermined ? Child.Undetermined.Nodes[0] : Child.Stem.Head.Value;
                            var ChildNode = Child.OnUndetermined ? Child.Undetermined.Nodes[1] : Child.Stem.Nodes.Single();
                            var oLeftExpr = TryTransformLeftValueDef(ChildHead, Text, NodePositions, Positions);
                            var RightExpr = Transform(ChildNode, Text, NodePositions, Positions);
                            if (oLeftExpr.OnSome)
                            {
                                var LeftExpr = oLeftExpr.Value;
                                if (LeftExpr.OnVariable && LeftExpr.Variable.Type.OnNone)
                                {
                                    var fa = Mark(new FieldAssign { Name = LeftExpr.Variable.Name, Expr = RightExpr }, Child, NodePositions, Positions);
                                    FieldAssigns.Add(fa);
                                }
                                else
                                {
                                    FieldAssigns.Clear();
                                    break;
                                }
                            }
                        }
                        if ((Nodes.Count == 0) || (FieldAssigns.Count > 0))
                        {
                            var rle = MarkRange(new RecordLiteralExpr { Type = Optional<TypeSpec>.Empty, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateRecordLiteral(rle), Range, Positions));
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
            }
            else if (Node.OnUndetermined)
            {
                var Nodes = Node.Undetermined.Nodes;
                var Transformed = TransformNodes(Nodes, GetRange(Nodes, NodePositions), Text, NodePositions, Positions);
                return Transformed;
            }
            else if (Node.OnMember)
            {
                var ParentExpr = Transform(Node.Member.Parent, Text, NodePositions, Positions);
                var Child = Node.Member.Child;
                if (Child.OnDirect)
                {
                    var oe = TryTransformVariableMemberChain(Child.Direct, Child, ParentExpr, Text, NodePositions, Positions);
                    if (oe.OnSome)
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
            return TransformNodes(Nodes, MakeRange(Nodes, NodePositions), Text, NodePositions, Positions);
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
                                var oSecondExpr = TryTransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                                if (oSecondExpr.OnSome)
                                {
                                    var SecondExpr = oSecondExpr.Value;
                                    var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), MakeRange(Nodes.Skip(3), NodePositions), Text, NodePositions, Positions);
                                    var e = MarkRange(new LetExpr { Left = SecondExpr, Right = FourthExpr }, Range, Positions);
                                    return MarkRange(Expr.CreateLet(e), Range, Positions);
                                }
                                else
                                {
                                    return MarkRange(Expr.CreateError(), Range, Positions);
                                }
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
                                var oSecondExpr = TryTransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                                if (oSecondExpr.OnSome)
                                {
                                    var SecondExpr = oSecondExpr.Value;
                                    var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), MakeRange(Nodes.Skip(3), NodePositions), Text, NodePositions, Positions);
                                    var e = MarkRange(new VarExpr { Left = SecondExpr, Right = FourthExpr }, Range, Positions);
                                    return MarkRange(Expr.CreateVar(e), Range, Positions);
                                }
                                else
                                {
                                    return MarkRange(Expr.CreateError(), Range, Positions);
                                }
                            }
                        }
                    }
                    else if (s == "If")
                    {
                        if (Nodes.Count == 2)
                        {
                            var oBranches = TryTransformIfBranchList(Nodes.Last(), Text, NodePositions, Positions);
                            if (oBranches.OnSome)
                            {
                                var Branches = oBranches.Value;
                                var e = MarkRange(new IfExpr { Branches = Branches }, Range, Positions);
                                return MarkRange(Expr.CreateIf(e), Range, Positions);
                            }
                            else
                            {
                                return MarkRange(Expr.CreateError(), Range, Positions);
                            }
                        }
                        else if (Nodes.Count >= 3)
                        {
                            var Branch = TransformIfBranch(Nodes.Skip(1).ToList(), MakeRange(Nodes.Skip(1), NodePositions), Text, NodePositions, Positions);
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
                            var oLastExpr = TryTransformMatchAlternativeList(Nodes.Last(), Text, NodePositions, Positions);
                            if (oLastExpr.OnSome)
                            {
                                var LastExpr = oLastExpr.Value;
                                var e = MarkRange(new MatchExpr { Target = MiddleExpr, Alternatives = LastExpr }, Range, Positions);
                                return MarkRange(Expr.CreateMatch(e), Range, Positions);
                            }
                            else
                            {
                                return MarkRange(Expr.CreateError(), Range, Positions);
                            }
                        }
                    }
                    else if (s == "For")
                    {
                        if (Nodes.Count >= 5)
                        {
                            var oSecondExpr = TryTransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                            if (oSecondExpr.OnSome)
                            {
                                var SecondExpr = oSecondExpr.Value;
                                var MiddleExpr = TransformNodes(Nodes.Skip(3).Take(Nodes.Count - 4).ToList(), Text, NodePositions, Positions);
                                var LastExpr = Transform(Nodes.Last(), Text, NodePositions, Positions);
                                var e = MarkRange(new ForExpr { EnumeratedValue = SecondExpr, Enumerable = MiddleExpr, Body = LastExpr }, Range, Positions);
                                return MarkRange(Expr.CreateFor(e), Range, Positions);
                            }
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
                var Second = Nodes[1];
                if (Second.OnOperator)
                {
                    var s = Second.Operator;
                    if (s == "=")
                    {
                        var oLeftExpr = TryTransformLeftValueRefList(Nodes[0], Text, NodePositions, Positions);
                        if (oLeftExpr.OnSome)
                        {
                            var LeftExpr = oLeftExpr.Value;
                            var RightExpr = TransformNodes(Nodes.Skip(2).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new AssignExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateAssign(e), Range, Positions);
                        }
                        else
                        {
                            return MarkRange(Expr.CreateError(), Range, Positions);
                        }
                    }
                    else if (s == "+=")
                    {
                        var oLeftExpr = TryTransformLeftValueRefList(Nodes[0], Text, NodePositions, Positions);
                        if (oLeftExpr.OnSome)
                        {
                            var LeftExpr = oLeftExpr.Value;
                            var RightExpr = TransformNodes(Nodes.Skip(2).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new IncreaseExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateIncrease(e), Range, Positions);
                        }
                        else
                        {
                            return MarkRange(Expr.CreateError(), Range, Positions);
                        }
                    }
                    else if (s == "-=")
                    {
                        var oLeftExpr = TryTransformLeftValueRefList(Nodes[0], Text, NodePositions, Positions);
                        if (oLeftExpr.OnSome)
                        {
                            var LeftExpr = oLeftExpr.Value;
                            var RightExpr = TransformNodes(Nodes.Skip(2).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new DecreaseExpr { Left = LeftExpr, Right = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateDecrease(e), Range, Positions);
                        }
                        else
                        {
                            return MarkRange(Expr.CreateError(), Range, Positions);
                        }
                    }
                    else if (s == "=>")
                    {
                        var oLeftExpr = TryTransformLeftValueDefList(Nodes[0], Text, NodePositions, Positions);
                        if (oLeftExpr.OnSome)
                        {
                            var LeftExpr = oLeftExpr.Value;
                            var RightExpr = TransformNodes(Nodes.Skip(2).ToList(), Text, NodePositions, Positions);
                            var e = MarkRange(new LambdaExpr { Parameters = LeftExpr, Body = RightExpr }, Range, Positions);
                            return MarkRange(Expr.CreateLambda(e), Range, Positions);
                        }
                        else
                        {
                            return MarkRange(Expr.CreateError(), Range, Positions);
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
                    var l = MarkRange(new List<Expr> { LeftExpr, RightExpr }, MakeRange(Nodes, NodePositions), Positions);
                    var e = MarkRange(new FunctionCallExpr { Func = Operator, Parameters = l }, Range, Positions);
                    return MarkRange(Expr.CreateFunctionCall(e), Range, Positions);
                }
            }
            if (Nodes.Count >= 2)
            {
                var First = Nodes.First();
                var Rest = Nodes.Skip(1).ToList();
                var RestRange = MakeRange(Rest, NodePositions);
                if (Rest.Count == 1)
                {
                    var One = Rest.Single();
                    if (One.OnStem && One.Stem.Head.OnNone && One.Stem.CanMerge)
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
                if (otvmc.OnSome)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    if (Nodes.All(Child => (Child.OnUndetermined && (Child.Undetermined.Nodes.Count == 2) && Child.Undetermined.Nodes[0].OnDirect) || (Child.OnStem && Child.Stem.Head.OnSome && (Child.Stem.Nodes.Count == 1))))
                    {
                        var FieldAssigns = new List<FieldAssign>();
                        foreach (var Child in Nodes)
                        {
                            var ChildHead = Child.OnUndetermined ? Child.Undetermined.Nodes[0] : Child.Stem.Head.Value;
                            var ChildNode = Child.OnUndetermined ? Child.Undetermined.Nodes[1] : Child.Stem.Nodes.Single();
                            var oLeftExpr = TryTransformLeftValueDef(ChildHead, Text, NodePositions, Positions);
                            var RightExpr = Transform(ChildNode, Text, NodePositions, Positions);
                            if (oLeftExpr.OnSome)
                            {
                                var LeftExpr = oLeftExpr.Value;
                                if (LeftExpr.OnVariable && LeftExpr.Variable.Type.OnNone)
                                {
                                    var fa = Mark(new FieldAssign { Name = LeftExpr.Variable.Name, Expr = RightExpr }, Child, NodePositions, Positions);
                                    FieldAssigns.Add(fa);
                                }
                                else
                                {
                                    return MarkRange(Expr.CreateError(), Range, Positions);
                                }
                            }
                        }
                        var rle = MarkRange(new RecordLiteralExpr { Type = t, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                        return MarkRange(Expr.CreateRecordLiteral(rle), Range, Positions);
                    }

                    if (Nodes.Count == 0)
                    {
                        var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = Optional<String>.Empty }, NodesRange, NodePositions, Positions);
                        Ambiguous.Add(MarkRange(Expr.CreatePrimitiveLiteral(ple), Range, Positions));
                        if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                        {
                            var tule = MarkRange(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = Optional<Expr>.Empty }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateTaggedUnionLiteral(tule), Range, Positions));
                        }
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
                    if (Transformed.Count == 1)
                    {
                        var One = Transformed.Single();
                        if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                        {
                            var tule = MarkRange(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = One }, Range, Positions);
                            Ambiguous.Add(MarkRange(Expr.CreateTaggedUnionLiteral(tule), Range, Positions));
                        }
                    }
                    if (Transformed.Count >= 1)
                    {
                        if (t.OnTuple)
                        {
                            var tle = MarkRange(new TupleLiteralExpr { Type = t, Elements = Transformed }, Range, Positions);
                            return MarkRange(Expr.CreateTupleLiteral(tle), Range, Positions);
                        }
                    }
                    {
                        var lle = MarkRange(new ListLiteralExpr { Type = t, Elements = Transformed }, Range, Positions);
                        Ambiguous.Add(MarkRange(Expr.CreateListLiteral(lle), Range, Positions));
                        var v = MarkRange(Expr.CreateVariableRef(tvmc.Variable), Range, Positions);
                        var fce = MarkRange(new FunctionCallExpr { Func = v, Parameters = Transformed }, Range, Positions);
                        Ambiguous.Add(MarkRange(Expr.CreateFunctionCall(fce), Range, Positions));
                        var ia = MarkRange(new IndexerAccess { Expr = v, Index = Transformed }, Range, Positions);
                        var vr = MarkRange(VariableRef.CreateIndexerAccess(ia), Range, Positions);
                        Ambiguous.Add(MarkRange(Expr.CreateVariableRef(vr), Range, Positions));
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

        private static Optional<List<LeftValueDef>> TryTransformLeftValueDefList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<LeftValueDef>();
            if (Node.OnStem)
            {
                foreach (var Child in Node.Stem.Nodes)
                {
                    var olvd = TryTransformLeftValueDef(Child, Text, NodePositions, Positions);
                    if (olvd.OnNone)
                    {
                        return Optional<List<LeftValueDef>>.Empty;
                    }
                    l.Add(olvd.Value);
                }
            }
            else
            {
                var olvd = TryTransformLeftValueDef(Node, Text, NodePositions, Positions);
                if (olvd.OnNone)
                {
                    return Optional<List<LeftValueDef>>.Empty;
                }
                l.Add(olvd.Value);
            }
            return Mark(l, Node, NodePositions, Positions);
        }

        private static Optional<LeftValueDef> TryTransformLeftValueDef(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Node.OnDirect)
            {
                var s = Node.Direct;
                var Parts = s.Split(new Char[] { ':' }, 2);
                int InvalidCharIndex;
                var ol = TokenParser.TrySplitSymbolMemberChain(Parts[0], out InvalidCharIndex);
                if (ol.OnNone) { return Optional<LeftValueDef>.Empty; }
                var l = ol.Value;
                if (l.Count != 1) { return Optional<LeftValueDef>.Empty; }
                var One = l.Single();
                if (One.Parameters.Count != 0) { return Optional<LeftValueDef>.Empty; }
                var Name = One.Name;
                var Type = Optional<TypeSpec>.Empty;
                if (Parts.Length == 2)
                {
                    var oType = TypeParser.TryParseTypeSpec(Parts[1], (o, Start, End) =>
                    {
                        if (NodePositions.ContainsKey(Node))
                        {
                            var Range = NodePositions[Node];
                            MarkRange(o, new TextRange { Start = Text.Calc(Range.Start, Parts[0].Length + 1 + Start), End = Text.Calc(Range.Start, Parts[0].Length + 1 + End) }, Positions);
                        }
                    }, out InvalidCharIndex);
                    if (oType.OnNone) { return Optional<LeftValueDef>.Empty; }
                    Type = oType.Value;
                }
                if (Parts[0] == "_")
                {
                    return Mark(LeftValueDef.CreateIgnore(Type), Node, NodePositions, Positions);
                }
                else
                {
                    var v = Mark(new LocalVariableDef { Name = Name, Type = Type }, Node, NodePositions, Positions);
                    return Mark(LeftValueDef.CreateVariable(v), Node, NodePositions, Positions);
                }
            }
            else
            {
                return Optional<LeftValueDef>.Empty;
            }
        }

        private static Optional<List<LeftValueRef>> TryTransformLeftValueRefList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<LeftValueRef>();
            if (Node.OnStem)
            {
                foreach (var Child in Node.Stem.Nodes)
                {
                    var olvd = TryTransformLeftValueRef(Child, Text, NodePositions, Positions);
                    if (olvd.OnNone)
                    {
                        return Optional<List<LeftValueRef>>.Empty;
                    }
                    l.Add(olvd.Value);
                }
            }
            else
            {
                var olvd = TryTransformLeftValueRef(Node, Text, NodePositions, Positions);
                if (olvd.OnNone)
                {
                    return Optional<List<LeftValueRef>>.Empty;
                }
                l.Add(olvd.Value);
            }
            return Mark(l, Node, NodePositions, Positions);
        }
        private static Optional<LeftValueRef> TryTransformLeftValueRef(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Node.OnDirect)
            {
                var s = Node.Direct;
                if (s == "_")
                {
                    return Mark(LeftValueRef.CreateIgnore(), Node, NodePositions, Positions);
                }
            }

            var Transformed = Transform(Node, Text, NodePositions, Positions);
            if (Transformed.OnVariableRef)
            {
                return Mark(LeftValueRef.CreateVariable(Transformed.VariableRef), Node, NodePositions, Positions);
            }
            else if (Transformed.OnAmbiguous)
            {
                var VariableRefs = Transformed.Ambiguous.Where(a => a.OnVariableRef).ToList();
                if (VariableRefs.Count == 1)
                {
                    return Mark(LeftValueRef.CreateVariable(VariableRefs.Single().VariableRef), Node, NodePositions, Positions);
                }
            }
            return Optional<LeftValueRef>.Empty;
        }

        private static Optional<List<IfBranch>> TryTransformIfBranchList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (!Node.OnStem) { return Optional<List<IfBranch>>.Empty; }
            if (Node.Stem.Head.OnSome) { return Optional<List<IfBranch>>.Empty; }
            var Branches = new List<IfBranch>();
            foreach (var BranchNode in Node.Stem.Nodes)
            {
                if (!BranchNode.OnUndetermined || (BranchNode.Undetermined.Nodes.Count < 2)) { return Optional<List<IfBranch>>.Empty; }
                Branches.Add(TransformIfBranch(BranchNode.Undetermined.Nodes, GetRange(BranchNode, NodePositions), Text, NodePositions, Positions));
            }
            return Mark(Branches, Node, NodePositions, Positions);
        }
        private static IfBranch TransformIfBranch(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var ConditionNodes = Nodes.Take(Nodes.Count - 1).ToList();
            var ContentNode = Nodes.Last();
            var ConditionExpr = TransformNodes(ConditionNodes, Text, NodePositions, Positions);
            var ContentExpr = Transform(ContentNode, Text, NodePositions, Positions);
            return MarkRange(new IfBranch { Condition = ConditionExpr, Expr = ContentExpr }, Range, Positions);
        }

        private static Optional<List<MatchAlternative>> TryTransformMatchAlternativeList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (!Node.OnStem) { return Optional<List<MatchAlternative>>.Empty; }
            if (Node.Stem.Head.OnSome) { return Optional<List<MatchAlternative>>.Empty; }
            var Alternatives = new List<MatchAlternative>();
            foreach (var AlternativeNode in Node.Stem.Nodes)
            {
                if (!AlternativeNode.OnUndetermined) { return Optional<List<MatchAlternative>>.Empty; }
                var oa = TryTransformMatchAlternative(AlternativeNode.Undetermined.Nodes, GetRange(AlternativeNode, NodePositions), Text, NodePositions, Positions);
                if (oa.OnNone) { return Optional<List<MatchAlternative>>.Empty; }
                Alternatives.Add(oa.Value);
            }
            return Mark(Alternatives, Node, NodePositions, Positions);
        }
        private static Optional<MatchAlternative> TryTransformMatchAlternative(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count < 2) { return Optional<MatchAlternative>.Empty; }
            var PatternNode = Nodes.First();
            var ConditionNodes = Optional<List<ExprNode>>.Empty;
            var ContentNode = Nodes.Last();

            if (Nodes[1].OnDirect && Nodes[1].Direct == "Where")
            {
                if (Nodes.Count < 4) { return Optional<MatchAlternative>.Empty; }
                ConditionNodes = Nodes.Skip(2).Take(Nodes.Count - 3).ToList();
            }
            else
            {
                if (Nodes.Count != 2) { return Optional<MatchAlternative>.Empty; }
            }

            var Pattern = TransformPattern(PatternNode, Text, NodePositions, Positions);
            var ConditionExpr = ConditionNodes.OnSome ? TransformNodes(ConditionNodes.Value, Text, NodePositions, Positions) : Optional<Expr>.Empty;
            var ContentExpr = Transform(ContentNode, Text, NodePositions, Positions);
            return MarkRange(new MatchAlternative { Pattern = Pattern, Condition = ConditionExpr, Expr = ContentExpr }, Range, Positions);
        }

        public static MatchPattern TransformPattern(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Node.OnDirect)
            {
                var s = Node.Direct;

                if (s == "Null")
                {
                    return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
                }
                else if (s == "Default")
                {
                    return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
                }
                else if (s == "This")
                {
                    var e = Mark(VariableRef.CreateThis(), Node, NodePositions, Positions);
                    return Mark(MatchPattern.CreateVariableRef(e), Node, NodePositions, Positions);
                }
                else if (s == "_")
                {
                    return Mark(MatchPattern.CreateIgnore(), Node, NodePositions, Positions);
                }
                else if ((s == "True") || (s == "False") || TokenParser.IsIntLiteral(s) || TokenParser.IsFloatLiteral(s))
                {
                    var e = Transform(Node, Text, NodePositions, Positions);
                    if (e.OnPrimitiveLiteral)
                    {
                        return Mark(MatchPattern.CreatePrimitiveLiteral(e.PrimitiveLiteral), Node, NodePositions, Positions);
                    }
                    else
                    {
                        return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
                    }
                }

                var Ambiguous = new List<MatchPattern> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Node, Text, NodePositions, Positions);
                if (otvmc.OnSome)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    Ambiguous.Add(Mark(MatchPattern.CreateVariableRef(tvmc.Variable), Node, NodePositions, Positions));

                    if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                    {
                        var tule = Mark(new TaggedUnionLiteralPattern { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = Optional<MatchPattern>.Empty }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(MatchPattern.CreateTaggedUnionLiteral(tule), Node, NodePositions, Positions));

                        var ele = Mark(new EnumLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Name = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single() }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(MatchPattern.CreateEnumLiteral(ele), Node, NodePositions, Positions));
                    }
                }

                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
                    Mark(Ambiguous, Node, NodePositions, Positions);
                    return Mark(MatchPattern.CreateAmbiguous(Ambiguous), Node, NodePositions, Positions);
                }
            }
            else if (Node.OnLiteral)
            {
                var e = Transform(Node, Text, NodePositions, Positions);
                if (e.OnPrimitiveLiteral)
                {
                    return Mark(MatchPattern.CreatePrimitiveLiteral(e.PrimitiveLiteral), Node, NodePositions, Positions);
                }
                else
                {
                    return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
                }
            }
            else if (Node.OnOperator || Node.OnMember)
            {
                var e = Transform(Node, Text, NodePositions, Positions);
                if (e.OnVariableRef)
                {
                    return Mark(MatchPattern.CreateVariableRef(e.VariableRef), Node, NodePositions, Positions);
                }
                else
                {
                    return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
                }
            }
            else if (Node.OnStem)
            {
                var Stem = Node.Stem;
                if (Stem.Head.OnSome)
                {
                    var Transformed = TransformStemPattern(Stem.Head.Value, Stem.Nodes, GetRange(Node, NodePositions), GetRange(Stem.Nodes, NodePositions), Text, NodePositions, Positions);
                    return Transformed;
                }
                else
                {
                    var Ambiguous = new List<MatchPattern> { };

                    var Transformed = Stem.Nodes.Select(n => TransformPattern(n, Text, NodePositions, Positions)).ToList();
                    Mark(Transformed, Node, NodePositions, Positions);
                    var lle = Mark(new ListLiteralPattern { Type = Optional<TypeSpec>.Empty, Elements = Transformed }, Node, NodePositions, Positions);
                    Ambiguous.Add(Mark(MatchPattern.CreateListLiteral(lle), Node, NodePositions, Positions));
                    if (Transformed.Count >= 1)
                    {
                        var tle = Mark(new TupleLiteralPattern { Type = Optional<TypeSpec>.Empty, Elements = Transformed }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(MatchPattern.CreateTupleLiteral(tle), Node, NodePositions, Positions));
                    }
                    var Nodes = Stem.Nodes;
                    var Range = GetRange(Node, NodePositions);
                    var NodesRange = GetRange(Stem.Nodes, NodePositions);
                    if (Nodes.All(Child => (Child.OnUndetermined && (Child.Undetermined.Nodes.Count == 2) && Child.Undetermined.Nodes[0].OnDirect) || (Child.OnStem && Child.Stem.Head.OnSome && (Child.Stem.Nodes.Count == 1))))
                    {
                        var FieldAssigns = new List<FieldAssignPattern>();
                        foreach (var Child in Nodes)
                        {
                            var ChildHead = Child.OnUndetermined ? Child.Undetermined.Nodes[0] : Child.Stem.Head.Value;
                            var ChildNode = Child.OnUndetermined ? Child.Undetermined.Nodes[1] : Child.Stem.Nodes.Single();
                            var oLeftExpr = TryTransformLeftValueDef(ChildHead, Text, NodePositions, Positions);
                            var RightExpr = TransformPattern(ChildNode, Text, NodePositions, Positions);
                            if (oLeftExpr.OnSome)
                            {
                                var LeftExpr = oLeftExpr.Value;
                                if (LeftExpr.OnVariable && LeftExpr.Variable.Type.OnNone)
                                {
                                    var fa = Mark(new FieldAssignPattern { Name = LeftExpr.Variable.Name, Expr = RightExpr }, Child, NodePositions, Positions);
                                    FieldAssigns.Add(fa);
                                }
                                else
                                {
                                    FieldAssigns.Clear();
                                    break;
                                }
                            }
                        }
                        if ((Nodes.Count == 0) || (FieldAssigns.Count > 0))
                        {
                            var rle = MarkRange(new RecordLiteralPattern { Type = Optional<TypeSpec>.Empty, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                            Ambiguous.Add(MarkRange(MatchPattern.CreateRecordLiteral(rle), Range, Positions));
                        }
                    }

                    if (Ambiguous.Count == 1)
                    {
                        return Ambiguous.Single();
                    }
                    else if (Ambiguous.Count > 1)
                    {
                        Mark(Ambiguous, Node, NodePositions, Positions);
                        return Mark(MatchPattern.CreateAmbiguous(Ambiguous), Node, NodePositions, Positions);
                    }
                }
            }
            else if (Node.OnUndetermined)
            {
                var Nodes = Node.Undetermined.Nodes;
                var Transformed = TransformNodesPattern(Nodes, GetRange(Nodes, NodePositions), Text, NodePositions, Positions);
                return Transformed;
            }
            else
            {
                throw new InvalidOperationException();
            }

            return Mark(MatchPattern.CreateError(), Node, NodePositions, Positions);
        }

        private static MatchPattern TransformNodesPattern(List<ExprNode> Nodes, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            return TransformNodesPattern(Nodes, MakeRange(Nodes, NodePositions), Text, NodePositions, Positions);
        }
        private static MatchPattern TransformNodesPattern(List<ExprNode> Nodes, Optional<TextRange> Range, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count == 2)
            {
                var First = Nodes.First();
                if (First.OnDirect)
                {
                    var s = First.Direct;
                    if (s == "Let")
                    {
                        var oSecondExpr = TryTransformLeftValueDef(Nodes[1], Text, NodePositions, Positions);
                        if (oSecondExpr.OnSome)
                        {
                            var SecondExpr = oSecondExpr.Value;
                            return MarkRange(MatchPattern.CreateLet(SecondExpr), Range, Positions);
                        }
                        else
                        {
                            return MarkRange(MatchPattern.CreateError(), Range, Positions);
                        }
                    }
                }
            }
            else if (Nodes.Count == 1)
            {
                return TransformPattern(Nodes.Single(), Text, NodePositions, Positions);
            }

            return MarkRange(MatchPattern.CreateError(), Range, Positions);
        }

        private static MatchPattern TransformStemPattern(ExprNode Head, List<ExprNode> Nodes, Optional<TextRange> Range, Optional<TextRange> NodesRange, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var Transformed = Nodes.Select(n => TransformPattern(n, Text, NodePositions, Positions)).ToList();
            MarkRange(Transformed, NodesRange, Positions);

            if (Head.OnDirect)
            {
                var s = Head.Direct;

                var Ambiguous = new List<MatchPattern> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Head, Text, NodePositions, Positions);
                if (otvmc.OnSome)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    if (Nodes.All(Child => (Child.OnUndetermined && (Child.Undetermined.Nodes.Count == 2) && Child.Undetermined.Nodes[0].OnDirect) || (Child.OnStem && Child.Stem.Head.OnSome && (Child.Stem.Nodes.Count == 1))))
                    {
                        var FieldAssigns = new List<FieldAssignPattern>();
                        foreach (var Child in Nodes)
                        {
                            var ChildHead = Child.OnUndetermined ? Child.Undetermined.Nodes[0] : Child.Stem.Head.Value;
                            var ChildNode = Child.OnUndetermined ? Child.Undetermined.Nodes[1] : Child.Stem.Nodes.Single();
                            var oLeftExpr = TryTransformLeftValueDef(ChildHead, Text, NodePositions, Positions);
                            var RightExpr = TransformPattern(ChildNode, Text, NodePositions, Positions);
                            if (oLeftExpr.OnSome)
                            {
                                var LeftExpr = oLeftExpr.Value;
                                if (LeftExpr.OnVariable && LeftExpr.Variable.Type.OnNone)
                                {
                                    var fa = Mark(new FieldAssignPattern { Name = LeftExpr.Variable.Name, Expr = RightExpr }, Child, NodePositions, Positions);
                                    FieldAssigns.Add(fa);
                                }
                                else
                                {
                                    return MarkRange(MatchPattern.CreateError(), Range, Positions);
                                }
                            }
                        }
                        var rle = MarkRange(new RecordLiteralPattern { Type = t, FieldAssigns = MarkRange(FieldAssigns, NodesRange, Positions) }, Range, Positions);
                        return MarkRange(MatchPattern.CreateRecordLiteral(rle), Range, Positions);
                    }

                    if (Nodes.Count == 0)
                    {
                        var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = Optional<String>.Empty }, NodesRange, NodePositions, Positions);
                        Ambiguous.Add(MarkRange(MatchPattern.CreatePrimitiveLiteral(ple), Range, Positions));
                        if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                        {
                            var tule = MarkRange(new TaggedUnionLiteralPattern { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = Optional<MatchPattern>.Empty }, Range, Positions);
                            Ambiguous.Add(MarkRange(MatchPattern.CreateTaggedUnionLiteral(tule), Range, Positions));
                        }
                    }
                    else if (Nodes.Count == 1)
                    {
                        var One = Nodes.Single();
                        if (One.OnDirect || One.OnLiteral || One.OnOperator)
                        {
                            var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = One.OnDirect ? One.Direct : One.OnLiteral ? One.Literal : One.OnOperator ? One.Operator : "" }, NodesRange, NodePositions, Positions);
                            Ambiguous.Add(MarkRange(MatchPattern.CreatePrimitiveLiteral(ple), Range, Positions));
                        }
                    }
                    if (Transformed.Count == 1)
                    {
                        var One = Transformed.Single();
                        if ((t.OnTypeRef && (t.TypeRef.Name.Count == 1) && (t.TypeRef.Version == "")) || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Name.Count == 1) && (t.Member.Child.TypeRef.Version == "")))
                        {
                            var tule = MarkRange(new TaggedUnionLiteralPattern { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name.Single() : t.TypeRef.Name.Single(), Expr = One }, Range, Positions);
                            Ambiguous.Add(MarkRange(MatchPattern.CreateTaggedUnionLiteral(tule), Range, Positions));
                        }
                    }
                    if (Transformed.Count >= 1)
                    {
                        if (t.OnTuple)
                        {
                            var tle = MarkRange(new TupleLiteralPattern { Type = t, Elements = Transformed }, Range, Positions);
                            return MarkRange(MatchPattern.CreateTupleLiteral(tle), Range, Positions);
                        }
                    }
                    {
                        var lle = MarkRange(new ListLiteralPattern { Type = t, Elements = Transformed }, Range, Positions);
                        Ambiguous.Add(MarkRange(MatchPattern.CreateListLiteral(lle), Range, Positions));
                        var v = MarkRange(Expr.CreateVariableRef(tvmc.Variable), Range, Positions);
                        var TransformedExpr = Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
                        MarkRange(TransformedExpr, NodesRange, Positions);
                        var ia = MarkRange(new IndexerAccess { Expr = v, Index = TransformedExpr }, Range, Positions);
                        var vr = MarkRange(VariableRef.CreateIndexerAccess(ia), Range, Positions);
                        Ambiguous.Add(MarkRange(MatchPattern.CreateVariableRef(vr), Range, Positions));
                    }
                }

                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
                    MarkRange(Ambiguous, Range, Positions);
                    return MarkRange(MatchPattern.CreateAmbiguous(Ambiguous), Range, Positions);
                }
            }
            else
            {
                var HeadExpr = Transform(Head, Text, NodePositions, Positions);

                var TransformedExpr = Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
                MarkRange(TransformedExpr, NodesRange, Positions);
                var ia = MarkRange(new IndexerAccess { Expr = HeadExpr, Index = TransformedExpr }, Range, Positions);
                var vr = MarkRange(VariableRef.CreateIndexerAccess(ia), Range, Positions);
                return MarkRange(MatchPattern.CreateVariableRef(vr), Range, Positions);
            }

            return MarkRange(MatchPattern.CreateError(), Range, Positions);
        }

        private sealed class TypeVariableMemberChain
        {
            public TypeSpec Type { get; init; }
            public VariableRef Variable { get; init; }
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
            if (osml.OnNone)
            {
                return Optional<TypeVariableMemberChain>.Empty;
            }
            var sml = osml.Value;

            var tTotal = Optional<TypeSpec>.Empty;
            var vTotal = Optional<VariableRef>.Empty;
            var Ambiguous = Optional<Expr>.Empty;
            var FirstStart = 0;
            foreach (var s in sml)
            {
                var LocalInvalidCharIndex = 0;
                var oName = TokenParser.TryUnescapeSymbolName(s.Name, out LocalInvalidCharIndex);
                if (oName.OnNone)
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
                    if (ov.OnNone)
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
                    String Version;
                    TypeParser.ParseNameAndVersion(Name, out Name, out Version);
                    var NameList = new List<String> { Name };
                    var Ref = new TypeRef { Name = NameList, Version = Version };
                    Mark(NameList, s.NameStartIndex, s.NameStartIndex + Name.Length);
                    Mark(Ref, s.NameStartIndex, s.NameEndIndex);
                    t = TypeSpec.CreateTypeRef(Ref);
                }
                Mark(t, s.NameStartIndex, s.NameEndIndex);

                var v = VariableRef.CreateName(Name);
                Mark(v, s.NameStartIndex, s.NameEndIndex);

                if (s.Parameters.Count > 0)
                {
                    if (tTotal.OnNone && String.Equals(Name, "Tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        t = TypeSpec.CreateTuple(l);
                    }
                    else if (tTotal.OnNone && String.Equals(Name, "Array", StringComparison.OrdinalIgnoreCase) && (l.Count == 1))
                    {
                        t = TypeSpec.CreateArray(l.Single());
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

                if (tTotal.OnNone)
                {
                    tTotal = t;
                    var tl = Expr.CreateTypeLiteral(t);
                    Mark(tl, s.SymbolStartIndex, s.SymbolEndIndex);

                    var vv = Expr.CreateVariableRef(v);
                    vTotal = v;
                    Mark(vv, s.SymbolStartIndex, s.SymbolEndIndex);

                    var al = new List<Expr> { tl, vv };
                    Ambiguous = Expr.CreateAmbiguous(al);
                    Mark(al, s.SymbolStartIndex, s.SymbolEndIndex);
                    Mark(Ambiguous.Value, s.SymbolStartIndex, s.SymbolEndIndex);

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
                    Mark(tl, FirstStart, s.SymbolEndIndex);

                    var ma = new MemberAccess { Parent = Ambiguous.Value, Child = v };
                    var vma = VariableRef.CreateMemberAccess(ma);
                    var vv = Expr.CreateVariableRef(vma);
                    vTotal = vma;
                    Mark(ma, FirstStart, s.SymbolEndIndex);
                    Mark(vma, FirstStart, s.SymbolEndIndex);
                    Mark(vv, FirstStart, s.SymbolEndIndex);

                    var al = new List<Expr> { tl, vv };
                    Ambiguous = Expr.CreateAmbiguous(al);
                    Mark(al, FirstStart, s.SymbolEndIndex);
                    Mark(Ambiguous.Value, FirstStart, s.SymbolEndIndex);
                }
            }

            return new TypeVariableMemberChain { Type = tTotal.Value, Variable = vTotal.Value };
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
            if (osml.OnNone)
            {
                return Optional<Expr>.Empty;
            }
            var sml = osml.Value;

            var vTotal = Parent;
            foreach (var s in sml)
            {
                var LocalInvalidCharIndex = 0;
                var oName = TokenParser.TryUnescapeSymbolName(s.Name, out LocalInvalidCharIndex);
                if (oName.OnNone)
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
                    if (ov.OnNone)
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
            if (SemanticsObj == null) { throw new ArgumentNullException(nameof(SemanticsObj)); }
            if (SyntaxObj == null) { throw new ArgumentNullException(nameof(SyntaxObj)); }

            if (!NodePositions.ContainsKey(SyntaxObj)) { return SemanticsObj; }
            var Range = NodePositions[SyntaxObj];
            Positions.Add(SemanticsObj, Range);
            return SemanticsObj;
        }
        private static T MarkRange<T>(T SemanticsObj, Optional<TextRange> Range, Dictionary<Object, TextRange> Positions)
        {
            if (SemanticsObj == null) { throw new ArgumentNullException(nameof(SemanticsObj)); }

            if (Range.OnSome)
            {
                Positions.Add(SemanticsObj, Range.Value);
            }
            return SemanticsObj;
        }
        private static Optional<TextRange> GetRange<T>(T SyntaxObj, Dictionary<Object, TextRange> NodePositions)
        {
            if (SyntaxObj == null) { throw new ArgumentNullException(nameof(SyntaxObj)); }

            if (NodePositions.ContainsKey(SyntaxObj))
            {
                return NodePositions[SyntaxObj];
            }
            return Optional<TextRange>.Empty;
        }
        private static Optional<TextRange> MakeRange<T>(IEnumerable<T> SyntaxObjs, Dictionary<Object, TextRange> NodePositions)
        {
            if (!SyntaxObjs.Any()) { return Optional<TextRange>.Empty; }
            var First = SyntaxObjs.First();
            var Last = SyntaxObjs.Last();
            if (First == null) { throw new ArgumentNullException(nameof(SyntaxObjs)); }
            if (Last == null) { throw new ArgumentNullException(nameof(SyntaxObjs)); }
            if (NodePositions.ContainsKey(First) && NodePositions.ContainsKey(Last))
            {
                return new TextRange { Start = NodePositions[First].Start, End = NodePositions[Last].End };
            }
            return Optional<TextRange>.Empty;
        }
    }
}
