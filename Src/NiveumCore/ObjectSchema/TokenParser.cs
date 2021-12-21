//==========================================================================
//
//  File:        TokenParser.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 词法解析器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Firefly.TextEncoding.String16;
using static Firefly.TextEncoding.String32;
using Firefly.Texting.TreeFormat.Syntax;

namespace Niveum.ObjectSchema
{
    public sealed class TokenParserResult
    {
        public Token Token { get; init; }
        public Optional<TextRange> RemainingChars { get; init; }
    }
    public sealed class DescriptionComposite
    {
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        public String Description { get; init; }
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

        public sealed class Symbol
        {
            public String Name { get; init; }
            public int SymbolStartIndex { get; init; }
            public int SymbolEndIndex { get; init; }
            public int NameStartIndex { get; init; }
            public int NameEndIndex { get; init; }
            public List<KeyValuePair<String, int>> Parameters { get; init; }
        }

        public static Optional<List<Symbol>> TrySplitSymbolMemberChain(String s, out int InvalidCharIndex)
        {
            var SymbolStartIndex = 0;
            var SymbolChars = new List<Char>();
            var ParamStartIndex = 0;
            var ParamChars = new List<Char>();
            var ParameterStrings = new List<KeyValuePair<String, int>>();
            var Output = new List<Symbol>();

            var Index = 0;
            Func<Boolean> EndOfString = () => Index >= s.Length;
            Func<Char> Peek1 = () => s[Index];
            Action Proceed = () => Index += 1;

            var State = 0;
            var Level = 0;

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfString())
                    {
                        InvalidCharIndex = Index;
                        return Optional<List<Symbol>>.Empty;
                    }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        Proceed();
                    }
                    else
                    {
                        SymbolStartIndex = Index;
                        State = 1;
                    }
                }
                else if (State == 1)
                {
                    if (EndOfString())
                    {
                        if (SymbolChars.Count == 0)
                        {
                            InvalidCharIndex = Index;
                            return Optional<List<Symbol>>.Empty;
                        }
                        else
                        {
                            State = 3;
                        }
                        continue;
                    }
                    var c = Peek1();
                    if (c == '<')
                    {
                        if (SymbolChars.Count == 0)
                        {
                            InvalidCharIndex = Index;
                            return Optional<List<Symbol>>.Empty;
                        }
                        else
                        {
                            State = 2;
                        }
                    }
                    else if (c == '>')
                    {
                        InvalidCharIndex = Index;
                        return Optional<List<Symbol>>.Empty;
                    }
                    else if (c == '.')
                    {
                        if (SymbolChars.Count == 0)
                        {
                            InvalidCharIndex = Index;
                            return Optional<List<Symbol>>.Empty;
                        }
                        else
                        {
                            State = 3;
                        }
                    }
                    else
                    {
                        SymbolChars.Add(c);
                        Proceed();
                    }
                }
                else if (State == 2)
                {
                    if (EndOfString())
                    {
                        InvalidCharIndex = Index;
                        return Optional<List<Symbol>>.Empty;
                    }
                    var c = Peek1();
                    if (c == '<')
                    {
                        if (Level > 0)
                        {
                            ParamChars.Add(c);
                        }
                        else
                        {
                            ParamStartIndex = Index + 1;
                        }
                        Level += 1;
                        Proceed();
                    }
                    else if (c == '>')
                    {
                        Level -= 1;
                        if (Level > 0)
                        {
                            ParamChars.Add(c);
                        }
                        else
                        {
                            var Param = new String(ParamChars.ToArray());
                            ParameterStrings.Add(new KeyValuePair<String, int>(Param.Trim(' '), ParamStartIndex + Param.TakeWhile(cc => cc == ' ').Count()));
                            ParamChars.Clear();
                            State = 3;
                        }
                        Proceed();
                    }
                    else if (c == ',')
                    {
                        if (Level == 1)
                        {
                            var Param = new String(ParamChars.ToArray());
                            ParameterStrings.Add(new KeyValuePair<String, int>(Param.Trim(' '), ParamStartIndex + Param.TakeWhile(cc => cc == ' ').Count()));
                            ParamChars.Clear();
                            ParamStartIndex = Index + 1;
                        }
                        else
                        {
                            ParamChars.Add(c);
                        }
                        Proceed();
                    }
                    else
                    {
                        ParamChars.Add(c);
                        Proceed();
                    }
                }
                else if (State == 3)
                {
                    Output.Add(new Symbol { Name = new String(SymbolChars.ToArray()), SymbolStartIndex = SymbolStartIndex, SymbolEndIndex = Index, NameStartIndex = SymbolStartIndex, NameEndIndex = SymbolStartIndex + SymbolChars.Count, Parameters = ParameterStrings });
                    SymbolChars = new List<Char>();
                    ParameterStrings = new List<KeyValuePair<String, int>>();
                    if (EndOfString()) { break; }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        State = 4;
                        Proceed();
                    }
                    else if (c == '.')
                    {
                        SymbolStartIndex = Index + 1;
                        State = 1;
                        Proceed();
                    }
                    else
                    {
                        InvalidCharIndex = Index;
                        return Optional<List<Symbol>>.Empty;
                    }
                }
                else if (State == 4)
                {
                    if (EndOfString()) { break; }
                    var c = Peek1();
                    if (c == ' ')
                    {
                        Proceed();
                    }
                    else
                    {
                        InvalidCharIndex = Index;
                        return Optional<List<Symbol>>.Empty;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            InvalidCharIndex = 0;
            return Output;
        }

        public static Optional<String> TryUnescapeSymbolName(String TypeString, out int InvalidCharIndex)
        {
            var Output = new List<Char>();

            var Index = 0;
            Func<Boolean> EndOfString = () => Index >= TypeString.Length;
            Func<Char> Peek1 = () => TypeString[Index];
            Func<int, String> Peek = n => TypeString.Substring(Index, Math.Min(n, TypeString.Length - Index));
            Action Proceed = () => Index += 1;
            Action<int> ProceedMultiple = n => Index += n;
            Func<String, int, Boolean> IsHex = (h, n) => (h.Length == n) && h.All(c => "0123456789ABCDEFabcdef".Contains(c));

            var State = 0;

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfString()) { break; }
                    var c = Peek1();
                    if (c == '{')
                    {
                        State = 1;
                        Proceed();
                    }
                    else if (c == '}')
                    {
                        InvalidCharIndex = Index;
                        return Optional<String>.Empty;
                    }
                    else
                    {
                        Output.Add(c);
                        Proceed();
                    }
                }
                else if (State == 1)
                {
                    if (EndOfString()) { break; }
                    var c = Peek1();
                    if (c == '{')
                    {
                        InvalidCharIndex = Index;
                        return Optional<String>.Empty;
                    }
                    else if (c == '}')
                    {
                        State = 0;
                        Proceed();
                    }
                    else if (c == '\\')
                    {
                        State = 2;
                        Proceed();
                    }
                    else
                    {
                        Output.Add(c);
                        Proceed();
                    }
                }
                else if (State == 2)
                {
                    if (EndOfString()) { break; }
                    var c = Peek1();
                    if (c == '0')
                    {
                        Output.Add('\0');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'a')
                    {
                        Output.Add('\a');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'b')
                    {
                        Output.Add('\b');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'f')
                    {
                        Output.Add('\f');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'n')
                    {
                        Output.Add('\n');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'r')
                    {
                        Output.Add('\r');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 't')
                    {
                        Output.Add('\t');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'v')
                    {
                        Output.Add('\v');
                        State = 1;
                        Proceed();
                    }
                    else if (c == 'x')
                    {
                        Proceed();
                        var Hex = Peek(2);
                        if (!IsHex(Hex, 2))
                        {
                            InvalidCharIndex = Index;
                            return Optional<String>.Empty;
                        }
                        ProceedMultiple(Hex.Length);
                        Output.Add(ChrW(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)));
                        State = 1;
                    }
                    else if (c == 'u')
                    {
                        Proceed();
                        var Hex = Peek(4);
                        if (!IsHex(Hex, 4))
                        {
                            InvalidCharIndex = Index;
                            return Optional<String>.Empty;
                        }
                        ProceedMultiple(Hex.Length);
                        Output.Add(ChrW(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)));
                        State = 1;
                    }
                    else if (c == 'U')
                    {
                        Proceed();
                        var Hex = Peek(5);
                        if (!IsHex(Hex, 5))
                        {
                            InvalidCharIndex = Index;
                            return Optional<String>.Empty;
                        }
                        ProceedMultiple(Hex.Length);
                        Output.AddRange(ChrQ(int.Parse(Hex, System.Globalization.NumberStyles.HexNumber)).ToString());
                        State = 1;
                    }
                    else
                    {
                        Output.Add(c);
                        State = 1;
                        Proceed();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            InvalidCharIndex = 0;
            return new String(Output.ToArray());
        }

        private static Regex rAttribute = new Regex(@"\[(?<Name>.*?)(:\s*(?<Params>.*?))?\]", RegexOptions.ExplicitCapture);
        public static DescriptionComposite DecomposeDescription(String Description)
        {
            var Attributes = new List<KeyValuePair<String, List<String>>>();
            var CurrentIndex = 0;
            var d = rAttribute.Replace(Description,
                m =>
                {
                    if (m.Index != CurrentIndex) { return m.Value; }
                    var Name = m.Result("${Name}");
                    var Params = m.Result("${Params}") ?? "";
                    var Parameters = Params == "" ? new List<String> { } : Params.Split(',').Select(p => p.Trim(' ')).ToList();
                    Attributes.Add(new KeyValuePair<String, List<String>>(Name, Parameters));
                    CurrentIndex = m.Index + m.Length;
                    return "";
                }
            );
            return new DescriptionComposite { Attributes = Attributes, Description = d };
        }

        private static Regex rIntLiteralDec = new Regex(@"^(?<Sign>[+-])?(?<Digits>[0-9]+)$", RegexOptions.ExplicitCapture);
        private static Regex rIntLiteralHex = new Regex(@"^(?<Sign>[+-])?0x(?<Digits>[0-9A-Fa-f]+)$", RegexOptions.ExplicitCapture);
        private static Regex rIntLiteralBin = new Regex(@"^(?<Sign>[+-])?0b(?<Digits>[01]+)$", RegexOptions.ExplicitCapture);
        private static Regex rFloatLiteral = new Regex(@"^[+-]?([0-9]+|\.[0-9]+|[0-9]+\.[0-9]+)([eE][+-][0-9]+)?$", RegexOptions.ExplicitCapture);
        public static Boolean IsIntLiteral(String s)
        {
            return rIntLiteralDec.IsMatch(s) || rIntLiteralHex.IsMatch(s) || rIntLiteralBin.IsMatch(s);
        }
        public static Boolean IsFloatLiteral(String s)
        {
            return rFloatLiteral.IsMatch(s);
        }
        public static Optional<Int64> TryParseInt64Literal(String s)
        {
            checked
            {
                var mDec = rIntLiteralDec.Match(s);
                if (mDec.Success)
                {
                    Int64 Result;
                    if (Int64.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Result))
                    {
                        return Result;
                    }
                    else
                    {
                        return Optional<Int64>.Empty;
                    }
                }
                var mHex = rIntLiteralHex.Match(s);
                if (mHex.Success)
                {
                    Func<Char, int> GetHexDigitValue = c =>
                    {
                        if ((c >= '0') && (c <= '9')) { return c - '0'; }
                        if ((c >= 'A') && (c <= 'F')) { return c - 'A' + 10; }
                        if ((c >= 'a') && (c <= 'f')) { return c - 'a' + 10; }
                        throw new InvalidOperationException();
                    };

                    Int64 Sign = mHex.Result("${Sign}") == "-" ? -1 : 1;
                    Int64 v = 0;
                    var Digits = mHex.Result("${Digits}").Select(d => GetHexDigitValue(d)).ToList();
                    if (Digits.Count > 16) { return Optional<Int64>.Empty; }
                    if (Digits.Count == 16)
                    {
                        var LeadingDigit = Digits.First();
                        if (Sign == -1)
                        {
                            if (LeadingDigit > 8) { return Optional<Int64>.Empty; }
                            if (LeadingDigit == 8)
                            {
                                if (Digits.Skip(1).Any(d => d != 0)) { return Optional<Int64>.Empty; }
                            }
                        }
                        else
                        {
                            if (LeadingDigit >= 8) { return Optional<Int64>.Empty; }
                        }
                    }
                    foreach (var d in Digits)
                    {
                        v = v * 16 + Sign * d;
                    }
                    return v;
                }
                var mBin = rIntLiteralBin.Match(s);
                if (mBin.Success)
                {
                    Func<Char, int> GetBinDigitValue = c =>
                    {
                        if ((c >= '0') && (c <= '1')) { return c - '0'; }
                        throw new InvalidOperationException();
                    };

                    Int64 Sign = mBin.Result("${Sign}") == "-" ? -1 : 1;
                    Int64 v = 0;
                    var Digits = mBin.Result("${Digits}").Select(d => GetBinDigitValue(d)).ToList();
                    if (Digits.Count > 64) { return Optional<Int64>.Empty; }
                    if (Digits.Count == 64)
                    {
                        var LeadingDigit = Digits.First();
                        if (Sign == -1)
                        {
                            if (LeadingDigit == 1)
                            {
                                if (Digits.Skip(1).Any(d => d != 0)) { return Optional<Int64>.Empty; }
                            }
                        }
                        else
                        {
                            if (LeadingDigit == 1) { return Optional<Int64>.Empty; }
                        }
                    }
                    foreach (var d in Digits)
                    {
                        v = v * 2 + Sign * d;
                    }
                    return v;
                }
                return Optional<Int64>.Empty;
            }
        }
        public static Optional<UInt64> TryParseUInt64Literal(String s)
        {
            checked
            {
                var mDec = rIntLiteralDec.Match(s);
                if (mDec.Success)
                {
                    UInt64 Result;
                    if (UInt64.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Result))
                    {
                        return Result;
                    }
                    else
                    {
                        return Optional<UInt64>.Empty;
                    }
                }
                var mHex = rIntLiteralHex.Match(s);
                if (mHex.Success)
                {
                    Func<Char, int> GetHexDigitValue = c =>
                    {
                        if ((c >= '0') && (c <= '9')) { return c - '0'; }
                        if ((c >= 'A') && (c <= 'F')) { return c - 'A' + 10; }
                        if ((c >= 'a') && (c <= 'f')) { return c - 'a' + 10; }
                        throw new InvalidOperationException();
                    };

                    if (mHex.Result("${Sign}") == "-") { return Optional<UInt64>.Empty; }
                    UInt64 v = 0;
                    var Digits = mHex.Result("${Digits}").Select(d => (UInt64)(GetHexDigitValue(d))).ToList();
                    if (Digits.Count > 16) { return Optional<UInt64>.Empty; }
                    foreach (var d in Digits)
                    {
                        v = (v << 4) | d;
                    }
                    return v;
                }
                var mBin = rIntLiteralBin.Match(s);
                if (mBin.Success)
                {
                    Func<Char, int> GetBinDigitValue = c =>
                    {
                        if ((c >= '0') && (c <= '1')) { return c - '0'; }
                        throw new InvalidOperationException();
                    };

                    if (mBin.Result("${Sign}") == "-") { return Optional<UInt64>.Empty; }
                    UInt64 v = 0;
                    var Digits = mBin.Result("${Digits}").Select(d => (UInt64)(GetBinDigitValue(d))).ToList();
                    if (Digits.Count > 64) { return Optional<UInt64>.Empty; }
                    foreach (var d in Digits)
                    {
                        v = (v << 1) | d;
                    }
                    return v;
                }
                return Optional<UInt64>.Empty;
            }
        }
        public static Optional<double> TryParseFloat64Literal(String s)
        {
            var Result = 0.0;
            if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Result))
            {
                return Result;
            }
            else
            {
                return Optional<double>.Empty;
            }
        }
    }
}
