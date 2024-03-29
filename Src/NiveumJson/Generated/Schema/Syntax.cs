﻿//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

using System;
using System.Collections.Generic;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;
using UInt8 = System.Byte;
using UInt16 = System.UInt16;
using UInt32 = System.UInt32;
using UInt64 = System.UInt64;
using Int8 = System.SByte;
using Int16 = System.Int16;
using Int32 = System.Int32;
using Int64 = System.Int64;
using Float32 = System.Single;
using Float64 = System.Double;

namespace Niveum.Json.Syntax
{
    public enum TokenLiteralTag
    {
        /// <summary>空</summary>
        NullValue = 0,
        /// <summary>布尔</summary>
        BooleanValue = 1,
        /// <summary>实数</summary>
        NumberValue = 2,
        /// <summary>字符串</summary>
        StringValue = 3
    }
    /// <summary>字面量</summary>
    [TaggedUnion]
    public sealed class TokenLiteral
    {
        [Tag] public TokenLiteralTag _Tag;

        /// <summary>空</summary>
        public Unit NullValue;
        /// <summary>布尔</summary>
        public Boolean BooleanValue;
        /// <summary>实数</summary>
        public Real NumberValue;
        /// <summary>字符串</summary>
        public String StringValue;

        /// <summary>空</summary>
        public static TokenLiteral CreateNullValue() { return new TokenLiteral { _Tag = TokenLiteralTag.NullValue, NullValue = default(Unit) }; }
        /// <summary>布尔</summary>
        public static TokenLiteral CreateBooleanValue(Boolean Value) { return new TokenLiteral { _Tag = TokenLiteralTag.BooleanValue, BooleanValue = Value }; }
        /// <summary>实数</summary>
        public static TokenLiteral CreateNumberValue(Real Value) { return new TokenLiteral { _Tag = TokenLiteralTag.NumberValue, NumberValue = Value }; }
        /// <summary>字符串</summary>
        public static TokenLiteral CreateStringValue(String Value) { return new TokenLiteral { _Tag = TokenLiteralTag.StringValue, StringValue = Value }; }

        /// <summary>空</summary>
        public Boolean OnNullValue { get { return _Tag == TokenLiteralTag.NullValue; } }
        /// <summary>布尔</summary>
        public Boolean OnBooleanValue { get { return _Tag == TokenLiteralTag.BooleanValue; } }
        /// <summary>实数</summary>
        public Boolean OnNumberValue { get { return _Tag == TokenLiteralTag.NumberValue; } }
        /// <summary>字符串</summary>
        public Boolean OnStringValue { get { return _Tag == TokenLiteralTag.StringValue; } }
    }
    public enum SyntaxValueTag
    {
        /// <summary>字面量</summary>
        Literal = 0,
        /// <summary>对象字面量</summary>
        Object = 1,
        /// <summary>数组字面量</summary>
        Array = 2
    }
    /// <summary>值</summary>
    [TaggedUnion]
    public sealed class SyntaxValue
    {
        [Tag] public SyntaxValueTag _Tag;

        /// <summary>字面量</summary>
        public TokenLiteral Literal;
        /// <summary>对象字面量</summary>
        public SyntaxObject Object;
        /// <summary>数组字面量</summary>
        public SyntaxArray Array;

        /// <summary>字面量</summary>
        public static SyntaxValue CreateLiteral(TokenLiteral Value) { return new SyntaxValue { _Tag = SyntaxValueTag.Literal, Literal = Value }; }
        /// <summary>对象字面量</summary>
        public static SyntaxValue CreateObject(SyntaxObject Value) { return new SyntaxValue { _Tag = SyntaxValueTag.Object, Object = Value }; }
        /// <summary>数组字面量</summary>
        public static SyntaxValue CreateArray(SyntaxArray Value) { return new SyntaxValue { _Tag = SyntaxValueTag.Array, Array = Value }; }

        /// <summary>字面量</summary>
        public Boolean OnLiteral { get { return _Tag == SyntaxValueTag.Literal; } }
        /// <summary>对象字面量</summary>
        public Boolean OnObject { get { return _Tag == SyntaxValueTag.Object; } }
        /// <summary>数组字面量</summary>
        public Boolean OnArray { get { return _Tag == SyntaxValueTag.Array; } }
    }
    /// <summary>对象字面量</summary>
    [Record]
    public sealed class SyntaxObject
    {
        /// <summary>成员列表</summary>
        public Optional<SyntaxMembers> Members;
    }
    public enum SyntaxMembersTag
    {
        /// <summary>单个成员</summary>
        Single = 0,
        /// <summary>多个成员</summary>
        Multiple = 1
    }
    /// <summary>成员列表</summary>
    [TaggedUnion]
    public sealed class SyntaxMembers
    {
        [Tag] public SyntaxMembersTag _Tag;

