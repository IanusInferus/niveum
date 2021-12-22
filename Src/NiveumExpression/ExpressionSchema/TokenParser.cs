//==========================================================================
//
//  File:        TokenParser.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 符号解析器
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;

namespace Niveum.ExpressionSchema
{
    public class TokenParserResult
    {
        public Optional<SyntaxRule> Token;
        public Optional<TextRange> RemainingChars;
    }

    public class TokenParser
    {
        private Text Text;
        private Dictionary<Object, TextRange> Positions;

        public TokenParser(Text Text, Dictionary<Object, TextRange> Positions)
        {
            this.Text = Text;
            this.Positions = Positions;
        }

        private static Regex rSymbol = new Regex(@"^[A-Za-z0-9_.]$");
        private static Regex rBooleanLiteral = new Regex(@"^false|true$");
        private static Regex rIntLiteral = new Regex(@"^[0-9]+$");
        private static Regex rRealLiteral = new Regex(@"^[0-9]+\.[0-9]*|[0-9]*\.[0-9]+$");
        private static Regex rIdentifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");
        public TokenParserResult ReadToken(TextRange RangeInLine)
        {
            var s = Text.GetTextInLine(RangeInLine);
            int Index = 0;

            Func<Boolean> EndOfLine = () => Index >= s.Length;
            Func<int, String> Peek = n => s.Substring(Index, Math.Min(n, s.Length - Index));
            Action Proceed = () => Index += 1;
            Action<int> ProceedMultiple = n => Index += n;

            Func<Optional<TextRange>> MakeRemainingChars = () => new TextRange { Start = Text.Calc(RangeInLine.Start, Index), End = RangeInLine.End };
            var NullRemainingChars = Optional<TextRange>.Empty;
            var NullToken = Optional<SyntaxRule>.Empty;
            Func<int, int, TextRange> MakeTokenRange = (TokenStart, TokenEnd) => new TextRange { Start = Text.Calc(RangeInLine.Start, TokenStart), End = Text.Calc(RangeInLine.Start, TokenEnd) };
            Func<int, FileTextRange> MakeNextErrorTokenRange = n => new FileTextRange { Text = Text, Range = MakeTokenRange(Index, n) };

            var State = 0;

            var StartIndex = 0;
            Func<String, InvalidTokenException> MakeCurrentErrorTokenException = Message => new InvalidTokenException(Message, new FileTextRange { Text = Text, Range = MakeTokenRange(StartIndex, Index) }, Text.GetTextInLine(MakeTokenRange(StartIndex, Index)));
            Func<String, InvalidTokenException> MakeNextCharErrorTokenException = Message => new InvalidTokenException(Message, MakeNextErrorTokenRange(1), Peek(1));
            Action MarkStart = () => StartIndex = Index;
            var Output = new List<Char>();
            Action<String> Write = cs => Output.AddRange(cs);

            Func<Optional<SyntaxRule>> MakeSymbol = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var Symbol = new String(Output.ToArray());
                if (rBooleanLiteral.Match(Symbol).Success)
                {
                    var t = TokenLiteral.CreateBooleanValue(!Symbol.Equals("false", StringComparison.Ordinal));
                    var st = SyntaxRule.CreateLiteral(t);
                    Positions.Add(t, Range);
                    Positions.Add(st, Range);
                    return st;
                }
                else if (rIntLiteral.Match(Symbol).Success)
                {
                    var t = TokenLiteral.CreateIntValue(NumericStrings.InvariantParseInt32(Symbol));
                    var st = SyntaxRule.CreateLiteral(t);
                    Positions.Add(t, Range);
                    Positions.Add(st, Range);
                    return st;
                }
                else if (rRealLiteral.Match(Symbol).Success)
                {
                    var t = TokenLiteral.CreateRealValue(NumericStrings.InvariantParseFloat64(Symbol));
                    var st = SyntaxRule.CreateLiteral(t);
                    Positions.Add(t, Range);
                    Positions.Add(st, Range);
                    return st;
                }
                else if (rIdentifier.Match(Symbol).Success)
                {
                    var t = new TokenIdentifier { Name = Symbol };
                    var st = SyntaxRule.CreateIdentifier(t);
                    Positions.Add(t, Range);
                    Positions.Add(st, Range);
                    return st;
                }
                else
                {
                    throw MakeCurrentErrorTokenException("InvalidToken");
                }
            };

