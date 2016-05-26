﻿//==========================================================================
//
//  File:        TokenParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 词法解析器
//  Version:     2016.05.26.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Firefly.TextEncoding.String16;
using static Firefly.TextEncoding.String32;
using Firefly.Texting.TreeFormat.Syntax;

namespace Nivea.Template.Syntax
{
    public class TokenParserResult
    {
        public Token Token;
        public Optional<TextRange> RemainingChars;
    }

    public static class TokenParser
    {
        public static Text BuildText(String s, String Path)
        {
            return new Text { Path = Path, Lines = GetLines(s, Path) };
        }

        public static Boolean IsBlankLine(String t)
        {
            return t.All(c => c == ' ');
        }
        public static Boolean IsExactFitIndentCount(String t, int IndentCount)
        {
            if (t.Length < IndentCount) { return false; }
            if (!t.Take(IndentCount).All(c => c == ' ')) { return false; }
            if (t.Length == IndentCount) { return true; }
            if (t[IndentCount] == ' ') { return false; }
            return true;
        }
        public static Boolean IsFitIndentCount(String t, int IndentCount)
        {
            if (t.Length < IndentCount) { return false; }
            if (!t.Take(IndentCount).All(c => c == ' ')) { return false; }
            return true;
        }

        public static Boolean IsExactFitIndentLevel(String t, int IndentLevel)
        {
            return IsExactFitIndentCount(t, IndentLevel * 4);
        }
        public static Boolean IsFitIndentLevel(String t, int IndentLevel)
        {
            return IsFitIndentCount(t, IndentLevel * 4);
        }

        private static Regex rLineSeparator = new Regex(@"\r\n|\n", RegexOptions.ExplicitCapture);
        private static Regex rLineSeparators = new Regex(@"\r|\n", RegexOptions.ExplicitCapture);
        private static List<TextLine> GetLines(String s, String Path)
        {
            var l = new List<TextLine>();

            var CurrentRow = 1;
            var CurrentIndex = 0;
            foreach (Match m in rLineSeparator.Matches(s + "\r\n"))
            {
                var t = s.Substring(CurrentIndex, m.Index - CurrentIndex);
                var Start = new TextPosition { CharIndex = CurrentIndex, Row = CurrentRow, Column = 1 };
                var End = new TextPosition { CharIndex = m.Index, Row = CurrentRow, Column = Start.Column + t.Length };
                var r = new TextRange { Start = Start, End = End };
                l.Add(new TextLine { Text = t, Range = r });
                var mm = rLineSeparators.Match(t);
                if (mm.Success)
                {
                    var SeparatorStart = new TextPosition { CharIndex = CurrentIndex + mm.Index, Row = CurrentRow, Column = mm.Index + 1 };
                    var SeparatorEnd = new TextPosition { CharIndex = CurrentIndex + mm.Index + mm.Length, Row = CurrentRow + 1, Column = 1 };
                    throw new InvalidSyntaxException("IllegalLineSeparator", new FileTextRange { Text = new Text { Path = Path, Lines = l }, Range = new TextRange { Start = SeparatorStart, End = SeparatorEnd } });
                }
                CurrentIndex = m.Index + m.Length;
                CurrentRow += 1;
            }

            return l;
        }

