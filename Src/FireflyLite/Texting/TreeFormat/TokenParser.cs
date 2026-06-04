using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat.Syntax;

namespace Firefly.Texting.TreeFormat
{
    public class TreeFormatTokenParseResult
    {
        public Token Token;
        public Optional<TextRange> RemainingChars;
    }

    public sealed class TreeFormatTokenParser
    {
        private TreeFormatTokenParser()
        {
        }

        public static Text BuildText(string Text, string Path)
        {
            return new Text { Path = Path, Lines = GetLines(Text, Path) };
        }

        private static Regex rLineSeparator = new Regex("\r\n|\n", RegexOptions.ExplicitCapture);
        private static Regex rLineSeparators = new Regex("\r|\n", RegexOptions.ExplicitCapture);
        private static List<TextLine> GetLines(string Text, string Path)
        {
            var l = new List<TextLine>();

            var CurrentRow = 1;
            var CurrentIndex = 0;
            foreach (Match m in rLineSeparator.Matches(Text + "\\r\\n".Descape()))
            {
                var t = Text.Substring(CurrentIndex, m.Index - CurrentIndex);
                var Start = new TextPosition { CharIndex = CurrentIndex, Row = CurrentRow, Column = 1 };
                TextPosition End = new TextPosition { CharIndex = m.Index, Row = CurrentRow, Column = Start.Column + t.Length };
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

        public static bool IsBlankLine(TextLine Line)
        {
            var t = Line.Text;
            return t.All(c => c == ' ');
        }
        public static bool IsExactFitIndentLevel(TextLine Line, int IndentLevel)
        {
            var t = Line.Text;
            var IndentCount = IndentLevel * 4;
            if (t.Length < IndentCount) return false;
            if (!t.Take(IndentCount).All(c => c == ' ')) return false;
            if (t.Length == IndentCount) return true;
            if (t[IndentCount] == ' ') return false;
            return true;
        }
        public static bool IsFitIndentLevel(TextLine Line, int IndentLevel)
        {
            var t = Line.Text;
            var IndentCount = IndentLevel * 4;
            if (t.Length < IndentCount) return false;
            if (!t.Take(IndentCount).All(c => c == ' ')) return false;
            return true;
        }

        private enum ParenthesisType
        {
            Angle,
            Bracket,
            Brace
        }
        private static char[] ForbiddenWhitespaces = "\\f\\t\\v".Descape().ToCharArray();
        public static Optional<TreeFormatTokenParseResult> ReadToken(Text Text, Dictionary<object, TextRange> Positions, TextRange RangeInLine)
        {
            var s = Text.GetTextInLine(RangeInLine);
            int Index = 0;

            Func<bool> EndOfLine = () => Index >= s.Length;
            Func<char> Peek1 = () => s[Index];
            Func<int, string> Peek = n => s.Substring(Index, Math.Min(n, s.Length - Index));
            Action Proceed = () => Index += 1;
            Action<int> ProceedMultiple = n => Index += n;

            Func<Optional<TextRange>> MakeRemainingChars = () => (Optional<TextRange>)new TextRange { Start = Text.Calc(RangeInLine.Start, Index), End = RangeInLine.End };
            Func<int, int, TextRange> MakeTokenRange = (TokenStart, TokenEnd) => new TextRange { Start = Text.Calc(RangeInLine.Start, TokenStart), End = Text.Calc(RangeInLine.Start, TokenEnd) };
            Func<int, FileTextRange> MakeNextErrorTokenRange = n => new FileTextRange { Text = Text, Range = MakeTokenRange(Index, Index + n) };
            Func<char, bool> IsForbiddenWhitespaces = c => ForbiddenWhitespaces.Contains(c);
            Func<char, bool> IsForbiddenHeadChars = c => "!%&;=?^`|~".Contains(c);
            Func<string, int, bool> IsHex = (h, n) => h.Length == n && h.ToCharArray().All(c => "0123456789ABCDEFabcdef".Contains(c));

            var State = 0;
            var Stack = new Stack<ParenthesisType>();

            int StartIndex = 0;
            Func<string, InvalidTokenException> MakeCurrentErrorTokenException = Message => new InvalidTokenException(Message, new FileTextRange { Text = Text, Range = MakeTokenRange(StartIndex, Index) }, Text.GetTextInLine(MakeTokenRange(StartIndex, Index)));
            Func<string, InvalidTokenException> MakeNextCharErrorTokenException = Message => new InvalidTokenException(Message, MakeNextErrorTokenRange(1), Peek(1));
            Action MarkStart = () => StartIndex = Index;
            var Output = new List<char>();
            Func<Optional<TreeFormatTokenParseResult>> MakeNullResult = () => Optional<TreeFormatTokenParseResult>.Empty;
            Func<Token, TreeFormatTokenParseResult> MakeResult =
                t =>
                {
                    var Range = MakeTokenRange(StartIndex, Index);
                    Positions.Add(t, Range);
                    var RemainingChars = EndOfLine() ? Optional<TextRange>.Empty : MakeRemainingChars();
                    return new TreeFormatTokenParseResult { Token = t, RemainingChars = RemainingChars };
                };
            Func<TreeFormatTokenParseResult> MakeResultChecked =
                () =>
                {
                    var Range = MakeTokenRange(StartIndex, Index);
                    var OriginalText = Text.GetTextInLine(Range);
                    Token t;
                    if (OriginalText.StartsWith("$"))
                    {
                        t = Token.CreatePreprocessDirective(OriginalText.Substring(1));
                    }
                    else if (OriginalText.StartsWith("#"))
                    {
                        t = Token.CreateFunctionDirective(OriginalText.Substring(1));
                    }
                    else
                    {
                        t = Token.CreateSingleLineLiteral(OriginalText);
                    }
                    Positions.Add(t, Range);
                    var RemainingChars = EndOfLine() ? Optional<TextRange>.Empty : MakeRemainingChars();
                    return new TreeFormatTokenParseResult { Token = t, RemainingChars = RemainingChars };
                };

            while (true)
            {
                switch (State)
                {
                    case 0:
                        {
                            if (EndOfLine()) return MakeNullResult();
                            var c = Peek1();
                            if (IsForbiddenWhitespaces(c)) throw MakeNextCharErrorTokenException("InvalidChar");
                            if (IsForbiddenHeadChars(c)) throw MakeNextCharErrorTokenException("InvalidHeadChar");
                            switch (c)
                            {
                                case ' ':
                                    Proceed();
                                    break;
                                case '"':
                                    MarkStart();
                                    State = 2;
                                    Proceed();
                                    break;
                                case '(':
                                    MarkStart();
                                    Proceed();
                                    return MakeResult(Token.CreateLeftParenthesis());
                                case ')':
                                    MarkStart();
                                    Proceed();
                                    return MakeResult(Token.CreateRightParenthesis());
                                case '/':
                                    if (Peek(2) == "//")
                                    {
                                        MarkStart();
                                        Proceed();
                                        Proceed();
                                        while (!EndOfLine())
                                        {
                                            Output.Add(Peek1());
                                            Proceed();
                                        }
                                        return MakeResult(Token.CreateSingleLineComment(new string(Output.ToArray())));
                                    }
                                    throw MakeNextCharErrorTokenException("InvalidChar");
                                case '<':
                                    MarkStart();
                                    Stack.Push(ParenthesisType.Angle);
                                    Output.Add(c);
                                    State = 1;
                                    Proceed();
                                    break;
                                case '[':
                                    MarkStart();
                                    Stack.Push(ParenthesisType.Bracket);
                                    State = 1;
                                    Proceed();
                                    break;
                                case '{':
                                    MarkStart();
                                    Stack.Push(ParenthesisType.Brace);
                                    State = 1;
                                    Proceed();
                                    break;
                                default:
                                    MarkStart();
                                    State = 1;
                                    Proceed();
                                    break;
                            }
                            break;
                        }
                    case 1:
                        {
                            if (EndOfLine())
                            {
                                if (Stack.Count != 0) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                return MakeResultChecked();
                            }
                            var c = Peek1();
                            if (IsForbiddenWhitespaces(c)) throw MakeNextCharErrorTokenException("InvalidChar");
                            switch (c)
                            {
                                case ' ':
                                    if (Stack.Count == 0) return MakeResultChecked();
                                    Proceed();
                                    break;
                                case '"':
                                    if (Stack.Count == 0) throw MakeNextCharErrorTokenException("InvalidChar");
                                    Proceed();
                                    break;
                                case '(':
                                case ')':
                                    if (Stack.Count != 0) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    return MakeResultChecked();
                                case '<':
                                    Stack.Push(ParenthesisType.Angle);
                                    Proceed();
                                    break;
                                case '[':
                                    Stack.Push(ParenthesisType.Bracket);
                                    Proceed();
                                    break;
                                case '{':
                                    Stack.Push(ParenthesisType.Brace);
                                    Proceed();
                                    break;
                                case '>':
                                    Proceed();
                                    if (Stack.Count == 0) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    if (Stack.Peek() != ParenthesisType.Angle) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    Stack.Pop();
                                    break;
                                case ']':
                                    Proceed();
                                    if (Stack.Count == 0) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    if (Stack.Peek() != ParenthesisType.Bracket) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    Stack.Pop();
                                    break;
                                case '}':
                                    Proceed();
                                    if (Stack.Count == 0) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    if (Stack.Peek() != ParenthesisType.Brace) throw MakeCurrentErrorTokenException("InvalidParenthesis");
                                    Stack.Pop();
                                    break;
                                default:
                                    Proceed();
                                    break;
                            }
                            break;
                        }
                    case 2:
                        {
                            if (EndOfLine()) throw MakeCurrentErrorTokenException("InvalidQuotationMark");
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
                            break;
                        }
                    case 21:
                        {
                            if (EndOfLine()) return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                            var c = Peek1();
                            switch (c)
                            {
                                case ' ':
                                case '(':
                                case ')':
                                    return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                                case '"':
                                    Output.Add(c);
                                    State = 22;
                                    Proceed();
                                    break;
                                case '\\':
                                    State = 31;
                                    Proceed();
                                    break;
                                default:
                                    Output.Add(c);
                                    State = 3;
                                    Proceed();
                                    break;
                            }
                            break;
                        }
                    case 22:
                        {
                            if (EndOfLine()) throw MakeCurrentErrorTokenException("InvalidQuotationMark");
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
                            break;
                        }
                    case 23:
                        {
                            if (EndOfLine()) return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                            var c = Peek1();
                            if (IsForbiddenWhitespaces(c)) throw MakeNextCharErrorTokenException("InvalidChar");
                            switch (c)
                            {
                                case ' ':
                                    return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                                case '"':
                                    Output.Add(c);
                                    State = 22;
                                    Proceed();
                                    break;
                                case '(':
                                case ')':
                                    return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                                default:
                                    throw MakeNextCharErrorTokenException("InvalidChar");
                            }
                            break;
                        }
                    case 3:
                        {
                            if (EndOfLine()) throw MakeCurrentErrorTokenException("InvalidQuotationMark");
                            var c = Peek1();
                            switch (c)
                            {
                                case '"':
                                    if (Peek(2) == "\"\"")
                                    {
                                        Proceed();
                                        Proceed();
                                        return MakeResult(Token.CreateSingleLineLiteral(new string(Output.ToArray())));
                                    }
                                    throw MakeNextCharErrorTokenException("InvalidQuotationMark");
                                case '\\':
                                    State = 31;
                                    Proceed();
                                    break;
                                default:
                                    Output.Add(c);
                                    Proceed();
                                    break;
                            }
                            break;
                        }
                    case 31:
                        {
                            if (EndOfLine()) throw MakeCurrentErrorTokenException("InvalidQuotationMark");
                            var c = Peek1();
                            switch (c)
                            {
                                case '0':
                                    Output.Add((char)0x0);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'a':
                                    Output.Add((char)0x7);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'b':
                                    Output.Add((char)0x8);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'f':
                                    Output.Add((char)0xC);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'n':
                                    Output.Add((char)0xA);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'r':
                                    Output.Add((char)0xD);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 't':
                                    Output.Add((char)0x9);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'v':
                                    Output.Add((char)0xB);
                                    State = 3;
                                    Proceed();
                                    break;
                                case 'x':
                                    {
                                        Proceed();
                                        var Hex = Peek(2);
                                        ProceedMultiple(Hex.Length);
                                        if (!IsHex(Hex, 2))
                                        {
                                            throw MakeCurrentErrorTokenException("InvalidEscapeSequence");
                                        }
                                        Output.Add((char)int.Parse(Hex, System.Globalization.NumberStyles.HexNumber));
                                        State = 3;
                                        break;
                                    }
                                case 'u':
                                    {
                                        Proceed();
                                        var Hex = Peek(4);
                                        ProceedMultiple(Hex.Length);
                                        if (!IsHex(Hex, 4))
                                        {
                                            throw MakeCurrentErrorTokenException("InvalidEscapeSequence");
                                        }
                                        Output.Add((char)int.Parse(Hex, System.Globalization.NumberStyles.HexNumber));
                                        State = 3;
                                        break;
                                    }
                                case 'U':
                                    {
                                        Proceed();
                                        var Hex = Peek(5);
                                        ProceedMultiple(Hex.Length);
                                        if (!IsHex(Hex, 5))
                                        {
                                            throw MakeCurrentErrorTokenException("InvalidEscapeSequence");
                                        }
                                        Output.AddRange(String32.ChrQ(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)).ToString());
                                        State = 3;
                                        break;
                                    }
                                default:
                                    Output.Add(c);
                                    State = 3;
                                    Proceed();
                                    break;
                            }
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
            throw new InvalidOperationException();
        }
    }
}
