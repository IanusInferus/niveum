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
    /// <summary>表达式结构</summary>
    [Record]
    public sealed class Schema
    {
        /// <summary>模块声明</summary>
        public List<ModuleDecl> Modules { get; init; }
        /// <summary>命名空间导入</summary>
        public List<String> Imports { get; init; }
    }
    /// <summary>模块声明</summary>
    [Record]
    public sealed class ModuleDecl
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
        /// <summary>函数声明</summary>
        public List<FunctionDecl> Functions { get; init; }
    }
    /// <summary>函数集</summary>
    [Record]
    public sealed class Assembly
    {
        /// <summary>散列</summary>
        public UInt64 Hash { get; init; }
        /// <summary>函数定义</summary>
        public List<ModuleDef> Modules { get; init; }
    }
    /// <summary>模块定义</summary>
    [Record]
    public sealed class ModuleDef
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
        /// <summary>函数定义</summary>
        public List<FunctionDef> Functions { get; init; }
    }
    /// <summary>函数声明</summary>
    [Record]
    public sealed class FunctionDecl
    {
        /// <summary>函数名</summary>
        public String Name { get; init; }
        /// <summary>参数</summary>
        public List<VariableDef> Parameters { get; init; }
        /// <summary>返回值类型</summary>
        public PrimitiveType ReturnValue { get; init; }
    }
    /// <summary>函数定义</summary>
    [Record]
    public sealed class FunctionDef
    {
        /// <summary>函数名</summary>
        public String Name { get; init; }
        /// <summary>参数</summary>
        public List<VariableDef> Parameters { get; init; }
        /// <summary>返回值类型</summary>
        public PrimitiveType ReturnValue { get; init; }
        /// <summary>函数体</summary>
        public Expr Body { get; init; }
    }
    /// <summary>变量</summary>
    [Record]
    public sealed class VariableDef
    {
        /// <summary>变量名</summary>
        public String Name { get; init; }
        /// <summary>类型</summary>
        public PrimitiveType Type { get; init; }
    }
    public enum ExprTag
    {
        /// <summary>字面量表达式</summary>
        Literal = 0,
        /// <summary>变量表达式</summary>
        Variable = 1,
        /// <summary>函数表达式</summary>
        Function = 2,
        /// <summary>if伪函数表达式</summary>
        If = 3,
        /// <summary>&amp;&amp;运算符表达式</summary>
        AndAlso = 4,
        /// <summary>||运算符表达式</summary>
        OrElse = 5
    }
    /// <summary>表达式</summary>
    [TaggedUnion]
    public sealed class Expr
    {
        [Tag] public ExprTag _Tag { get; init; }

        /// <summary>字面量表达式</summary>
        public LiteralExpr Literal { get; init; }
        /// <summary>变量表达式</summary>
        public VariableExpr Variable { get; init; }
        /// <summary>函数表达式</summary>
        public FunctionExpr Function { get; init; }
        /// <summary>if伪函数表达式</summary>
        public IfExpr If { get; init; }
        /// <summary>&amp;&amp;运算符表达式</summary>
        public AndAlsoExpr AndAlso { get; init; }
        /// <summary>||运算符表达式</summary>
        public OrElseExpr OrElse { get; init; }

        /// <summary>字面量表达式</summary>
        public static Expr CreateLiteral(LiteralExpr Value) { return new Expr { _Tag = ExprTag.Literal, Literal = Value }; }
        /// <summary>变量表达式</summary>
        public static Expr CreateVariable(VariableExpr Value) { return new Expr { _Tag = ExprTag.Variable, Variable = Value }; }
        /// <summary>函数表达式</summary>
        public static Expr CreateFunction(FunctionExpr Value) { return new Expr { _Tag = ExprTag.Function, Function = Value }; }
        /// <summary>if伪函数表达式</summary>
        public static Expr CreateIf(IfExpr Value) { return new Expr { _Tag = ExprTag.If, If = Value }; }
        /// <summary>&amp;&amp;运算符表达式</summary>
        public static Expr CreateAndAlso(AndAlsoExpr Value) { return new Expr { _Tag = ExprTag.AndAlso, AndAlso = Value }; }
        /// <summary>||运算符表达式</summary>
        public static Expr CreateOrElse(OrElseExpr Value) { return new Expr { _Tag = ExprTag.OrElse, OrElse = Value }; }

        /// <summary>字面量表达式</summary>
        public Boolean OnLiteral { get { return _Tag == ExprTag.Literal; } }
        /// <summary>变量表达式</summary>
        public Boolean OnVariable { get { return _Tag == ExprTag.Variable; } }
        /// <summary>函数表达式</summary>
        public Boolean OnFunction { get { return _Tag == ExprTag.Function; } }
        /// <summary>if伪函数表达式</summary>
        public Boolean OnIf { get { return _Tag == ExprTag.If; } }
        /// <summary>&amp;&amp;运算符表达式</summary>
        public Boolean OnAndAlso { get { return _Tag == ExprTag.AndAlso; } }
        /// <summary>||运算符表达式</summary>
        public Boolean OnOrElse { get { return _Tag == ExprTag.OrElse; } }
    }
    public enum LiteralExprTag
    {
        /// <summary>布尔字面量</summary>
        BooleanValue = 0,
        /// <summary>整数字面量</summary>
        IntValue = 1,
        /// <summary>实数字面量</summary>
        RealValue = 2
    }
    /// <summary>字面量表示式</summary>
    [TaggedUnion]
    public sealed class LiteralExpr
    {
        [Tag] public LiteralExprTag _Tag { get; init; }

        /// <summary>布尔字面量</summary>
        public Boolean BooleanValue { get; init; }
        /// <summary>整数字面量</summary>
        public Int IntValue { get; init; }
        /// <summary>实数字面量</summary>
        public Real RealValue { get; init; }

        /// <summary>布尔字面量</summary>
        public static LiteralExpr CreateBooleanValue(Boolean Value) { return new LiteralExpr { _Tag = LiteralExprTag.BooleanValue, BooleanValue = Value }; }
        /// <summary>整数字面量</summary>
        public static LiteralExpr CreateIntValue(Int Value) { return new LiteralExpr { _Tag = LiteralExprTag.IntValue, IntValue = Value }; }
        /// <summary>实数字面量</summary>
        public static LiteralExpr CreateRealValue(Real Value) { return new LiteralExpr { _Tag = LiteralExprTag.RealValue, RealValue = Value }; }

        /// <summary>布尔字面量</summary>
        public Boolean OnBooleanValue { get { return _Tag == LiteralExprTag.BooleanValue; } }
        /// <summary>整数字面量</summary>
        public Boolean OnIntValue { get { return _Tag == LiteralExprTag.IntValue; } }
        /// <summary>实数字面量</summary>
        public Boolean OnRealValue { get { return _Tag == LiteralExprTag.RealValue; } }
    }
    /// <summary>变量表达式</summary>
    [Record]
    public sealed class VariableExpr
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
    }
    /// <summary>函数表达式</summary>
    [Record]
    public sealed class FunctionExpr
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
        /// <summary>参数类型</summary>
        public List<PrimitiveType> ParameterTypes { get; init; }
        /// <summary>返回值类型</summary>
        public PrimitiveType ReturnType { get; init; }
        /// <summary>实参</summary>
        public List<Expr> Arguments { get; init; }
    }
    /// <summary>if伪函数表达式</summary>
    [Record]
    public sealed class IfExpr
    {
        /// <summary>条件</summary>
        public Expr Condition { get; init; }
        /// <summary>条件为真时的值</summary>
        public Expr TruePart { get; init; }
        /// <summary>条件为假时的值</summary>
        public Expr FalsePart { get; init; }
    }
    /// <summary>&amp;&amp;运算符表达式</summary>
    [Record]
    public sealed class AndAlsoExpr
    {
        /// <summary>左部表达式</summary>
        public Expr Left { get; init; }
        /// <summary>右部表达式</summary>
        public Expr Right { get; init; }
    }
    /// <summary>||运算符表达式</summary>
    [Record]
    public sealed class OrElseExpr
    {
        /// <summary>左部表达式</summary>
        public Expr Left { get; init; }
        /// <summary>右部表达式</summary>
        public Expr Right { get; init; }
    }
    /// <summary>基元类型</summary>
    public enum PrimitiveType : int
    {
        /// <summary>布尔类型</summary>
        Boolean = 0,
        /// <summary>整数类型</summary>
        Int = 1,
        /// <summary>实数类型</summary>
        Real = 2
    }
}
