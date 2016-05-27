//==========================================================================
//
//  File:        ExprTransformer.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式转换器
//  Version:     2016.05.27.
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
                var Ambiguous = new List<Expr> { };

                if (s == "Throw")
                {
                    Ambiguous.Add(Mark(Expr.CreateThrow(Optional<Expr>.Empty), Node, NodePositions, Positions));
                }
                else if (s == "Continue")
                {
                    Ambiguous.Add(Mark(Expr.CreateContinue(Optional<int>.Empty), Node, NodePositions, Positions));
                }
                else if (s == "Break")
                {
                    Ambiguous.Add(Mark(Expr.CreateBreak(Optional<int>.Empty), Node, NodePositions, Positions));
                }
                else if (s == "Return")
                {
                    Ambiguous.Add(Mark(Expr.CreateReturn(Optional<Expr>.Empty), Node, NodePositions, Positions));
                }
                else if (s == "Null")
                {
                    Ambiguous.Add(Mark(Expr.CreateNull(), Node, NodePositions, Positions));
                }
                else if (s == "Default")
                {
                    Ambiguous.Add(Mark(Expr.CreateDefault(), Node, NodePositions, Positions));
                }
                else if (s == "This")
                {
                    var e = Mark(VariableRef.CreateThis(), Node, NodePositions, Positions);
                    Ambiguous.Add(Mark(Expr.CreateVariableRef(e), Node, NodePositions, Positions));
                }

                int InvalidCharIndex;
                var ot = TypeParser.TryParseTypeSpec(s, (o, Start, End) =>
                {
                    if (NodePositions.ContainsKey(Node))
                    {
                        var Range = NodePositions[Node];
                        var TypeRange = new TextRange { Start = Text.Calc(Range.Start, Start), End = Text.Calc(Range.Start, End) };
                        Positions.Add(o, TypeRange);
                    }
                }, out InvalidCharIndex);
                if (ot.OnHasValue)
                {
                    var t = ot.Value;
                    Ambiguous.Add(Mark(Expr.CreateTypeLiteral(t), Node, NodePositions, Positions));
                }

                //TODO

                if (Ambiguous.Count == 1)
                {
                    return Ambiguous.Single();
                }
                else if (Ambiguous.Count > 1)
                {
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
                    //TODO
                }
                else
                {
                    var Transformed = Stem.Nodes.Select(n => Transform(n, Text, NodePositions, Positions)).ToList();
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

                    var Ambiguous = new List<Expr> { };

                    int InvalidCharIndex;
                    var ot = TypeParser.TryParseTypeSpec(s, (o, Start, End) =>
                    {
                        if (NodePositions.ContainsKey(Node))
                        {
                            var Range = NodePositions[Node];
                            var TypeRange = new TextRange { Start = Text.Calc(Range.Start, Start), End = Text.Calc(Range.Start, End) };
                            Positions.Add(o, TypeRange);
                        }
                    }, out InvalidCharIndex);
                    if (ot.OnHasValue)
                    {
                        var t = ot.Value;

                        if (Second.OnDirect || Second.OnLiteral || Second.OnOperator)
                        {
                            var ple = Mark(new PrimitiveLiteralExpr { Type = t, Value = Second.OnDirect ? Second.Direct : Second.OnLiteral ? Second.Literal : Second.OnOperator ? Second.Operator : "" }, Nodes, Node, NodePositions, Positions);
                            Ambiguous.Add(Mark(Expr.CreatePrimitiveLiteral(ple), Nodes, Node, NodePositions, Positions));
                        }
                        if (Second.OnDirect || Second.OnLiteral || Second.OnStem || Second.OnUndetermined)
                        {
                            var Last = Nodes.Last();
                            var LastExpr = Transform(Last, Text, NodePositions, Positions);

                            var FieldAssigns = new List<FieldAssign>();
                            var FieldAssignRanges = new Dictionary<FieldAssign, TextRange>();
                            if (LastExpr.OnAssign)
                            {
                                var a = LastExpr.Assign;
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
                            else if (LastExpr.OnSequence)
                            {
                                foreach (var e in LastExpr.Sequence)
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
                                var rle = Mark(new RecordLiteralExpr { Type = t, FieldAssigns = Mark(FieldAssigns, Last, NodePositions, Positions) }, Last, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateRecordLiteral(rle), Nodes, Node, NodePositions, Positions));
                            }
                            else if (LastExpr.OnSequence)
                            {
                                if (LastExpr.Sequence.Count == 0)
                                {
                                    var rle = Mark(new RecordLiteralExpr { Type = t, FieldAssigns = Mark(FieldAssigns, Last, NodePositions, Positions) }, Last, NodePositions, Positions);
                                    Ambiguous.Add(Mark(Expr.CreateRecordLiteral(rle), Nodes, Node, NodePositions, Positions));
                                }
                                if (LastExpr.Sequence.Count >= 2)
                                {
                                    if (t.OnTuple)
                                    {
                                        var tle = Mark(new TupleLiteralExpr { Type = t, Parameters = LastExpr.Sequence }, Last, NodePositions, Positions);
                                        return Mark(Expr.CreateTupleLiteral(tle), Nodes, Node, NodePositions, Positions);
                                    }
                                    else if (t.OnTypeRef && (t.TypeRef.Name == "Tuple") && (t.TypeRef.Version == ""))
                                    {
                                        var tle = Mark(new TupleLiteralExpr { Type = Optional<TypeSpec>.Empty, Parameters = LastExpr.Sequence }, Last, NodePositions, Positions);
                                        Ambiguous.Add(Mark(Expr.CreateTupleLiteral(tle), Nodes, Node, NodePositions, Positions));
                                    }
                                }
                                var lle = Mark(new ListLiteralExpr { Type = t, Parameters = LastExpr.Sequence }, Last, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateListLiteral(lle), Nodes, Node, NodePositions, Positions));
                            }
                            else
                            {
                                var Parameters = Mark(new List<Expr> { LastExpr }, Last, NodePositions, Positions);
                                var lle = Mark(new ListLiteralExpr { Type = t, Parameters = Parameters }, Last, NodePositions, Positions);
                                Ambiguous.Add(Mark(Expr.CreateListLiteral(lle), Nodes, Node, NodePositions, Positions));
                            }
                        }
                    }

                    //TODO

                    if (Ambiguous.Count == 1)
                    {
                        return Ambiguous.Single();
                    }
                    else if (Ambiguous.Count > 1)
                    {
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
