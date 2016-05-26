//==========================================================================
//
//  File:        ExprTransformer.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式转换器
//  Version:     2016.05.26.
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
                else
                {
                    //TODO
                }
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
