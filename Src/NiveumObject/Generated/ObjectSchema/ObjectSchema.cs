//==========================================================================
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

namespace Niveum.ObjectSchema
{
    public enum TypeDefTag
    {
        /// <summary>基元</summary>
        Primitive = 0,
        /// <summary>别名</summary>
        Alias = 1,
        /// <summary>记录</summary>
        Record = 2,
        /// <summary>标签联合</summary>
        TaggedUnion = 3,
        /// <summary>枚举</summary>
        Enum = 4,
        /// <summary>客户端命令</summary>
        ClientCommand = 5,
        /// <summary>服务端命令</summary>
        ServerCommand = 6,
        /// <summary>查询</summary>
        Query = 7
    }
    /// <summary>类型定义</summary>
    [TaggedUnion]
    public sealed class TypeDef
    {
        [Tag] public TypeDefTag _Tag { get; init; }

        /// <summary>基元</summary>
        public PrimitiveDef Primitive { get; init; }
        /// <summary>别名</summary>
        public AliasDef Alias { get; init; }
        /// <summary>记录</summary>
        public RecordDef Record { get; init; }
        /// <summary>标签联合</summary>
        public TaggedUnionDef TaggedUnion { get; init; }
        /// <summary>枚举</summary>
        public EnumDef Enum { get; init; }
        /// <summary>客户端命令</summary>
        public ClientCommandDef ClientCommand { get; init; }
        /// <summary>服务端命令</summary>
        public ServerCommandDef ServerCommand { get; init; }
        /// <summary>查询</summary>
        public QueryDef Query { get; init; }

        /// <summary>基元</summary>
        public static TypeDef CreatePrimitive(PrimitiveDef Value) { return new TypeDef { _Tag = TypeDefTag.Primitive, Primitive = Value }; }
        /// <summary>别名</summary>
        public static TypeDef CreateAlias(AliasDef Value) { return new TypeDef { _Tag = TypeDefTag.Alias, Alias = Value }; }
        /// <summary>记录</summary>
        public static TypeDef CreateRecord(RecordDef Value) { return new TypeDef { _Tag = TypeDefTag.Record, Record = Value }; }
        /// <summary>标签联合</summary>
        public static TypeDef CreateTaggedUnion(TaggedUnionDef Value) { return new TypeDef { _Tag = TypeDefTag.TaggedUnion, TaggedUnion = Value }; }
        /// <summary>枚举</summary>
        public static TypeDef CreateEnum(EnumDef Value) { return new TypeDef { _Tag = TypeDefTag.Enum, Enum = Value }; }
        /// <summary>客户端命令</summary>
        public static TypeDef CreateClientCommand(ClientCommandDef Value) { return new TypeDef { _Tag = TypeDefTag.ClientCommand, ClientCommand = Value }; }
        /// <summary>服务端命令</summary>
        public static TypeDef CreateServerCommand(ServerCommandDef Value) { return new TypeDef { _Tag = TypeDefTag.ServerCommand, ServerCommand = Value }; }
        /// <summary>查询</summary>
        public static TypeDef CreateQuery(QueryDef Value) { return new TypeDef { _Tag = TypeDefTag.Query, Query = Value }; }