        /// <summary>单个成员</summary>
        public Tuple<TokenLiteral, SyntaxValue> Single;
        /// <summary>多个成员</summary>
        public Tuple<SyntaxMembers, TokenLiteral, SyntaxValue> Multiple;

        /// <summary>单个成员</summary>
        public static SyntaxMembers CreateSingle(Tuple<TokenLiteral, SyntaxValue> Value) { return new SyntaxMembers { _Tag = SyntaxMembersTag.Single, Single = Value }; }
        /// <summary>多个成员</summary>
        public static SyntaxMembers CreateMultiple(Tuple<SyntaxMembers, TokenLiteral, SyntaxValue> Value) { return new SyntaxMembers { _Tag = SyntaxMembersTag.Multiple, Multiple = Value }; }

        /// <summary>单个成员</summary>
        public Boolean OnSingle { get { return _Tag == SyntaxMembersTag.Single; } }
        /// <summary>多个成员</summary>
        public Boolean OnMultiple { get { return _Tag == SyntaxMembersTag.Multiple; } }
    }
    /// <summary>数组字面量</summary>
    [Record]
    public sealed class SyntaxArray
    {
        /// <summary>元素列表</summary>
        public Optional<SyntaxElements> Elements;
    }
    public enum SyntaxElementsTag
    {
        /// <summary>单个元素</summary>
        Single = 0,
        /// <summary>多个元素</summary>
        Multiple = 1
    }
    /// <summary>元素列表</summary>
    [TaggedUnion]
    public sealed class SyntaxElements
    {
        [Tag] public SyntaxElementsTag _Tag;

        /// <summary>单个元素</summary>
        public SyntaxValue Single;
        /// <summary>多个元素</summary>
        public Tuple<SyntaxElements, SyntaxValue> Multiple;

        /// <summary>单个元素</summary>
        public static SyntaxElements CreateSingle(SyntaxValue Value) { return new SyntaxElements { _Tag = SyntaxElementsTag.Single, Single = Value }; }
        /// <summary>多个元素</summary>
        public static SyntaxElements CreateMultiple(Tuple<SyntaxElements, SyntaxValue> Value) { return new SyntaxElements { _Tag = SyntaxElementsTag.Multiple, Multiple = Value }; }

        /// <summary>单个元素</summary>
        public Boolean OnSingle { get { return _Tag == SyntaxElementsTag.Single; } }
        /// <summary>多个元素</summary>
        public Boolean OnMultiple { get { return _Tag == SyntaxElementsTag.Multiple; } }
    }
    public enum SyntaxRuleTag
    {
        /// <summary>字面量</summary>
        Literal = 0,
        /// <summary>左方括号</summary>
        LeftBracket = 1,
        /// <summary>右方括号</summary>
        RightBracket = 2,
        /// <summary>左花括号</summary>
        LeftBrace = 3,
        /// <summary>右花括号</summary>
        RightBrace = 4,
        /// <summary>冒号</summary>
        Colon = 5,
        /// <summary>逗号</summary>
        Comma = 6,
        /// <summary>空白</summary>
        Whitespace = 7,
        /// <summary>值</summary>
        Value = 8,
        /// <summary>对象</summary>
        Object = 9,
        /// <summary>成员</summary>
        Members = 10,
        /// <summary>数组</summary>
        Array = 11,
        /// <summary>元素</summary>
        Elements = 12
    }
    /// <summary>句法规则</summary>
    [TaggedUnion]
    public sealed class SyntaxRule
    {
        [Tag] public SyntaxRuleTag _Tag;

        /// <summary>字面量</summary>
        public TokenLiteral Literal;
        /// <summary>左方括号</summary>
        public Unit LeftBracket;
        /// <summary>右方括号</summary>
        public Unit RightBracket;
        /// <summary>左花括号</summary>
        public Unit LeftBrace;
        /// <summary>右花括号</summary>
        public Unit RightBrace;
        /// <summary>冒号</summary>
        public Unit Colon;
        /// <summary>逗号</summary>
        public Unit Comma;
        /// <summary>空白</summary>
        public Unit Whitespace;
        /// <summary>值</summary>
        public SyntaxValue Value;
        /// <summary>对象</summary>
        public SyntaxObject Object;
        /// <summary>成员</summary>
        public SyntaxMembers Members;
        /// <summary>数组</summary>
        public SyntaxArray Array;
        /// <summary>元素</summary>
        public SyntaxElements Elements;