            Func<Optional<SyntaxRule>> MakeBinaryOperator = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new TokenBinaryOperator { Name = new String(Output.ToArray()) };
                var st = SyntaxRule.CreateBinaryOperator(t);
                Positions.Add(t, Range);
                Positions.Add(st, Range);
                return st;
            };

            Func<Optional<SyntaxRule>> MakeUnaryOperator = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new TokenUnaryOperator { Name = new String(Output.ToArray()) };
                var st = SyntaxRule.CreateUnaryOperator(t);
                Positions.Add(t, Range);
                Positions.Add(st, Range);
                return st;
            };

            Func<Optional<SyntaxRule>> MakeLeftParen = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new TokenLeftParen { };
                var st = SyntaxRule.CreateLeftParen(t);
                Positions.Add(t, Range);
                Positions.Add(st, Range);
                return st;
            };

            Func<Optional<SyntaxRule>> MakeRightParen = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new TokenRightParen { };
                var st = SyntaxRule.CreateRightParen(t);
                Positions.Add(t, Range);
                Positions.Add(st, Range);
                return st;
            };

            Func<Optional<SyntaxRule>> MakeComma = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new TokenComma { };
                var st = SyntaxRule.CreateComma(t);
                Positions.Add(t, Range);
                Positions.Add(st, Range);
                return st;
            };

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfLine())
                    {
                        return new TokenParserResult { Token = NullToken, RemainingChars = NullRemainingChars };
                    }
                    var c2 = Peek(2);
                    if (c2 == "&&" || c2 == "||" || c2 == "<=" || c2 == ">=" || c2 == "==" || c2 == "!=")
                    {
                        MarkStart();
                        Write(c2);
                        ProceedMultiple(2);
                        return new TokenParserResult { Token = MakeBinaryOperator(), RemainingChars = MakeRemainingChars() };
                    }
                    var c = Peek(1);
                    if (c == " ")
                    {
                        Proceed();
                        continue;
                    }
                    if (c == "+" || c == "-" || c == "*" || c == "/" || c == "<" || c == ">")
                    {
                        MarkStart();
                        Write(c);
                        Proceed();
                        return new TokenParserResult { Token = MakeBinaryOperator(), RemainingChars = MakeRemainingChars() };
                    }
                    if (c == "!")
                    {
                        MarkStart();
                        Write(c);
                        Proceed();
                        return new TokenParserResult { Token = MakeUnaryOperator(), RemainingChars = MakeRemainingChars() };
                    }
                    if (c == "(")
                    {
                        MarkStart();
                        Proceed();
                        return new TokenParserResult { Token = MakeLeftParen(), RemainingChars = MakeRemainingChars() };
                    }
                    if (c == ")")
                    {
                        MarkStart();
                        Proceed();
                        return new TokenParserResult { Token = MakeRightParen(), RemainingChars = MakeRemainingChars() };
                    }
                    if (c == ",")
                    {
                        MarkStart();
                        Proceed();
                        return new TokenParserResult { Token = MakeComma(), RemainingChars = MakeRemainingChars() };
                    }
                    if (rSymbol.Match(c).Success)
                    {
                        MarkStart();
                        Write(c);
                        Proceed();
                        State = 1;
                        continue;
                    }
                    throw MakeNextCharErrorTokenException("InvalidChar");
                }
                else if (State == 1)
                {
                    var c = Peek(1);
                    if (rSymbol.Match(c).Success)
                    {
                        Write(c);
                        Proceed();
                        continue;
                    }
                    return new TokenParserResult { Token = MakeSymbol(), RemainingChars = MakeRemainingChars() };
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