        public static Optional<TokenParserResult> ReadToken(TextRange RangeInLine, Boolean IsLeadingToken, Text Text, Dictionary<Object, TextRange> Positions)
        {
            var s = Text.GetTextInLine(RangeInLine);
            var Index = 0;

            Func<Boolean> EndOfLine = () => Index >= s.Length;
            Func<Char> Peek1 = () => s[Index];
            Func<int, String> Peek = n => s.Substring(Index, Math.Min(n, s.Length - Index));
            Action Proceed = () => Index += 1;
            Action<int> ProceedMultiple = n => Index += n;
            Func<String, int, Boolean> IsHex = (h, n) => (h.Length == n) && h.All(c => "0123456789ABCDEFabcdef".Contains(c));

            Func<Optional<TextRange>> MakeRemainingChars = () => new TextRange { Start = Text.Calc(RangeInLine.Start, Index), End = RangeInLine.End };
            Func<int, int, TextRange> MakeTokenRange = (TokenStart, TokenEnd) => new TextRange { Start = Text.Calc(RangeInLine.Start, TokenStart), End = Text.Calc(RangeInLine.Start, TokenEnd) };
            Func<int, FileTextRange> MakeNextErrorTokenRange = n => new FileTextRange { Text = Text, Range = MakeTokenRange(Index, Index + n) };

            var State = 0;
            var Stack = new Stack<ParenthesisType>();
            var IsAfterSpace = false;

            var StartIndex = 0;
            Func<String, InvalidTokenException> MakeCurrentErrorTokenException = Message => new InvalidTokenException(Message, new FileTextRange { Text = Text, Range = MakeTokenRange(StartIndex, Index) }, Text.GetTextInLine(MakeTokenRange(StartIndex, Index)));
            Func<String, InvalidTokenException> MakeNextCharErrorTokenException = Message => new InvalidTokenException(Message, MakeNextErrorTokenRange(1), Peek(1));
            Action MarkStart = () => StartIndex = Index;
            var Output = new List<Char>();
            Func<Optional<TokenParserResult>> MakeNullResult = () => Optional<TokenParserResult>.Empty;
            Func<TokenType, TokenParserResult> MakeResult = tt =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var t = new Token { OriginalText = Text.GetTextInLine(Range), Type = tt, IsLeadingToken = IsLeadingToken, IsAfterSpace = IsAfterSpace };
                Positions.Add(t, Range);
                var RemainingChars = EndOfLine() ? Optional<TextRange>.Empty : MakeRemainingChars();
                return new TokenParserResult { Token = t, RemainingChars = RemainingChars };
            };
            Func<TokenParserResult> MakeResultChecked = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var OriginalText = Text.GetTextInLine(Range);
                TokenType tt;
                if (OriginalText.All(c => "!%&*+-/<=>?@\\^|~".Contains(c)))
                {
                    tt = TokenType.CreateOperator(OriginalText);
                }
                else if (OriginalText.StartsWith("$") || OriginalText.StartsWith("#"))
                {
                    tt = TokenType.CreatePreprocessDirective(OriginalText);
                }
                else
                {
                    tt = TokenType.CreateDirect(OriginalText);
                }
                var t = new Token { OriginalText = OriginalText, Type = tt, IsLeadingToken = IsLeadingToken, IsAfterSpace = IsAfterSpace };
                Positions.Add(t, Range);
                var RemainingChars = EndOfLine() ? Optional<TextRange>.Empty : MakeRemainingChars();
                return new TokenParserResult { Token = t, RemainingChars = RemainingChars };
            };
            Func<Boolean> IsTokenOperator = () =>
            {
                var Range = MakeTokenRange(StartIndex, Index);
                var OriginalText = Text.GetTextInLine(Range);
                return OriginalText.All(c => "!%&*+-/<=>?@\\^|~".Contains(c));
            };

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfLine()) { return MakeNullResult(); }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        IsAfterSpace = true;
                        Proceed();
                    }
                    else if ("\f\t\v;`".Contains(c))
                    {
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                    else if (c == '"')
                    {
                        MarkStart();
                        State = 2;
                        Proceed();
                    }
                    else if (c == '(')
                    {
                        MarkStart();
                        Proceed();
                        return MakeResult(TokenType.CreateLeftParenthesis());
                    }
                    else if (c == ')')
                    {
                        MarkStart();
                        Proceed();
                        return MakeResult(TokenType.CreateRightParenthesis());
                    }
                    else if (c == ',')
                    {
                        MarkStart();
                        Proceed();
                        return MakeResult(TokenType.CreateComma());
                    }
                    else if ((c == '/') && (Peek(2) == "//"))
                    {
                        MarkStart();
                        Proceed();
                        Proceed();
                        while (!EndOfLine())
                        {
                            Output.Add(Peek1());
                            Proceed();
                        }
                        return MakeResult(TokenType.CreateSingleLineComment(new String(Output.ToArray())));
                    }
                    else if (c == '<')
                    {
                        MarkStart();
                        Stack.Push(ParenthesisType.Angle);
                        State = 1;
                        Proceed();
                    }
                    else if (c == '[')
                    {
                        MarkStart();
                        Stack.Push(ParenthesisType.Bracket);
                        State = 1;
                        Proceed();
                    }
                    else if (c == '{')
                    {
                        MarkStart();
                        Stack.Push(ParenthesisType.Brace);
                        State = 1;
                        Proceed();
                    }
                    else if ("!%&*+-/<=>?@\\^|~".Contains(c))
                    {
                        MarkStart();
                        State = 4;
                        Proceed();
                    }
                    else
                    {
                        MarkStart();
                        State = 1;
                        Proceed();
                    }
                }
                else if (State == 1)
                {
                    if (EndOfLine())
                    {
                        if (Stack.Count == 0) { return MakeResultChecked(); }
                        if (IsTokenOperator()) { return MakeResultChecked(); }
                        throw MakeCurrentErrorTokenException("InvalidParenthesis");
                    }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        if (Stack.Count == 0) { return MakeResultChecked(); }
                        if (IsTokenOperator()) { return MakeResultChecked(); }
                        Proceed();
                    }
                    else if ("\f\t\v".Contains(c))
                    {
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                    else if (c == '"')
                    {
                        if (Stack.Count == 0) { throw MakeNextCharErrorTokenException("InvalidChar"); }
                        Proceed();
                    }
                    else if ("()".Contains(c))
                    {
                        if (Stack.Count == 0) { return MakeResultChecked(); }
                        if (IsTokenOperator()) { return MakeResultChecked(); }
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                    else if (c == ',')
                    {
                        if (Stack.Count == 0) { return MakeResultChecked(); }
                        if (IsTokenOperator()) { return MakeResultChecked(); }
                        Proceed();
                    }
                    else if (c == '<')
                    {
                        Stack.Push(ParenthesisType.Angle);
                        Proceed();
                    }
                    else if (c == '[')
                    {
                        Stack.Push(ParenthesisType.Bracket);
                        Proceed();
                    }
                    else if (c == '{')
                    {
                        Stack.Push(ParenthesisType.Brace);
                        Proceed();
                    }
                    else if (c == '>')
                    {
                        Proceed();
                        if ((Stack.Count == 0) || (Stack.Peek() != ParenthesisType.Angle))
                        {
                            if (IsTokenOperator())
                            {
                                State = 4;
                                Stack.Clear();
                            }
                            else
                            {
                                throw MakeCurrentErrorTokenException("InvalidParenthesis");
                            }
                        }
                        else
                        {
                            Stack.Pop();
                        }
                    }
                    else if (c == ']')
                    {
                        Proceed();
                        if ((Stack.Count == 0) || (Stack.Peek() != ParenthesisType.Bracket)) { throw MakeCurrentErrorTokenException("InvalidParenthesis"); }
                        Stack.Pop();
                    }
                    else if (c == '}')
                    {
                        Proceed();
                        if ((Stack.Count == 0) || (Stack.Peek() != ParenthesisType.Brace)) { throw MakeCurrentErrorTokenException("InvalidParenthesis"); }
                        Stack.Pop();
                    }
                    else
                    {
                        Proceed();
                    }
                }
                else if (State == 2)
                {
                    if (EndOfLine()) { throw MakeCurrentErrorTokenException("InvalidQuotationMark"); }
                    var c = Peek1();
                    if (c == '"')
                    {
                        State = 21;
                        Proceed();
                    }
                    else
                    {
                        Output.Add(c);
                        State = 22;
                        Proceed();
                    }
                }
                else if (State == 21)
                {
                    if (EndOfLine()) { return MakeResult(TokenType.CreateQuoted(new String(Output.ToArray()))); }
                    var c = Peek1();
                    if ((c == ' ') || (c == '(') || (c == ')'))
                    {
                        return MakeResult(TokenType.CreateQuoted(new String(Output.ToArray())));
                    }
                    else if (c == '"')
                    {
                        Output.Add(c);
                        State = 22;
                        Proceed();
                    }
                    else if (c == '\\')
                    {
                        State = 31;
                        Proceed();
                    }
                    else
                    {
                        Output.Add(c);
                        State = 3;
                        Proceed();
                    }
                }
                else if (State == 22)
                {
                    if (EndOfLine()) { throw MakeCurrentErrorTokenException("InvalidQuotationMark"); }
                    var c = Peek1();
                    if (c == '"')
                    {
                        State = 23;
                        Proceed();
                    }
                    else
                    {
                        Output.Add(c);
                        Proceed();
                    }
                }
                else if (State == 23)
                {
                    if (EndOfLine()) { return MakeResult(TokenType.CreateQuoted(new String(Output.ToArray()))); }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        return MakeResult(TokenType.CreateQuoted(new String(Output.ToArray())));
                    }
                    else if ("\f\t\v".Contains(c))
                    {
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                    else if (c == '"')
                    {
                        Output.Add(c);
                        State = 22;
                        Proceed();
                    }
                    else if ("(),".Contains(c))
                    {
                        return MakeResult(TokenType.CreateQuoted(new String(Output.ToArray())));
                    }
                    else
                    {
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                }
                else if (State == 3)
                {
                    if (EndOfLine()) { throw MakeCurrentErrorTokenException("InvalidQuotationMark"); }
                    var c = Peek1();
                    if (c == '"')
                    {
                        if (Peek(2) == "\"\"")
                        {
                            Proceed();
                            Proceed();
                            return MakeResult(TokenType.CreateEscaped(new String(Output.ToArray())));
                        }
                        else
                        {
                            throw MakeCurrentErrorTokenException("InvalidQuotationMark");
                        }
                    }
                    else if (c == '\\')
                    {
                        State = 31;
                        Proceed();
                    }
                    else
                    {
                        Output.Add(c);
                        Proceed();
                    }
                }
                else if (State == 31)
                {
                    if (EndOfLine()) { throw MakeCurrentErrorTokenException("InvalidQuotationMark"); }
                    var c = Peek1();
                    if (c == '0')
                    {
                        Output.Add('\0');
                    }
                    else if (c == 'a')
                    {
                        Output.Add('\a');
                    }
                    else if (c == 'b')
                    {
                        Output.Add('\b');
                    }
                    else if (c == 'f')
                    {
                        Output.Add('\f');
                    }
                    else if (c == 'n')
                    {
                        Output.Add('\n');
                    }
                    else if (c == 'r')
                    {
                        Output.Add('\r');
                    }
                    else if (c == 't')
                    {
                        Output.Add('\t');
                    }
                    else if (c == 'v')
                    {
                        Output.Add('\v');
                    }
                    else if (c == 'x')
                    {
                        Proceed();
                        var Hex = Peek(2);
                        ProceedMultiple(Hex.Length);
                        if (!IsHex(Hex, 2)) { throw MakeCurrentErrorTokenException("InvalidEscapeSequence"); }
                        Output.Add(ChrW(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)));
                        State = 3;
                    }
                    else if (c == 'u')
                    {
                        Proceed();
                        var Hex = Peek(4);
                        ProceedMultiple(Hex.Length);
                        if (!IsHex(Hex, 4)) { throw MakeCurrentErrorTokenException("InvalidEscapeSequence"); }
                        Output.Add(ChrW(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)));
                        State = 3;
                    }
                    else if (c == 'U')
                    {
                        Proceed();
                        var Hex = Peek(5);
                        ProceedMultiple(Hex.Length);
                        if (!IsHex(Hex, 5)) { throw MakeCurrentErrorTokenException("InvalidEscapeSequence"); }
                        Output.AddRange(ChrQ(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)).ToString());
                        State = 3;
                    }
                    else
                    {
                        Output.Add(c);
                        State = 3;
                        Proceed();
                    }
                }
                else if (State == 4)
                {
                    if (EndOfLine()) { return MakeResultChecked(); }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        return MakeResultChecked();
                    }
                    else if ("!%&*+-/<=>?@\\^|~".Contains(c))
                    {
                        Proceed();
                    }
                    else
                    {
                        throw MakeNextCharErrorTokenException("InvalidChar");
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public static List<Token> ReadTokensInLine(Text Text, Dictionary<Object, TextRange> Positions, TextRange RangeInLine)
        {
            var l = new List<Token>();
            var Range = RangeInLine;
            var IsLeadingToken = true;
            while (true)
            {
                var oResult = ReadToken(Range, IsLeadingToken, Text, Positions);
                if (oResult.OnNotHasValue) { break; }
                var Result = oResult.Value;
                l.Add(Result.Token);
                if (Result.RemainingChars.OnNotHasValue) { break; }
                Range = Result.RemainingChars.Value;
                IsLeadingToken = false;
            }
            return l;
        }
    }
}