        /// <summary>字面量</summary>
        public static SyntaxRule CreateLiteral(TokenLiteral Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Literal, Literal = Value }; }
        /// <summary>左方括号</summary>
        public static SyntaxRule CreateLeftBracket() { return new SyntaxRule { _Tag = SyntaxRuleTag.LeftBracket, LeftBracket = default(Unit) }; }
        /// <summary>右方括号</summary>
        public static SyntaxRule CreateRightBracket() { return new SyntaxRule { _Tag = SyntaxRuleTag.RightBracket, RightBracket = default(Unit) }; }
        /// <summary>左花括号</summary>
        public static SyntaxRule CreateLeftBrace() { return new SyntaxRule { _Tag = SyntaxRuleTag.LeftBrace, LeftBrace = default(Unit) }; }
        /// <summary>右花括号</summary>
        public static SyntaxRule CreateRightBrace() { return new SyntaxRule { _Tag = SyntaxRuleTag.RightBrace, RightBrace = default(Unit) }; }
        /// <summary>冒号</summary>
        public static SyntaxRule CreateColon() { return new SyntaxRule { _Tag = SyntaxRuleTag.Colon, Colon = default(Unit) }; }
        /// <summary>逗号</summary>
        public static SyntaxRule CreateComma() { return new SyntaxRule { _Tag = SyntaxRuleTag.Comma, Comma = default(Unit) }; }
        /// <summary>空白</summary>
        public static SyntaxRule CreateWhitespace() { return new SyntaxRule { _Tag = SyntaxRuleTag.Whitespace, Whitespace = default(Unit) }; }
        /// <summary>值</summary>
        public static SyntaxRule CreateValue(SyntaxValue Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Value, Value = Value }; }
        /// <summary>对象</summary>
        public static SyntaxRule CreateObject(SyntaxObject Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Object, Object = Value }; }
        /// <summary>成员</summary>
        public static SyntaxRule CreateMembers(SyntaxMembers Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Members, Members = Value }; }
        /// <summary>数组</summary>
        public static SyntaxRule CreateArray(SyntaxArray Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Array, Array = Value }; }
        /// <summary>元素</summary>
        public static SyntaxRule CreateElements(SyntaxElements Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Elements, Elements = Value }; }

        /// <summary>字面量</summary>
        public Boolean OnLiteral { get { return _Tag == SyntaxRuleTag.Literal; } }
        /// <summary>左方括号</summary>
        public Boolean OnLeftBracket { get { return _Tag == SyntaxRuleTag.LeftBracket; } }
        /// <summary>右方括号</summary>
        public Boolean OnRightBracket { get { return _Tag == SyntaxRuleTag.RightBracket; } }
        /// <summary>左花括号</summary>
        public Boolean OnLeftBrace { get { return _Tag == SyntaxRuleTag.LeftBrace; } }
        /// <summary>右花括号</summary>
        public Boolean OnRightBrace { get { return _Tag == SyntaxRuleTag.RightBrace; } }
        /// <summary>冒号</summary>
        public Boolean OnColon { get { return _Tag == SyntaxRuleTag.Colon; } }
        /// <summary>逗号</summary>
        public Boolean OnComma { get { return _Tag == SyntaxRuleTag.Comma; } }
        /// <summary>空白</summary>
        public Boolean OnWhitespace { get { return _Tag == SyntaxRuleTag.Whitespace; } }
        /// <summary>值</summary>
        public Boolean OnValue { get { return _Tag == SyntaxRuleTag.Value; } }
        /// <summary>对象</summary>
        public Boolean OnObject { get { return _Tag == SyntaxRuleTag.Object; } }
        /// <summary>成员</summary>
        public Boolean OnMembers { get { return _Tag == SyntaxRuleTag.Members; } }
        /// <summary>数组</summary>
        public Boolean OnArray { get { return _Tag == SyntaxRuleTag.Array; } }
        /// <summary>元素</summary>
        public Boolean OnElements { get { return _Tag == SyntaxRuleTag.Elements; } }
    }
}
