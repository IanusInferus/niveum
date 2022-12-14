﻿//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

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

namespace Niveum.ExpressionSchema
{
    public enum TokenLiteralTag
    {
        /// <summary>布尔类型</summary>
        BooleanValue = 0,
        /// <summary>整数类型</summary>
        IntValue = 1,
        /// <summary>实数类型</summary>
        RealValue = 2
    }
    /// <summary>字面量</summary>
    [TaggedUnion]
    public sealed class TokenLiteral
    {
        [Tag] public TokenLiteralTag _Tag { get; init; }

        /// <summary>布尔类型</summary>
        public Boolean BooleanValue { get; init; }
        /// <summary>整数类型</summary>
        public Int IntValue { get; init; }
        /// <summary>实数类型</summary>
        public Real RealValue { get; init; }

        /// <summary>布尔类型</summary>
        public static TokenLiteral CreateBooleanValue(Boolean Value) { return new TokenLiteral { _Tag = TokenLiteralTag.BooleanValue, BooleanValue = Value }; }
        /// <summary>整数类型</summary>
        public static TokenLiteral CreateIntValue(Int Value) { return new TokenLiteral { _Tag = TokenLiteralTag.IntValue, IntValue = Value }; }
        /// <summary>实数类型</summary>
        public static TokenLiteral CreateRealValue(Real Value) { return new TokenLiteral { _Tag = TokenLiteralTag.RealValue, RealValue = Value }; }

