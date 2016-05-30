//==========================================================================
//
//  File:        ExprTransformer.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式转换器
//  Version:     2016.05.31.
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

                //TODO 数字等

                var Ambiguous = new List<Expr> { };

                var otvmc = TryTransformTypeVariableMemberChain(s, Node, Text, NodePositions, Positions);
                if (otvmc.OnHasValue)
                {
                    var tvmc = otvmc.Value;
                    var t = tvmc.Type;

                    Ambiguous.Add(Mark(Expr.CreateTypeLiteral(t), Node, NodePositions, Positions));

                    if (t.OnTypeRef || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Version == "")))
                    {
                        var tule = Mark(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name, Expr = Optional<Expr>.Empty }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateTaggedUnionLiteral(tule), Node, NodePositions, Positions));

                        var ele = Mark(new EnumLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Name = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name }, Node, NodePositions, Positions);
                        Ambiguous.Add(Mark(Expr.CreateEnumLiteral(ele), Node, NodePositions, Positions));
                    }
                }

                //TODO

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
                //TODO
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
                    var Undetermined = new ExprNodeUndetermined { Nodes = Stem.Nodes };
                    var e = ExprNode.CreateUndetermined(Undetermined);
                    if (NodePositions.ContainsKey(Stem.Nodes))
                    {
                        var Range = NodePositions[Stem.Nodes];
                        NodePositions.Add(Undetermined, Range);
                        NodePositions.Add(e, Range);
                    }

                    var l = new List<ExprNode> { Stem.Head.Value, e };
                    var Transformed = TransformNodes(l, Node, Text, NodePositions, Positions);
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
                var Transformed = TransformNodes(Nodes, Node, Text, NodePositions, Positions);
                return Transformed;
            }

            //TODO
            return Expr.CreateNull();
        }

        private static Expr TransformNodes(List<ExprNode> Nodes, ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (Nodes.Count == 0)
            {
                return Mark(Expr.CreateSequence(Mark(new List<Expr> { }, Nodes, Node, NodePositions, Positions)), Nodes, Node, NodePositions, Positions);
            }
            if (Nodes.Count == 2)
            {
                var First = Nodes.First();
                var Second = Nodes[1];
                if (First.OnDirect)
                {
                    var s = First.Direct;
                    if (s == "Yield")
                    {
                        return Mark(Expr.CreateYield(Transform(Second, Text, NodePositions, Positions)), Nodes, Node, NodePositions, Positions);
                    }
                    else if (s == "YieldMany")
                    {
                        return Mark(Expr.CreateYieldMany(Transform(Second, Text, NodePositions, Positions)), Nodes, Node, NodePositions, Positions);
                    }
                    else if (s == "Throw")
                    {
                        return Mark(Expr.CreateThrow(Transform(Second, Text, NodePositions, Positions)), Nodes, Node, NodePositions, Positions);
                    }
                    else if (s == "Continue")
                    {
                        if (Second.OnDirect)
                        {
                            var i = 0;
                            if (int.TryParse(Second.Direct, out i))
                            {
                                return Mark(Expr.CreateContinue(i), Nodes, Node, NodePositions, Positions);
                            }
                        }
                    }
                    else if (s == "Break")
                    {
                        if (Second.OnDirect)
                        {
                            var i = 0;
                            if (int.TryParse(Second.Direct, out i))
                            {
                                return Mark(Expr.CreateBreak(i), Nodes, Node, NodePositions, Positions);
                            }
                        }
                    }
                    else if (s == "Return")
                    {
                        return Mark(Expr.CreateReturn(Transform(Second, Text, NodePositions, Positions)), Nodes, Node, NodePositions, Positions);
                    }
                    else if (s == "Cast")
                    {
                        //TODO
                    }

                    var Ambiguous = new List<Expr> { };

                    var otvmc = TryTransformTypeVariableMemberChain(s, First, Text, NodePositions, Positions);
                    if (otvmc.OnHasValue)
                    {
                        var tvmc = otvmc.Value;
                        var t = tvmc.Type;

                        if (Second.OnDirect || Second.OnLiteral || Second.OnOperator)
                        {
                            var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = Second.OnDirect ? Second.Direct : Second.OnLiteral ? Second.Literal : Second.OnOperator ? Second.Operator : "" }, Nodes, Node, NodePositions, Positions);
                            Ambiguous.Add(Mark(Expr.CreatePrimitiveLiteral(ple), Nodes, Node, NodePositions, Positions));
                        }
                        if (Second.OnDirect || Second.OnLiteral || Second.OnStem || Second.OnUndetermined)
                        {
                            var SecondExpr = Transform(Second, Text, NodePositions, Positions);

                            var FieldAssigns = new List<FieldAssign>();
                            var FieldAssignRanges = new Dictionary<FieldAssign, TextRange>();
                            if (SecondExpr.OnAssign)
                            {
                                var a = SecondExpr.Assign;
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
                                }
                            }
                            else if (SecondExpr.OnSequence)
                            {
                                foreach (var e in SecondExpr.Sequence)
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
                                var rle = Mark(new RecordLiteralExpr { Type = t, FieldAssigns = Mark(FieldAssigns, Second, NodePositions, Positions) }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateRecordLiteral(rle), Nodes, Node, NodePositions, Positions));
                            }
                            else if (SecondExpr.OnSequence)
                            {
                                if (SecondExpr.Sequence.Count == 0)
                                {
                                    var rle = Mark(new RecordLiteralExpr { Type = t, FieldAssigns = Mark(FieldAssigns, Second, NodePositions, Positions) }, Nodes, Node, NodePositions, Positions);
                                    Ambiguous.Add(Mark(Expr.CreateRecordLiteral(rle), Nodes, Node, NodePositions, Positions));
                                }
                                if (SecondExpr.Sequence.Count >= 2)
                                {
                                    if (t.OnTuple)
                                    {
                                        var tle = Mark(new TupleLiteralExpr { Type = t, Parameters = SecondExpr.Sequence }, Nodes, Node, NodePositions, Positions);
                                        return Mark(Expr.CreateTupleLiteral(tle), Nodes, Node, NodePositions, Positions);
                                    }
                                    else if (t.OnTypeRef && (t.TypeRef.Name == "Tuple") && (t.TypeRef.Version == ""))
                                    {
                                        var tle = Mark(new TupleLiteralExpr { Type = Optional<TypeSpec>.Empty, Parameters = SecondExpr.Sequence }, Nodes, Node, NodePositions, Positions);
                                        Ambiguous.Add(Mark(Expr.CreateTupleLiteral(tle), Nodes, Node, NodePositions, Positions));
                                    }
                                }
                                var lle = Mark(new ListLiteralExpr { Type = t, Parameters = SecondExpr.Sequence }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateListLiteral(lle), Nodes, Node, NodePositions, Positions));
                                var fce = Mark(new FunctionCallExpr { Func = tvmc.Variable, Parameters = SecondExpr.Sequence }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateFunctionCall(fce), Nodes, Node, NodePositions, Positions));
                            }
                            else
                            {
                                var Parameters = Mark(new List<Expr> { SecondExpr }, Second, NodePositions, Positions);
                                var lle = Mark(new ListLiteralExpr { Type = t, Parameters = Parameters }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateListLiteral(lle), Nodes, Node, NodePositions, Positions));
                                var fce = Mark(new FunctionCallExpr { Func = tvmc.Variable, Parameters = Parameters }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateFunctionCall(fce), Nodes, Node, NodePositions, Positions));
                            }

                            if (t.OnTypeRef || (t.OnMember && t.Member.Child.OnTypeRef && (t.Member.Child.TypeRef.Version == "")))
                            {
                                var tule = Mark(new TaggedUnionLiteralExpr { Type = t.OnMember ? t.Member.Child : Optional<TypeSpec>.Empty, Alternative = t.OnMember ? t.Member.Child.TypeRef.Name : t.TypeRef.Name, Expr = SecondExpr }, Nodes, Node, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateTaggedUnionLiteral(tule), Nodes, Node, NodePositions, Positions));
                            }
                        }
                    }

                    if (Ambiguous.Count == 1)
                    {
                        return Ambiguous.Single();
                    }
                    else if (Ambiguous.Count > 1)
                    {
                        Mark(Ambiguous, Nodes, Node, NodePositions, Positions);
                        return Mark(Expr.CreateAmbiguous(Ambiguous), Nodes, Node, NodePositions, Positions);
                    }
                }
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
                                var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), Node, Text, NodePositions, Positions);
                                var e = Mark(new LetExpr { Left = SecondExpr, Right = FourthExpr }, Nodes, Node, NodePositions, Positions);
                                return Mark(Expr.CreateLet(e), Nodes, Node, NodePositions, Positions);
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
                                var FourthExpr = Nodes.Count == 4 ? Transform(Nodes[3], Text, NodePositions, Positions) : TransformNodes(Nodes.Skip(3).ToList(), Node, Text, NodePositions, Positions);
                                var e = Mark(new VarExpr { Left = SecondExpr, Right = FourthExpr }, Nodes, Node, NodePositions, Positions);
                                return Mark(Expr.CreateVar(e), Nodes, Node, NodePositions, Positions);
                            }
                        }
                    }
                    else if (s == "If")
                    {
                        if (Nodes.Count == 2)
                        {
                            var Branches = TransformIfBranchList(Nodes.Last(), Text, NodePositions, Positions);
                            var e = Mark(new IfExpr { Branches = Branches }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateIf(e), Nodes, Node, NodePositions, Positions);
                        }
                        else if (Nodes.Count >= 3)
                        {
                            var Branch = TransformIfBranch(Nodes.Skip(1).ToList(), Node, Text, NodePositions, Positions);
                            var Branches = Mark(new List<IfBranch> { Branch }, Nodes, Node, NodePositions, Positions);
                            var e = Mark(new IfExpr { Branches = Branches }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateIf(e), Nodes, Node, NodePositions, Positions);
                        }
                    }
                    else if (s == "Match")
                    {
                        if (Nodes.Count >= 3)
                        {
                            var MiddleExpr = TransformNodes(Nodes.Skip(1).Take(Nodes.Count - 2).ToList(), Node, Text, NodePositions, Positions);
                            var LastExpr = TransformMatchAlternativeList(Nodes.Last(), Text, NodePositions, Positions);
                            var e = Mark(new MatchExpr { Target = MiddleExpr, Alternatives = LastExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateMatch(e), Nodes, Node, NodePositions, Positions);
                        }
                    }
                    else if (s == "For")
                    {
                        if (Nodes.Count >= 5)
                        {
                            var SecondExpr = TransformLeftValueDefList(Nodes[1], Text, NodePositions, Positions);
                            var MiddleExpr = TransformNodes(Nodes.Skip(3).Take(Nodes.Count - 4).ToList(), Node, Text, NodePositions, Positions);
                            var LastExpr = Transform(Nodes.Last(), Text, NodePositions, Positions);
                            var e = Mark(new ForExpr { EnumeratedValue = SecondExpr, Enumerable = MiddleExpr, Body = LastExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateFor(e), Nodes, Node, NodePositions, Positions);
                        }
                    }
                    else if (s == "While")
                    {
                        if (Nodes.Count >= 3)
                        {
                            var MiddleExpr = TransformNodes(Nodes.Skip(1).Take(Nodes.Count - 2).ToList(), Node, Text, NodePositions, Positions);
                            var LastExpr = Transform(Nodes.Last(), Text, NodePositions, Positions);
                            var e = Mark(new WhileExpr { Condition = MiddleExpr, Body = LastExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateWhile(e), Nodes, Node, NodePositions, Positions);
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
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), Node, Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Node, Text, NodePositions, Positions);
                            var e = Mark(new AssignExpr { Left = LeftExpr, Right = RightExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateAssign(e), Nodes, Node, NodePositions, Positions);
                        }
                        else if (s == "+=")
                        {
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), Node, Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Node, Text, NodePositions, Positions);
                            var e = Mark(new IncreaseExpr { Left = LeftExpr, Right = RightExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateIncrease(e), Nodes, Node, NodePositions, Positions);
                        }
                        else if (s == "-=")
                        {
                            var LeftExpr = TransformLeftValueRefList(Nodes.Take(i).ToList(), Node, Text, NodePositions, Positions);
                            var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Node, Text, NodePositions, Positions);
                            var e = Mark(new DecreaseExpr { Left = LeftExpr, Right = RightExpr }, Nodes, Node, NodePositions, Positions);
                            return Mark(Expr.CreateDecrease(e), Nodes, Node, NodePositions, Positions);
                        }
                        else if (s == "=>")
                        {
                            if (i == 1)
                            {
                                var LeftExpr = TransformLeftValueDefList(Nodes[0], Text, NodePositions, Positions);
                                var RightExpr = TransformNodes(Nodes.Skip(i + 1).ToList(), Node, Text, NodePositions, Positions);
                                var e = Mark(new LambdaExpr { Parameters = LeftExpr, Body = RightExpr }, Nodes, Node, NodePositions, Positions);
                                return Mark(Expr.CreateLambda(e), Nodes, Node, NodePositions, Positions);
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
                    var LeftExpr = Transform(Nodes[0], Text, NodePositions, Positions);
                    var RightExpr = Transform(Nodes[2], Text, NodePositions, Positions);
                    var e = Mark(new BinaryOperatorExpr { Operator = s, Left = LeftExpr, Right = RightExpr }, Nodes, Node, NodePositions, Positions);
                    return Mark(Expr.CreateBinaryOperator(e), Nodes, Node, NodePositions, Positions);
                }
            }
            if (Nodes.Count >= 2)
            {
                var First = Nodes.First();
                if (First.OnOperator)
                {
                    var s = First.Operator;
                    var OperandExpr = TransformNodes(Nodes.Skip(1).ToList(), Node, Text, NodePositions, Positions);
                    var e = Mark(new UnaryOperatorExpr { Operator = s, Operand = OperandExpr }, Nodes, Node, NodePositions, Positions);
                    return Mark(Expr.CreateUnaryOperator(e), Nodes, Node, NodePositions, Positions);
                }
            }


            //TODO
            return Expr.CreateNull();
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
        private static IfBranch TransformIfBranch(List<ExprNode> Nodes, ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new IfBranch { Condition = Expr.CreateNull(), Expr = Expr.CreateNull() };
        }

        private static List<MatchAlternative> TransformMatchAlternativeList(ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            //TODO
            return new List<MatchAlternative> { };
        }

        private static List<LeftValueRef> TransformLeftValueRefList(List<ExprNode> Nodes, ExprNode Node, Text Text, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
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
                    Mark(tl, s.SymbolStartIndex, s.SymbolEndIndex);

                    var ma = new MemberAccess { Parent = Ambiguous, Child = v };
                    var vma = VariableRef.CreateMemberAccess(ma);
                    var vv = Expr.CreateVariableRef(vma);
                    vTotal = vv;
                    Mark(ma, s.SymbolStartIndex, s.SymbolEndIndex);
                    Mark(vma, s.SymbolStartIndex, s.SymbolEndIndex);
                    Mark(vv, s.SymbolStartIndex, s.SymbolEndIndex);

                    var al = new List<Expr> { tl, vv };
                    Ambiguous = Expr.CreateAmbiguous(al);
                    Mark(al, s.SymbolStartIndex, s.SymbolEndIndex);
                    Mark(Ambiguous, s.SymbolStartIndex, s.SymbolEndIndex);
                }
            }

            return new TypeVariableMemberChain { Type = tTotal.Value, TypeLiteral = tlTotal.Value, Variable = vTotal.Value };
        }

        private static T Mark<T>(T SemanticsObj, Object SyntaxObj, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            if (!NodePositions.ContainsKey(SyntaxObj)) { return SemanticsObj; }
            var Range = NodePositions[SyntaxObj];
            Positions.Add(SemanticsObj, Range);
            return SemanticsObj;
        }
        private static T Mark<T>(T SemanticsObj, IEnumerable<Object> SyntaxObjs, Object SyntaxObj, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var FirstObj = SyntaxObjs.FirstOrDefault();
            var LastObj = SyntaxObjs.LastOrDefault();
            if (FirstObj == null)
            {
                if (!NodePositions.ContainsKey(SyntaxObj)) { return SemanticsObj; }
                var Range = NodePositions[SyntaxObj];
                Positions.Add(SemanticsObj, Range);
                return SemanticsObj;
            }
            else
            {
                if (!NodePositions.ContainsKey(FirstObj) || !NodePositions.ContainsKey(LastObj)) { return SemanticsObj; }
                var FirstRange = NodePositions[FirstObj];
                var LastRange = NodePositions[LastObj];
                Positions.Add(SemanticsObj, new TextRange { Start = FirstRange.Start, End = LastRange.End });
                return SemanticsObj;
            }
        }
    }
}