        /// <summary>基元</summary>
        public Boolean OnPrimitive { get { return _Tag == TypeDefTag.Primitive; } }
        /// <summary>别名</summary>
        public Boolean OnAlias { get { return _Tag == TypeDefTag.Alias; } }
        /// <summary>记录</summary>
        public Boolean OnRecord { get { return _Tag == TypeDefTag.Record; } }
        /// <summary>标签联合</summary>
        public Boolean OnTaggedUnion { get { return _Tag == TypeDefTag.TaggedUnion; } }
        /// <summary>枚举</summary>
        public Boolean OnEnum { get { return _Tag == TypeDefTag.Enum; } }
        /// <summary>客户端命令</summary>
        public Boolean OnClientCommand { get { return _Tag == TypeDefTag.ClientCommand; } }
        /// <summary>服务端命令</summary>
        public Boolean OnServerCommand { get { return _Tag == TypeDefTag.ServerCommand; } }
        /// <summary>查询</summary>
        public Boolean OnQuery { get { return _Tag == TypeDefTag.Query; } }
    }
    /// <summary>类型引用</summary>
    [Record]
    public sealed class TypeRef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
    }
    public enum TypeSpecTag
    {
        /// <summary>类型引用</summary>
        TypeRef = 0,
        /// <summary>泛型参数引用</summary>
        GenericParameterRef = 1,
        /// <summary>元组规格</summary>
        Tuple = 2,
        /// <summary>泛型特化规格</summary>
        GenericTypeSpec = 3
    }
    /// <summary>类型规格</summary>
    [TaggedUnion]
    public sealed class TypeSpec
    {
        [Tag] public TypeSpecTag _Tag { get; init; }

        /// <summary>类型引用</summary>
        public TypeRef TypeRef { get; init; }
        /// <summary>泛型参数引用</summary>
        public String GenericParameterRef { get; init; }
        /// <summary>元组规格</summary>
        public List<TypeSpec> Tuple { get; init; }
        /// <summary>泛型特化规格</summary>
        public GenericTypeSpec GenericTypeSpec { get; init; }

        /// <summary>类型引用</summary>
        public static TypeSpec CreateTypeRef(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.TypeRef, TypeRef = Value }; }
        /// <summary>泛型参数引用</summary>
        public static TypeSpec CreateGenericParameterRef(String Value) { return new TypeSpec { _Tag = TypeSpecTag.GenericParameterRef, GenericParameterRef = Value }; }
        /// <summary>元组规格</summary>
        public static TypeSpec CreateTuple(List<TypeSpec> Value) { return new TypeSpec { _Tag = TypeSpecTag.Tuple, Tuple = Value }; }
        /// <summary>泛型特化规格</summary>
        public static TypeSpec CreateGenericTypeSpec(GenericTypeSpec Value) { return new TypeSpec { _Tag = TypeSpecTag.GenericTypeSpec, GenericTypeSpec = Value }; }

        /// <summary>类型引用</summary>
        public Boolean OnTypeRef { get { return _Tag == TypeSpecTag.TypeRef; } }
        /// <summary>泛型参数引用</summary>
        public Boolean OnGenericParameterRef { get { return _Tag == TypeSpecTag.GenericParameterRef; } }
        /// <summary>元组规格</summary>
        public Boolean OnTuple { get { return _Tag == TypeSpecTag.Tuple; } }
        /// <summary>泛型特化规格</summary>
        public Boolean OnGenericTypeSpec { get { return _Tag == TypeSpecTag.GenericTypeSpec; } }
    }
    /// <summary>基元定义</summary>
    [Record]
    public sealed class PrimitiveDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>泛型参数</summary>
        public List<VariableDef> GenericParameters { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>别名定义</summary>
    [Record]
    public sealed class AliasDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>泛型参数</summary>
        public List<VariableDef> GenericParameters { get; init; }
        /// <summary>类型</summary>
        public TypeSpec Type { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>记录定义</summary>
    [Record]
    public sealed class RecordDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>泛型参数</summary>
        public List<VariableDef> GenericParameters { get; init; }
        /// <summary>字段</summary>
        public List<VariableDef> Fields { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>标签联合定义</summary>
    [Record]
    public sealed class TaggedUnionDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>泛型参数</summary>
        public List<VariableDef> GenericParameters { get; init; }
        /// <summary>选择</summary>
        public List<VariableDef> Alternatives { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>枚举定义</summary>
    [Record]
    public sealed class EnumDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>基础类型</summary>
        public TypeSpec UnderlyingType { get; init; }
        /// <summary>字面量</summary>
        public List<LiteralDef> Literals { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>客户端命令</summary>
    [Record]
    public sealed class ClientCommandDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>传出参数（客户端到服务端）</summary>
        public List<VariableDef> OutParameters { get; init; }
        /// <summary>传入参数（服务端到客户端）</summary>
        public List<VariableDef> InParameters { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>服务端命令</summary>
    [Record]
    public sealed class ServerCommandDef
    {
        /// <summary>名称</summary>
        public List<String> Name { get; init; }
        /// <summary>版本</summary>
        public String Version { get; init; }
        /// <summary>传出参数（服务端到客户端）</summary>
        public List<VariableDef> OutParameters { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>泛型特化规格</summary>
    [Record]
    public sealed class GenericTypeSpec
    {
        /// <summary>泛型类型</summary>
        public TypeSpec TypeSpec { get; init; }
        /// <summary>泛型参数</summary>
        public List<TypeSpec> ParameterValues { get; init; }
    }
    /// <summary>变量定义</summary>
    [Record]
    public sealed class VariableDef
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
        /// <summary>类型</summary>
        public TypeSpec Type { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>字面量定义</summary>
    [Record]
    public sealed class LiteralDef
    {
        /// <summary>名称</summary>
        public String Name { get; init; }
        /// <summary>值</summary>
        public Int64 Value { get; init; }
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes { get; init; }
        /// <summary>描述</summary>
        public String Description { get; init; }
    }
    /// <summary>类型定义集</summary>
    [Record]
    public sealed class Schema
    {
        /// <summary>类型</summary>
        public List<TypeDef> Types { get; init; }
        /// <summary>类型引用</summary>
        public List<TypeDef> TypeRefs { get; init; }
        /// <summary>命名空间导入</summary>
        public List<String> Imports { get; init; }
    }
}