        /// <summary>布尔类型</summary>
        public Boolean OnBooleanValue { get { return _Tag == TokenLiteralTag.BooleanValue; } }
        /// <summary>整数类型</summary>
        public Boolean OnIntValue { get { return _Tag == TokenLiteralTag.IntValue; } }
        /// <summary>实数类型</summary>
        public Boolean OnRealValue { get { return _Tag == TokenLiteralTag.RealValue; } }
    }
    /// <summary>标识符</summary>
    [Record]
    public sealed class TokenIdentifier
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
    }
    /// <summary>二目运算符</summary>
    [Record]
    public sealed class TokenBinaryOperator
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
    }
    /// <summary>单目运算符</summary>
    [Record]
    public sealed class TokenUnaryOperator
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
    }
    /// <summary>左小括号</summary>
    [Record]
    public sealed class TokenLeftParen
    {
    }
    /// <summary>右小括号</summary>
    [Record]
    public sealed class TokenRightParen
    {
    }
    /// <summary>逗号</summary>
    [Record]
    public sealed class TokenComma
    {
    }
    public enum SyntaxExprTag
    {
        /// <summary>字面量</summary>
        Literal = 0,
        /// <summary>函数</summary>
        Function = 1,
        /// <summary>单个变量</summary>
        Variable = 2,
        /// <summary>括号</summary>
        Paren = 3,
        /// <summary>前缀单目运算</summary>
        UnaryOperator = 4,
        /// <summary>中缀二目运算</summary>
        BinaryOperator = 5
    }
    /// <summary>表达式</summary>
    [TaggedUnion]
    public sealed class SyntaxExpr
    {
        [Tag] public SyntaxExprTag _Tag { get; init; }

        /// <summary>字面量</summary>
        public ProductionLiteral Literal { get; init; }
        /// <summary>函数</summary>
        public ProductionFunction Function { get; init; }
        /// <summary>单个变量</summary>
        public ProductionVariable Variable { get; init; }
        /// <summary>括号</summary>
        public ProductionParen Paren { get; init; }
        /// <summary>前缀单目运算</summary>
        public ProductionUnaryOperator UnaryOperator { get; init; }
        /// <summary>中缀二目运算</summary>
        public ProductionBinaryOperator BinaryOperator { get; init; }

        /// <summary>字面量</summary>
        public static SyntaxExpr CreateLiteral(ProductionLiteral Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.Literal, Literal = Value }; }
        /// <summary>函数</summary>
        public static SyntaxExpr CreateFunction(ProductionFunction Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.Function, Function = Value }; }
        /// <summary>单个变量</summary>
        public static SyntaxExpr CreateVariable(ProductionVariable Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.Variable, Variable = Value }; }
        /// <summary>括号</summary>
        public static SyntaxExpr CreateParen(ProductionParen Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.Paren, Paren = Value }; }
        /// <summary>前缀单目运算</summary>
        public static SyntaxExpr CreateUnaryOperator(ProductionUnaryOperator Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.UnaryOperator, UnaryOperator = Value }; }
        /// <summary>中缀二目运算</summary>
        public static SyntaxExpr CreateBinaryOperator(ProductionBinaryOperator Value) { return new SyntaxExpr { _Tag = SyntaxExprTag.BinaryOperator, BinaryOperator = Value }; }

        /// <summary>字面量</summary>
        public Boolean OnLiteral { get { return _Tag == SyntaxExprTag.Literal; } }
        /// <summary>函数</summary>
        public Boolean OnFunction { get { return _Tag == SyntaxExprTag.Function; } }
        /// <summary>单个变量</summary>
        public Boolean OnVariable { get { return _Tag == SyntaxExprTag.Variable; } }
        /// <summary>括号</summary>
        public Boolean OnParen { get { return _Tag == SyntaxExprTag.Paren; } }
        /// <summary>前缀单目运算</summary>
        public Boolean OnUnaryOperator { get { return _Tag == SyntaxExprTag.UnaryOperator; } }
        /// <summary>中缀二目运算</summary>
        public Boolean OnBinaryOperator { get { return _Tag == SyntaxExprTag.BinaryOperator; } }
    }
    public enum SyntaxParameterListTag
    {
        /// <summary>空参数列表</summary>
        Null = 0,
        /// <summary>非空参数列表</summary>
        Nonnull = 1
    }
    /// <summary>参数列表</summary>
    [TaggedUnion]
    public sealed class SyntaxParameterList
    {
        [Tag] public SyntaxParameterListTag _Tag { get; init; }

        /// <summary>空参数列表</summary>
        public ProductionNullParameterList Null { get; init; }
        /// <summary>非空参数列表</summary>
        public ProductionNonnullParameterList Nonnull { get; init; }

        /// <summary>空参数列表</summary>
        public static SyntaxParameterList CreateNull(ProductionNullParameterList Value) { return new SyntaxParameterList { _Tag = SyntaxParameterListTag.Null, Null = Value }; }
        /// <summary>非空参数列表</summary>
        public static SyntaxParameterList CreateNonnull(ProductionNonnullParameterList Value) { return new SyntaxParameterList { _Tag = SyntaxParameterListTag.Nonnull, Nonnull = Value }; }

        /// <summary>空参数列表</summary>
        public Boolean OnNull { get { return _Tag == SyntaxParameterListTag.Null; } }
        /// <summary>非空参数列表</summary>
        public Boolean OnNonnull { get { return _Tag == SyntaxParameterListTag.Nonnull; } }
    }
    public enum SyntaxNonnullParameterListTag
    {
        /// <summary>单个参数列表</summary>
        Single = 0,
        /// <summary>多个参数列表</summary>
        Multiple = 1
    }
    /// <summary>非空参数列表</summary>
    [TaggedUnion]
    public sealed class SyntaxNonnullParameterList
    {
        [Tag] public SyntaxNonnullParameterListTag _Tag { get; init; }

        /// <summary>单个参数列表</summary>
        public ProductionSingleParameterList Single { get; init; }
        /// <summary>多个参数列表</summary>
        public ProductionMultipleParameterList Multiple { get; init; }

        /// <summary>单个参数列表</summary>
        public static SyntaxNonnullParameterList CreateSingle(ProductionSingleParameterList Value) { return new SyntaxNonnullParameterList { _Tag = SyntaxNonnullParameterListTag.Single, Single = Value }; }
        /// <summary>多个参数列表</summary>
        public static SyntaxNonnullParameterList CreateMultiple(ProductionMultipleParameterList Value) { return new SyntaxNonnullParameterList { _Tag = SyntaxNonnullParameterListTag.Multiple, Multiple = Value }; }

        /// <summary>单个参数列表</summary>
        public Boolean OnSingle { get { return _Tag == SyntaxNonnullParameterListTag.Single; } }
        /// <summary>多个参数列表</summary>
        public Boolean OnMultiple { get { return _Tag == SyntaxNonnullParameterListTag.Multiple; } }
    }
    /// <summary>字面量</summary>
    [Record]
    public sealed class ProductionLiteral
    {
        /// <summary>字面量</summary>
        public TokenLiteral Literal { get; init; }
    }
    /// <summary>函数</summary>
    [Record]
    public sealed class ProductionFunction
    {
        /// <summary>标识符</summary>
        public TokenIdentifier Identifier { get; init; }
        /// <summary>参数列表</summary>
        public SyntaxParameterList ParameterList { get; init; }
    }
    /// <summary>单个变量</summary>
    [Record]
    public sealed class ProductionVariable
    {
        /// <summary>标识符</summary>
        public TokenIdentifier Identifier { get; init; }
    }
    /// <summary>括号</summary>
    [Record]
    public sealed class ProductionParen
    {
        /// <summary>表达式</summary>
        public SyntaxExpr Expr { get; init; }
    }
    /// <summary>前缀单目运算</summary>
    [Record]
    public sealed class ProductionUnaryOperator
    {
        /// <summary>单目运算符</summary>
        public TokenUnaryOperator UnaryOperator { get; init; }
        /// <summary>表达式</summary>
        public SyntaxExpr Expr { get; init; }
    }
    /// <summary>中缀二目运算</summary>
    [Record]
    public sealed class ProductionBinaryOperator
    {
        /// <summary>二目运算符</summary>
        public TokenBinaryOperator BinaryOperator { get; init; }
        /// <summary>表达式</summary>
        public SyntaxExpr Left { get; init; }
        /// <summary>表达式</summary>
        public SyntaxExpr Right { get; init; }
    }
    /// <summary>空参数列表</summary>
    [Record]
    public sealed class ProductionNullParameterList
    {
    }
    /// <summary>非空参数列表</summary>
    [Record]
    public sealed class ProductionNonnullParameterList
    {
        /// <summary>非空参数列表</summary>
        public SyntaxNonnullParameterList NonnullParameterList { get; init; }
    }
    /// <summary>单个参数列表</summary>
    [Record]
    public sealed class ProductionSingleParameterList
    {
        /// <summary>表达式</summary>
        public SyntaxExpr Expr { get; init; }
    }
    /// <summary>多个参数列表</summary>
    [Record]
    public sealed class ProductionMultipleParameterList
    {
        /// <summary>非空参数列表</summary>
        public SyntaxNonnullParameterList NonnullParameterList { get; init; }
        /// <summary>表达式</summary>
        public SyntaxExpr Expr { get; init; }
    }
    public enum SyntaxRuleTag
    {
        /// <summary>字面量</summary>
        Literal = 0,
        /// <summary>标识符</summary>
        Identifier = 1,
        /// <summary>二目运算符</summary>
        BinaryOperator = 2,
        /// <summary>单目运算符</summary>
        UnaryOperator = 3,
        /// <summary>左小括号</summary>
        LeftParen = 4,
        /// <summary>右小括号</summary>
        RightParen = 5,
        /// <summary>逗号</summary>
        Comma = 6,
        /// <summary>表达式</summary>
        Expr = 7,
        /// <summary>参数列表</summary>
        ParameterList = 8,
        /// <summary>非空参数列表</summary>
        NonnullParameterList = 9
    }
    /// <summary>句法规则</summary>
    [TaggedUnion]
    public sealed class SyntaxRule
    {
        [Tag] public SyntaxRuleTag _Tag { get; init; }

        /// <summary>字面量</summary>
        public TokenLiteral Literal { get; init; }
        /// <summary>标识符</summary>
        public TokenIdentifier Identifier { get; init; }
        /// <summary>二目运算符</summary>
        public TokenBinaryOperator BinaryOperator { get; init; }
        /// <summary>单目运算符</summary>
        public TokenUnaryOperator UnaryOperator { get; init; }
        /// <summary>左小括号</summary>
        public TokenLeftParen LeftParen { get; init; }
        /// <summary>右小括号</summary>
        public TokenRightParen RightParen { get; init; }
        /// <summary>逗号</summary>
        public TokenComma Comma { get; init; }
        /// <summary>表达式</summary>
        public SyntaxExpr Expr { get; init; }
        /// <summary>参数列表</summary>
        public SyntaxParameterList ParameterList { get; init; }
        /// <summary>非空参数列表</summary>
        public SyntaxNonnullParameterList NonnullParameterList { get; init; }

        /// <summary>字面量</summary>
        public static SyntaxRule CreateLiteral(TokenLiteral Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Literal, Literal = Value }; }
        /// <summary>标识符</summary>
        public static SyntaxRule CreateIdentifier(TokenIdentifier Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Identifier, Identifier = Value }; }
        /// <summary>二目运算符</summary>
        public static SyntaxRule CreateBinaryOperator(TokenBinaryOperator Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.BinaryOperator, BinaryOperator = Value }; }
        /// <summary>单目运算符</summary>
        public static SyntaxRule CreateUnaryOperator(TokenUnaryOperator Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.UnaryOperator, UnaryOperator = Value }; }
        /// <summary>左小括号</summary>
        public static SyntaxRule CreateLeftParen(TokenLeftParen Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.LeftParen, LeftParen = Value }; }
        /// <summary>右小括号</summary>
        public static SyntaxRule CreateRightParen(TokenRightParen Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.RightParen, RightParen = Value }; }
        /// <summary>逗号</summary>
        public static SyntaxRule CreateComma(TokenComma Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Comma, Comma = Value }; }
        /// <summary>表达式</summary>
        public static SyntaxRule CreateExpr(SyntaxExpr Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.Expr, Expr = Value }; }
        /// <summary>参数列表</summary>
        public static SyntaxRule CreateParameterList(SyntaxParameterList Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.ParameterList, ParameterList = Value }; }
        /// <summary>非空参数列表</summary>
        public static SyntaxRule CreateNonnullParameterList(SyntaxNonnullParameterList Value) { return new SyntaxRule { _Tag = SyntaxRuleTag.NonnullParameterList, NonnullParameterList = Value }; }

        /// <summary>字面量</summary>
        public Boolean OnLiteral { get { return _Tag == SyntaxRuleTag.Literal; } }
        /// <summary>标识符</summary>
        public Boolean OnIdentifier { get { return _Tag == SyntaxRuleTag.Identifier; } }
        /// <summary>二目运算符</summary>
        public Boolean OnBinaryOperator { get { return _Tag == SyntaxRuleTag.BinaryOperator; } }
        /// <summary>单目运算符</summary>
        public Boolean OnUnaryOperator { get { return _Tag == SyntaxRuleTag.UnaryOperator; } }
        /// <summary>左小括号</summary>
        public Boolean OnLeftParen { get { return _Tag == SyntaxRuleTag.LeftParen; } }
        /// <summary>右小括号</summary>
        public Boolean OnRightParen { get { return _Tag == SyntaxRuleTag.RightParen; } }
        /// <summary>逗号</summary>
        public Boolean OnComma { get { return _Tag == SyntaxRuleTag.Comma; } }
        /// <summary>表达式</summary>
        public Boolean OnExpr { get { return _Tag == SyntaxRuleTag.Expr; } }
        /// <summary>参数列表</summary>
        public Boolean OnParameterList { get { return _Tag == SyntaxRuleTag.ParameterList; } }
        /// <summary>非空参数列表</summary>
        public Boolean OnNonnullParameterList { get { return _Tag == SyntaxRuleTag.NonnullParameterList; } }
    }
}
