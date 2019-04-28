//==========================================================================
//
//  File:        TokenParser.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 词法分析器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace Niveum.Json.Syntax
{
    public static class TokenParser
    {
        public static SyntaxRule ReadToken(PositionedTextReader r, Optional<Dictionary<Object, TextRange>> TextRanges)
        {
            int State = 0;
            var StartPosition = r.CurrentPosition;

            void Proceed()
            {
                r.Read();
            }
            Exception MakeIllegalChar()
            {
                var FilePath = r.FilePath;
                var Message = !r.EndOfText ? "IllegalChar '" + r.Peek() + "'" : "InvalidEndOfText";
                if (FilePath.OnSome)
                {
                    return new InvalidOperationException(FilePath.Value + r.CurrentPosition.ToString() + ": " + Message);
                }
                else
                {
                    return new InvalidOperationException(r.CurrentPosition.ToString() + ": " + Message);
                }
            }
            T MarkRange<T>(T Rule)
            {
                if (TextRanges.OnSome)
                {
                    var Range = new TextRange(StartPosition, r.CurrentPosition);
                    TextRanges.Value.Add(Rule, Range);
                }
                return Rule;
            }

            while (true)
            {
                var c = r.EndOfText ? default(Char) : r.Peek();
                if (State == 0)
                {
                    if (c == '"')
                    {
                        return ReadStringLiteral(r, TextRanges);
                    }
                    else if ((c == '-') || ((c >= '0') && (c <= '9')))
                    {
                        return ReadNumberLiteral(r, TextRanges);
                    }
                    else if (c == '{')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateLeftBrace());
                    }
                    else if (c == '}')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateRightBrace());
                    }
                    else if (c == '[')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateLeftBracket());
                    }
                    else if (c == ']')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateRightBracket());
                    }
                    else if (c == ':')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateColon());
                    }
                    else if (c == ',')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateComma());
                    }
                    else if (c == 't')
                    {
                        Proceed();
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'r') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'u') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'e') { throw MakeIllegalChar(); }
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateBooleanValue(true))));
                    }
                    else if (c == 'f')
                    {
                        Proceed();
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'a') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'l') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 's') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'e') { throw MakeIllegalChar(); }
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateBooleanValue(false))));
                    }
                    else if (c == 'n')
                    {
                        Proceed();
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'u') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'l') { throw MakeIllegalChar(); }
                        if (r.EndOfText) { throw MakeIllegalChar(); }
                        c = r.Read();
                        if (c != 'l') { throw MakeIllegalChar(); }
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateNullValue())));
                    }
                    else if ((c == '\t') || (c == '\n') || (c == '\r') || (c == ' '))
                    {
                        Proceed();
                        State = 1;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 1)
                {
                    if ((c == '\t') || (c == '\n') || (c == '\r') || (c == ' '))
                    {
                        Proceed();
                        State = 1;
                    }
                    else
                    {
                        return MarkRange(SyntaxRule.CreateWhitespace());
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public static SyntaxRule ReadNumberLiteral(PositionedTextReader r, Optional<Dictionary<Object, TextRange>> TextRanges)
        {
            int State = 0;
            var StartPosition = r.CurrentPosition;
            bool NegativeSign = false;
            double IntegerPart = 0;
            double FractionalPart = 0;
            double FractionalBase = 1;
            bool ExpNegativeSign = false;
            double ExpPart = 0;

            void Proceed()
            {
                r.Read();
            }
            Exception MakeIllegalChar()
            {
                var FilePath = r.FilePath;
                var Message = !r.EndOfText ? "IllegalChar '" + r.Peek() + "'" : "InvalidEndOfText";
                if (FilePath.OnSome)
                {
                    return new InvalidOperationException(FilePath.Value + r.CurrentPosition.ToString() + ": " + Message);
                }
                else
                {
                    return new InvalidOperationException(r.CurrentPosition.ToString() + ": " + Message);
                }
            }
            T MarkRange<T>(T Rule)
            {
                if (TextRanges.OnSome)
                {
                    var Range = new TextRange(StartPosition, r.CurrentPosition);
                    TextRanges.Value.Add(Rule, Range);
                }
                return Rule;
            }
            Func<double> GetResult = () =>
            {
                var d = ((NegativeSign ? -1 : 1) * (IntegerPart + FractionalPart / FractionalBase)) * Math.Pow(10, (ExpNegativeSign ? -1 : 1) * ExpPart);
                return d;
            };

            while (true)
            {
                var c = r.EndOfText ? default(Char) : r.Peek();
                if (State == 0)
                {
                    if (c == '-')
                    {
                        NegativeSign = true;
                        Proceed();
                        State = 1;
                    }
                    else if (c == '0')
                    {
                        IntegerPart = 0;
                        Proceed();
                        State = 2;
                    }
                    else if ((c >= '1') && (c <= '9'))
                    {
                        IntegerPart = IntegerPart * 10 + (c - '0');
                        Proceed();
                        State = 3;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 1)
                {
                    if (c == '0')
                    {
                        IntegerPart = 0;
                        Proceed();
                        State = 2;
                    }
                    else if ((c >= '1') && (c <= '9'))
                    {
                        IntegerPart = IntegerPart * 10 + (c - '0');
                        Proceed();
                        State = 3;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 2)
                {
                    if (c == '.')
                    {
                        Proceed();
                        State = 4;
                    }
                    else if ((c == 'e') || (c == 'E'))
                    {
                        Proceed();
                        State = 5;
                    }
                    else
                    {
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateNumberValue(GetResult()))));
                    }
                }
                else if ((State == 3) || (State == 6))
                {
                    if (c == '.')
                    {
                        Proceed();
                        State = 4;
                    }
                    else if ((c == 'e') || (c == 'E'))
                    {
                        Proceed();
                        State = 5;
                    }
                    else if ((c >= '0') && (c <= '9'))
                    {
                        IntegerPart = IntegerPart * 10 + (c - '0');
                        Proceed();
                        State = 6;
                    }
                    else
                    {
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateNumberValue(GetResult()))));
                    }
                }
                else if (State == 4)
                {
                    if ((c >= '0') && (c <= '9'))
                    {
                        FractionalPart = FractionalPart * 10 + (c - '0');
                        FractionalBase *= 10;
                        Proceed();
                        State = 7;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 5)
                {
                    if (c == '+')
                    {
                        ExpNegativeSign = false;
                        Proceed();
                        State = 8;
                    }
                    else if (c == '-')
                    {
                        ExpNegativeSign = true;
                        Proceed();
                        State = 8;
                    }
                    else if ((c >= '0') && (c <= '9'))
                    {
                        ExpPart = ExpPart * 10 + (c - '0');
                        Proceed();
                        State = 9;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 7)
                {
                    if ((c == 'e') || (c == 'E'))
                    {
                        Proceed();
                        State = 5;
                    }
                    else if ((c >= '0') && (c <= '9'))
                    {
                        FractionalPart = FractionalPart * 10 + (c - '0');
                        FractionalBase *= 10;
                        Proceed();
                        State = 7;
                    }
                    else
                    {
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateNumberValue(GetResult()))));
                    }
                }
                else if (State == 8)
                {
                    if ((c >= '0') && (c <= '9'))
                    {
                        ExpPart = ExpPart * 10 + (c - '0');
                        Proceed();
                        State = 9;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 9)
                {
                    if ((c >= '0') && (c <= '9'))
                    {
                        ExpPart = ExpPart * 10 + (c - '0');
                        Proceed();
                        State = 9;
                    }
                    else
                    {
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateNumberValue(GetResult()))));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public static SyntaxRule ReadStringLiteral(PositionedTextReader r, Optional<Dictionary<Object, TextRange>> TextRanges)
        {
            int State = 0;
            var StartPosition = r.CurrentPosition;
            var Chars = new StringBuilder();
            int Hex = 0;

            void Proceed()
            {
                r.Read();
            }
            Exception MakeIllegalChar()
            {
                var FilePath = r.FilePath;
                var Message = !r.EndOfText ? "IllegalChar '" + r.Peek() + "'" : "InvalidEndOfText";
                if (FilePath.OnSome)
                {
                    return new InvalidOperationException(FilePath.Value + r.CurrentPosition.ToString() + ": " + Message);
                }
                else
                {
                    return new InvalidOperationException(r.CurrentPosition.ToString() + ": " + Message);
                }
            }
            T MarkRange<T>(T Rule)
            {
                if (TextRanges.OnSome)
                {
                    var Range = new TextRange(StartPosition, r.CurrentPosition);
                    TextRanges.Value.Add(Rule, Range);
                }
                return Rule;
            };
            int? TryParseHex(Char c)
            {
                if ((c >= '0') && (c <= '9'))
                {
                    return c - '0';
                }
                else if ((c >= 'A') && (c <= 'F'))
                {
                    return c - 'A' + 10;
                }
                else if ((c >= 'a') && (c <= 'f'))
                {
                    return c - 'a' + 10;
                }
                else
                {
                    return null;
                }
            };

            while (true)
            {
                if (r.EndOfText) { throw MakeIllegalChar(); }
                var c = r.Peek();
                if (State == 0)
                {
                    if (c == '"')
                    {
                        Proceed();
                        State = 1;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if ((State == 1) || (State == 2) || (State == 4) || (State == 9))
                {
                    if (c == '"')
                    {
                        Proceed();
                        return MarkRange(SyntaxRule.CreateLiteral(MarkRange(TokenLiteral.CreateStringValue(Chars.ToString()))));
                    }
                    else if (c == '\\')
                    {
                        Proceed();
                        State = 3;
                    }
                    else if (c >= '\u0020')
                    {
                        Chars.Append(c);
                        Proceed();
                        State = 2;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 3)
                {
                    if (c == 'u')
                    {
                        Proceed();
                        State = 5;
                    }
                    else if (c == '"')
                    {
                        Chars.Append('"');
                        Proceed();
                        State = 4;
                    }
                    else if (c == '\\')
                    {
                        Chars.Append('\\');
                        Proceed();
                        State = 4;
                    }
                    else if (c == '/')
                    {
                        Chars.Append('/');
                        Proceed();
                        State = 4;
                    }
                    else if (c == 'b')
                    {
                        Chars.Append('\b');
                        Proceed();
                        State = 4;
                    }
                    else if (c == 'f')
                    {
                        Chars.Append('\f');
                        Proceed();
                        State = 4;
                    }
                    else if (c == 'n')
                    {
                        Chars.Append('\n');
                        Proceed();
                        State = 4;
                    }
                    else if (c == 'r')
                    {
                        Chars.Append('\r');
                        Proceed();
                        State = 4;
                    }
                    else if (c == 't')
                    {
                        Chars.Append('\t');
                        Proceed();
                        State = 4;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 5)
                {
                    if (TryParseHex(c) is int h)
                    {
                        Hex = h;
                        Proceed();
                        State = 6;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 6)
                {
                    if (TryParseHex(c) is int h)
                    {
                        Hex = Hex * 16 + h;
                        Proceed();
                        State = 7;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 7)
                {
                    if (TryParseHex(c) is int h)
                    {
                        Hex = Hex * 16 + h;
                        Proceed();
                        State = 8;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else if (State == 8)
                {
                    if (TryParseHex(c) is int h)
                    {
                        Hex = Hex * 16 + h;
                        Chars.Append((Char)(Hex));
                        Proceed();
                        State = 9;
                    }
                    else
                    {
                        throw MakeIllegalChar();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
