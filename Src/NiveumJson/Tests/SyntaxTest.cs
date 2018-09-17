//==========================================================================
//
//  File:        SyntaxTest.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 文法测试
//  Version:     2018.09.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using Niveum.Json.Syntax;

namespace Niveum.Json.Tests
{
    public sealed class SyntaxTest
    {
        public static void DoTest()
        {
            Assert(SyntaxEquals(SyntaxParse(@"""123abc\u0020\t\"""""), SyntaxRule.CreateLiteral(TokenLiteral.CreateStringValue("123abc \t\""))));

            Assert(SyntaxEquals(SyntaxParse(@"0"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(0))));
            Assert(SyntaxEquals(SyntaxParse(@"-1000"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-1000))));
            Assert(SyntaxEquals(SyntaxParse(@"0.0"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(0))));
            Assert(SyntaxEquals(SyntaxParse(@"123.456"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(123.456))));
            Assert(SyntaxEquals(SyntaxParse(@"-10.1"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-10.1))));
            Assert(SyntaxEquals(SyntaxParse(@"100.001e10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(100.001e10))));
            Assert(SyntaxEquals(SyntaxParse(@"-100.00e+10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-100.00e+10))));
            Assert(SyntaxEquals(SyntaxParse(@"100.001e-10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(100.001e-10))));
            Assert(SyntaxEquals(SyntaxParse(@"1.23456789123456789123456789e10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(1.23456789123456789123456789e10))));
            Assert(SyntaxEquals(SyntaxParse(@"1.79769e+308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(1.79769e+308))));
            Assert(SyntaxEquals(SyntaxParse(@"-1.79769e+308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-1.79769e+308))));
            Assert(SyntaxEquals(SyntaxParse(@"2.22507e-308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(2.22507e-308))));
            Assert(SyntaxEquals(SyntaxParse(@"-2.22507e-308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-2.22507e-308))));

            Assert(SyntaxEquals(SyntaxParse(@"null"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNullValue())));
            Assert(SyntaxEquals(SyntaxParse(@"false"), SyntaxRule.CreateLiteral(TokenLiteral.CreateBooleanValue(false))));
            Assert(SyntaxEquals(SyntaxParse(@"true"), SyntaxRule.CreateLiteral(TokenLiteral.CreateBooleanValue(true))));
            Assert(SyntaxEquals(SyntaxParse(@"{"), SyntaxRule.CreateLeftBrace()));
            Assert(SyntaxEquals(SyntaxParse(@"}"), SyntaxRule.CreateRightBrace()));
            Assert(SyntaxEquals(SyntaxParse(@"["), SyntaxRule.CreateLeftBracket()));
            Assert(SyntaxEquals(SyntaxParse(@"]"), SyntaxRule.CreateRightBracket()));
            Assert(SyntaxEquals(SyntaxParse(@":"), SyntaxRule.CreateColon()));
            Assert(SyntaxEquals(SyntaxParse(@","), SyntaxRule.CreateComma()));
            Assert(SyntaxEquals(SyntaxParse(" \t\r\n"), SyntaxRule.CreateWhitespace()));
        }

        private static void Assert(bool b)
        {
            if (!b) { throw new InvalidOperationException("Assertion Failed"); }
        }

        private static SyntaxRule SyntaxParse(String Text)
        {
            var TextRanges = new Dictionary<Object, TextRange>();
            using (var sr = new System.IO.StringReader(Text))
            using (var ptr = new PositionedTextReader(Optional<String>.Empty, sr))
            {
                var t = TokenParser.ReadToken(ptr, TextRanges);
                if (!ptr.EndOfText) { throw new InvalidOperationException(); }
                return t;
            }
        }
        private static bool SyntaxEquals(SyntaxRule Left, SyntaxRule Right)
        {
            if (Left._Tag != Right._Tag) { return false; }
            if (Left.OnLiteral)
            {
                return SyntaxEquals(Left.Literal, Right.Literal);
            }
            else if (Left.OnLeftBracket || Left.OnRightBracket || Left.OnLeftBrace || Left.OnRightBrace || Left.OnColon || Left.OnComma || Left.OnWhitespace)
            {
                return true;
            }
            else if (Left.OnValue)
            {
                return SyntaxEquals(Left.Value, Right.Value);
            }
            else if (Left.OnObject)
            {
                return SyntaxEquals(Left.Object, Right.Object);
            }
            else if (Left.OnMembers)
            {
                return SyntaxEquals(Left.Members, Right.Members);
            }
            else if (Left.OnArray)
            {
                return SyntaxEquals(Left.Array, Right.Array);
            }
            else if (Left.OnElements)
            {
                return SyntaxEquals(Left.Elements, Right.Elements);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static bool SyntaxEquals(TokenLiteral Left, TokenLiteral Right)
        {
            if (Left._Tag != Right._Tag) { return false; }
            if (Left.OnNullValue)
            {
                return true;
            }
            else if (Left.OnBooleanValue)
            {
                return Left.BooleanValue == Right.BooleanValue;
            }
            else if (Left.OnNumberValue)
            {
                return Left.NumberValue == Right.NumberValue;
            }
            else if (Left.OnStringValue)
            {
                return Left.StringValue == Right.StringValue;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static bool SyntaxEquals(SyntaxValue Left, SyntaxValue Right)
        {
            if (Left._Tag != Right._Tag) { return false; }
            if (Left.OnLiteral)
            {
                return SyntaxEquals(Left.Literal, Right.Literal);
            }
            else if (Left.OnObject)
            {
                return SyntaxEquals(Left.Object, Right.Object);
            }
            else if (Left.OnArray)
            {
                return SyntaxEquals(Left.Array, Right.Array);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static bool SyntaxEquals(SyntaxObject Left, SyntaxObject Right)
        {
            if (Left.Members.OnNotHasValue && Right.Members.OnNotHasValue) { return true; }
            if (Left.Members.OnNotHasValue || Right.Members.OnNotHasValue) { return false; }
            return SyntaxEquals(Left.Members.Value, Right.Members.Value);
        }
        private static bool SyntaxEquals(SyntaxMembers Left, SyntaxMembers Right)
        {
            if (Left._Tag != Right._Tag) { return false; }
            if (Left.OnSingle)
            {
                return SyntaxEquals(Left.Single.Item1, Right.Single.Item1) && SyntaxEquals(Left.Single.Item2, Right.Single.Item2);
            }
            else if (Left.OnMultiple)
            {
                return SyntaxEquals(Left.Multiple.Item1, Right.Multiple.Item1) && SyntaxEquals(Left.Multiple.Item2, Right.Multiple.Item2) && SyntaxEquals(Left.Multiple.Item3, Right.Multiple.Item3);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static bool SyntaxEquals(SyntaxArray Left, SyntaxArray Right)
        {
            if (Left.Elements.OnNotHasValue && Right.Elements.OnNotHasValue) { return true; }
            if (Left.Elements.OnNotHasValue || Right.Elements.OnNotHasValue) { return false; }
            return SyntaxEquals(Left.Elements.Value, Right.Elements.Value);
        }
        private static bool SyntaxEquals(SyntaxElements Left, SyntaxElements Right)
        {
            if (Left._Tag != Right._Tag) { return false; }
            if (Left.OnSingle)
            {
                return SyntaxEquals(Left.Single, Right.Single);
            }
            else if (Left.OnMultiple)
            {
                return SyntaxEquals(Left.Multiple.Item1, Right.Multiple.Item1) && SyntaxEquals(Left.Multiple.Item2, Right.Multiple.Item2);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}