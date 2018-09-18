//==========================================================================
//
//  File:        SyntaxTest.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 文法测试
//  Version:     2018.09.19.
//  Copyright(C) F.R.C.
//
//==========================================================================

using Niveum.Json.Syntax;
using System;
using System.Collections.Generic;

namespace Niveum.Json.Tests
{
    public sealed class SyntaxTest
    {
        public static void DoTest()
        {
            Assert(SyntaxEquals(TokenParse(@"""123abc\u0020\t\"""""), SyntaxRule.CreateLiteral(TokenLiteral.CreateStringValue("123abc \t\""))));

            Assert(SyntaxEquals(TokenParse(@"0"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(0))));
            Assert(SyntaxEquals(TokenParse(@"-1000"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-1000))));
            Assert(SyntaxEquals(TokenParse(@"0.0"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(0))));
            Assert(SyntaxEquals(TokenParse(@"123.456"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(123.456))));
            Assert(SyntaxEquals(TokenParse(@"-10.1"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-10.1))));
            Assert(SyntaxEquals(TokenParse(@"100.001e10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(100.001e10))));
            Assert(SyntaxEquals(TokenParse(@"-100.00e+10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-100.00e+10))));
            Assert(SyntaxEquals(TokenParse(@"100.001e-10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(100.001e-10))));
            Assert(SyntaxEquals(TokenParse(@"1.23456789123456789123456789e10"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(1.23456789123456789123456789e10))));
            Assert(SyntaxEquals(TokenParse(@"1.79769e+308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(1.79769e+308))));
            Assert(SyntaxEquals(TokenParse(@"-1.79769e+308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-1.79769e+308))));
            Assert(SyntaxEquals(TokenParse(@"2.22507e-308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(2.22507e-308))));
            Assert(SyntaxEquals(TokenParse(@"-2.22507e-308"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNumberValue(-2.22507e-308))));

            Assert(SyntaxEquals(TokenParse(@"null"), SyntaxRule.CreateLiteral(TokenLiteral.CreateNullValue())));
            Assert(SyntaxEquals(TokenParse(@"false"), SyntaxRule.CreateLiteral(TokenLiteral.CreateBooleanValue(false))));
            Assert(SyntaxEquals(TokenParse(@"true"), SyntaxRule.CreateLiteral(TokenLiteral.CreateBooleanValue(true))));
            Assert(SyntaxEquals(TokenParse(@"{"), SyntaxRule.CreateLeftBrace()));
            Assert(SyntaxEquals(TokenParse(@"}"), SyntaxRule.CreateRightBrace()));
            Assert(SyntaxEquals(TokenParse(@"["), SyntaxRule.CreateLeftBracket()));
            Assert(SyntaxEquals(TokenParse(@"]"), SyntaxRule.CreateRightBracket()));
            Assert(SyntaxEquals(TokenParse(@":"), SyntaxRule.CreateColon()));
            Assert(SyntaxEquals(TokenParse(@","), SyntaxRule.CreateComma()));
            Assert(SyntaxEquals(TokenParse(" \t\r\n"), SyntaxRule.CreateWhitespace()));

            Assert(SyntaxEquals(SyntaxParse("null"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNullValue())));
            Assert(SyntaxEquals(SyntaxParse("false"), SyntaxValue.CreateLiteral(TokenLiteral.CreateBooleanValue(false))));
            Assert(SyntaxEquals(SyntaxParse("true"), SyntaxValue.CreateLiteral(TokenLiteral.CreateBooleanValue(true))));
            Assert(SyntaxEquals(SyntaxParse("123"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(123))));
            Assert(SyntaxEquals(SyntaxParse("\"123\""), SyntaxValue.CreateLiteral(TokenLiteral.CreateStringValue("123"))));
            Assert(SyntaxEquals(SyntaxParse(" null "), SyntaxValue.CreateLiteral(TokenLiteral.CreateNullValue())));
            Assert(SyntaxEquals(SyntaxParse(" false "), SyntaxValue.CreateLiteral(TokenLiteral.CreateBooleanValue(false))));
            Assert(SyntaxEquals(SyntaxParse(" true "), SyntaxValue.CreateLiteral(TokenLiteral.CreateBooleanValue(true))));
            Assert(SyntaxEquals(SyntaxParse(" 123 "), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(123))));
            Assert(SyntaxEquals(SyntaxParse(" \"123\" "), SyntaxValue.CreateLiteral(TokenLiteral.CreateStringValue("123"))));
            Assert(SyntaxEquals(SyntaxParse("{}"), SyntaxValue.CreateObject(new SyntaxObject { Members = Optional<SyntaxMembers>.Empty })));
            Assert(SyntaxEquals(SyntaxParse("[]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty })));
            Assert(SyntaxEquals(SyntaxParse("{ }"), SyntaxValue.CreateObject(new SyntaxObject { Members = Optional<SyntaxMembers>.Empty })));
            Assert(SyntaxEquals(SyntaxParse("[ ]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty })));
            Assert(SyntaxEquals(SyntaxParse(" { }"), SyntaxValue.CreateObject(new SyntaxObject { Members = Optional<SyntaxMembers>.Empty })));
            Assert(SyntaxEquals(SyntaxParse(" [ ]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty })));
            Assert(SyntaxEquals(SyntaxParse(" { } "), SyntaxValue.CreateObject(new SyntaxObject { Members = Optional<SyntaxMembers>.Empty })));
            Assert(SyntaxEquals(SyntaxParse(" [ ] "), SyntaxValue.CreateArray(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty })));
            Assert(SyntaxEquals(SyntaxParse("{\"key\":1}"), SyntaxValue.CreateObject(new SyntaxObject { Members = SyntaxMembers.CreateSingle(new Tuple<TokenLiteral, SyntaxValue>(TokenLiteral.CreateStringValue("key"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1)))) })));
            Assert(SyntaxEquals(SyntaxParse("[1]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = SyntaxElements.CreateSingle(SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1))) })));
            Assert(SyntaxEquals(SyntaxParse("{ \"key\" : 1 }"), SyntaxValue.CreateObject(new SyntaxObject { Members = SyntaxMembers.CreateSingle(new Tuple<TokenLiteral, SyntaxValue>(TokenLiteral.CreateStringValue("key"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1)))) })));
            Assert(SyntaxEquals(SyntaxParse("[ 1 ]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = SyntaxElements.CreateSingle(SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1))) })));
            Assert(SyntaxEquals(SyntaxParse("{\"key\":1,\"value\":2}"), SyntaxValue.CreateObject(new SyntaxObject { Members = SyntaxMembers.CreateMultiple(new Tuple<SyntaxMembers, TokenLiteral, SyntaxValue>(SyntaxMembers.CreateSingle(new Tuple<TokenLiteral, SyntaxValue>(TokenLiteral.CreateStringValue("key"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1)))), TokenLiteral.CreateStringValue("value"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(2)))) })));
            Assert(SyntaxEquals(SyntaxParse("[1,2]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = SyntaxElements.CreateMultiple(new Tuple<SyntaxElements, SyntaxValue>(SyntaxElements.CreateSingle(SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1))), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(2)))) })));
            Assert(SyntaxEquals(SyntaxParse("{ \"key\" : 1, \"value\" : 2 }"), SyntaxValue.CreateObject(new SyntaxObject { Members = SyntaxMembers.CreateMultiple(new Tuple<SyntaxMembers, TokenLiteral, SyntaxValue>(SyntaxMembers.CreateSingle(new Tuple<TokenLiteral, SyntaxValue>(TokenLiteral.CreateStringValue("key"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1)))), TokenLiteral.CreateStringValue("value"), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(2)))) })));
            Assert(SyntaxEquals(SyntaxParse("[ 1, 2 ]"), SyntaxValue.CreateArray(new SyntaxArray { Elements = SyntaxElements.CreateMultiple(new Tuple<SyntaxElements, SyntaxValue>(SyntaxElements.CreateSingle(SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(1))), SyntaxValue.CreateLiteral(TokenLiteral.CreateNumberValue(2)))) })));
        }

        [System.Diagnostics.DebuggerNonUserCode]
        private static void Assert(bool b)
        {
            if (!b) { throw new InvalidOperationException("Assertion Failed"); }
        }

        private static SyntaxRule TokenParse(String Text)
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
        private static SyntaxValue SyntaxParse(String Text)
        {
            var TextRanges = new Dictionary<Object, TextRange>();
            using (var sr = new System.IO.StringReader(Text))
            using (var ptr = new PositionedTextReader(Optional<String>.Empty, sr))
            {
                var t = SyntaxParser.ReadValue(ptr, TextRanges);
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