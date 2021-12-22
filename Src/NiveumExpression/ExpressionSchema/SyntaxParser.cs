//==========================================================================
//
//  File:        SyntaxParser.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 语法解析器
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;

namespace Niveum.ExpressionSchema
{
    public sealed class SyntaxParserResult
    {
        public SyntaxExpr Syntax { get; init; }
    }

    public class SyntaxParser
    {
        private Text Text;
        private Dictionary<Object, TextRange> Positions;
        private TokenParser tp;

        public SyntaxParser(Text Text, Dictionary<Object, TextRange> Positions)
        {
            this.Text = Text;
            this.Positions = Positions;
            this.tp = new TokenParser(Text, Positions);
        }

        public SyntaxParserResult Parse(TextRange RangeInLine)
        {
            var TokenQueue = new LinkedList<SyntaxRule>();

            var r = RangeInLine;
            while (true)
            {
                var t = tp.ReadToken(r);
                if (t.Token.OnSome)
                {
                    TokenQueue.AddLast(t.Token.Some);
                }
                if (!t.RemainingChars.OnSome) { break; }
                r = t.RemainingChars.Some;
            }

            var StateStack = new Stack<int>();
            var RuleStack = new Stack<SyntaxRule>();
            var BinaryOperatorStack = new Stack<TokenBinaryOperator>();

            var State = 0; //初始状态为0
            StateStack.Push(State);
            Optional<SyntaxRule> Token = Optional<SyntaxRule>.Empty;

            Action<SyntaxRule, TextRange> Mark = (st, Range) =>
            {
                if (st.OnExpr)
                {
                    Positions.Add(st.Expr, Range);
                }
                else if (st.OnParameterList)
                {
                    Positions.Add(st.ParameterList, Range);
                }
                else if (st.OnNonnullParameterList)
                {
                    Positions.Add(st.NonnullParameterList, Range);
                }
                Positions.Add(st, Range);
            };

            Func<Boolean> EndOfFile = () => TokenQueue.Count == 0;
            Action<int> Shift = NewState =>
            {
                StateStack.Push(NewState);
                RuleStack.Push(TokenQueue.First.Value);
                TokenQueue.RemoveFirst();
            };
            Action<Func<SyntaxRule>> Reduce0 = f =>
            {
                var st = f();
                TextRange Range;
                if (EndOfFile())
                {
                    Range = new TextRange { Start = RangeInLine.End, End = RangeInLine.End };
                }
                else
                {
                    var NextTokenRange = Positions[Token.Some];
                    Range = new TextRange { Start = NextTokenRange.Start, End = NextTokenRange.Start };
                }
                Mark(st, Range);
                TokenQueue.AddFirst(st);
            };
            Action<Func<SyntaxRule, SyntaxRule>> Reduce1 = f =>
            {
                var Rule0 = RuleStack.Pop();
                StateStack.Pop();
                var st = f(Rule0);
                var Rule0Range = Positions[Rule0];
                var Range = Rule0Range;
                Mark(st, Range);
                TokenQueue.AddFirst(st);
            };
            Action<Func<SyntaxRule, SyntaxRule, SyntaxRule>> Reduce2 = f =>
            {
                var Rule1 = RuleStack.Pop();
                var Rule0 = RuleStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                var st = f(Rule0, Rule1);
                var Rule0Range = Positions[Rule0];
                var Rule1Range = Positions[Rule1];
                var Range = new TextRange { Start = Rule0Range.Start, End = Rule1Range.End };
                Mark(st, Range);
                TokenQueue.AddFirst(st);
            };
            Action<Func<SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule>> Reduce3 = f =>
            {
                var Rule2 = RuleStack.Pop();
                var Rule1 = RuleStack.Pop();
                var Rule0 = RuleStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                var st = f(Rule0, Rule1, Rule2);
                var Rule0Range = Positions[Rule0];
                var Rule2Range = Positions[Rule2];
                var Range = new TextRange { Start = Rule0Range.Start, End = Rule2Range.End };
                Mark(st, Range);
                TokenQueue.AddFirst(st);
            };
            Action<Func<SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule>> Reduce4 = f =>
            {
                var Rule3 = RuleStack.Pop();
                var Rule2 = RuleStack.Pop();
                var Rule1 = RuleStack.Pop();
                var Rule0 = RuleStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                StateStack.Pop();
                var st = f(Rule0, Rule1, Rule2, Rule3);
                var Rule0Range = Positions[Rule0];
                var Rule3Range = Positions[Rule3];
                var Range = new TextRange { Start = Rule0Range.Start, End = Rule3Range.End };
                Mark(st, Range);
                TokenQueue.AddFirst(st);
            };
            Action<SyntaxRule> Replace = f =>
            {
                Mark(f, Positions[Token.Some]);
                Token = f;
                TokenQueue.First.Value = f;
            };

            Func<InvalidSyntaxException> MakeInvalidEndOfFile = () =>
            {
                var Range = new FileTextRange { Text = Text, Range = new TextRange { Start = RangeInLine.End, End = RangeInLine.End } };
                return new InvalidSyntaxException("InvalidEndOfFile", Range);
            };
            Func<InvalidSyntaxException> MakeInvalidSyntaxRule = () =>
            {
                var tr = Positions[Token.Some];
                var Range = new FileTextRange { Text = Text, Range = tr };
                var t = Text.GetTextInLine(tr);
                return new InvalidSyntaxException(String.Format("'{0}' : InvalidSyntaxRuleAtToken", t), Range);
            };

            while (true)
            {
                State = StateStack.Peek();
                Token = EndOfFile() ? Optional<SyntaxRule>.Empty : TokenQueue.First.Value;
                if (State == 0)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(5);
                    }
                    else if (t.OnParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 1)
                {
                    Reduce1(l => SyntaxRule.CreateExpr(SyntaxExpr.CreateLiteral(new ProductionLiteral { Literal = l.Literal })));
                }
                else if (State == 2)
                {
                    if (Token.OnSome && Token.Some.OnLeftParen)
                    {
                        Shift(6);
                    }
                    else
                    {
                        Reduce1(i => SyntaxRule.CreateExpr(SyntaxExpr.CreateVariable(new ProductionVariable { Identifier = i.Identifier })));
                    }
                }
                else if (State == 3)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(7);
                    }
                    else if (t.OnParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 4)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(8);
                    }
                    else if (t.OnParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 5)
                {
                    if (!Token.OnSome) { return new SyntaxParserResult { Syntax = RuleStack.Single().Expr }; }
                    var t = Token.Some;
                    if (t.OnBinaryOperator)
                    {
                        BinaryOperatorStack.Push(t.BinaryOperator);
                        Shift(9);
                    }
                    else
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                }
                else if (State == 6)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        Reduce0(() => SyntaxRule.CreateParameterList(SyntaxParameterList.CreateNull(new ProductionNullParameterList { })));
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(10);
                    }
                    else if (t.OnParameterList)
                    {
                        Shift(11);
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        Shift(12);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 7)
                {
                    if (Token.OnSome && Token.Some.OnLeftParen)
                    {
                        Shift(6);
                    }
                    else
                    {
                        Reduce2((u, e) => SyntaxRule.CreateExpr(SyntaxExpr.CreateUnaryOperator(new ProductionUnaryOperator { UnaryOperator = u.UnaryOperator, Expr = e.Expr })));
                    }
                }
                else if (State == 8)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnBinaryOperator)
                    {
                        BinaryOperatorStack.Push(t.BinaryOperator);
                        Shift(9);
                    }
                    else if (t.OnRightParen)
                    {
                        Shift(13);
                    }
                    else
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                }
                else if (State == 9)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(14);
                    }
                    else if (t.OnParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 10)
                {
                    if (Token.OnSome && Token.Some.OnBinaryOperator)
                    {
                        BinaryOperatorStack.Push(Token.Some.BinaryOperator);
                        Shift(9);
                    }
                    else
                    {
                        Reduce1(e => SyntaxRule.CreateNonnullParameterList(SyntaxNonnullParameterList.CreateSingle(new ProductionSingleParameterList { Expr = e.Expr })));
                    }
                }
                else if (State == 11)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnRightParen)
                    {
                        Shift(15);
                    }
                    else
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                }
                else if (State == 12)
                {
                    if (Token.OnSome && Token.Some.OnComma)
                    {
                        Shift(16);
                    }
                    else
                    {
                        Reduce1(n => SyntaxRule.CreateParameterList(SyntaxParameterList.CreateNonnull(new ProductionNonnullParameterList { NonnullParameterList = n.NonnullParameterList })));
                    }
                }
                else if (State == 13)
                {
                    Reduce3((lp, e, rp) => SyntaxRule.CreateExpr(SyntaxExpr.CreateParen(new ProductionParen { Expr = e.Expr })));
                }
                else if (State == 14)
                {
                    if (Token.OnSome && Token.Some.OnBinaryOperator)
                    {
                        var sbo = BinaryOperatorStack.Peek();
                        var bo = Token.Some.BinaryOperator;
                        var sp = GetPriority(sbo);
                        var p = GetPriority(bo);
                        if (p == 3 && p == sp && bo.Name != sbo.Name)
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                        else if (p >= sp)
                        {
                            Reduce3((le, b, re) => SyntaxRule.CreateExpr(SyntaxExpr.CreateBinaryOperator(new ProductionBinaryOperator { Left = le.Expr, BinaryOperator = b.BinaryOperator, Right = re.Expr })));
                            BinaryOperatorStack.Pop();
                        }
                        else
                        {
                            BinaryOperatorStack.Push(Token.Some.BinaryOperator);
                            Shift(9);
                        }
                    }
                    else
                    {
                        Reduce3((le, b, re) => SyntaxRule.CreateExpr(SyntaxExpr.CreateBinaryOperator(new ProductionBinaryOperator { Left = le.Expr, BinaryOperator = b.BinaryOperator, Right = re.Expr })));
                        BinaryOperatorStack.Pop();
                    }
                }
                else if (State == 15)
                {
                    Reduce4((i, lp, p, rp) => SyntaxRule.CreateExpr(SyntaxExpr.CreateFunction(new ProductionFunction { Identifier = i.Identifier, ParameterList = p.ParameterList })));
                }
                else if (State == 16)
                {
                    if (!Token.OnSome) { throw MakeInvalidEndOfFile(); }
                    var t = Token.Some;
                    if (t.OnLiteral)
                    {
                        Shift(1);
                    }
                    else if (t.OnIdentifier)
                    {
                        Shift(2);
                    }
                    else if (t.OnBinaryOperator)
                    {
                        if (t.BinaryOperator.Name == "+" || t.BinaryOperator.Name == "-")
                        {
                            Replace(SyntaxRule.CreateUnaryOperator(new TokenUnaryOperator { Name = t.BinaryOperator.Name }));
                        }
                        else
                        {
                            throw MakeInvalidSyntaxRule();
                        }
                    }
                    else if (t.OnUnaryOperator)
                    {
                        Shift(3);
                    }
                    else if (t.OnLeftParen)
                    {
                        Shift(4);
                    }
                    else if (t.OnRightParen)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnComma)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnExpr)
                    {
                        Shift(17);
                    }
                    else if (t.OnParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else if (t.OnNonnullParameterList)
                    {
                        throw MakeInvalidSyntaxRule();
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (State == 17)
                {
                    if (Token.OnSome && Token.Some.OnBinaryOperator)
                    {
                        BinaryOperatorStack.Push(Token.Some.BinaryOperator);
                        Shift(9);
                    }
                    else
                    {
                        Reduce3((n, c, e) => SyntaxRule.CreateNonnullParameterList(SyntaxNonnullParameterList.CreateMultiple(new ProductionMultipleParameterList { NonnullParameterList = n.NonnullParameterList, Expr = e.Expr })));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            throw new InvalidOperationException();
        }

        private static int GetPriority(TokenBinaryOperator b)
        {
            var n = b.Name;
            if (n == "*" || n == "/") { return 0; }
            if (n == "+" || n == "-") { return 1; }
            if (n == "<" || n == ">" || n == "<=" || n == ">=" || n == "==" || n == "!=") { return 2; }
            if (n == "&&" || n == "||") { return 3; }
            throw new InvalidOperationException();
        }
    }
}
