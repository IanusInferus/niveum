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

namespace Yuki.RelationSchema
{
    public enum TypeDefTag
    {
        /// <summary>基元</summary>
        Primitive = 0,
        /// <summary>实体</summary>
        Entity = 1,
        /// <summary>枚举</summary>
        Enum = 2,
        /// <summary>查询列表</summary>
        QueryList = 3
    }
    /// <summary>类型定义</summary>
    [TaggedUnion]
    public sealed class TypeDef
    {
        [Tag] public TypeDefTag _Tag;

        /// <summary>基元</summary>
        public PrimitiveDef Primitive;
        /// <summary>实体</summary>
        public EntityDef Entity;
        /// <summary>枚举</summary>
        public EnumDef Enum;
        /// <summary>查询列表</summary>
        public QueryListDef QueryList;

        /// <summary>基元</summary>
        public static TypeDef CreatePrimitive(PrimitiveDef Value) { return new TypeDef { _Tag = TypeDefTag.Primitive, Primitive = Value }; }
        /// <summary>实体</summary>
        public static TypeDef CreateEntity(EntityDef Value) { return new TypeDef { _Tag = TypeDefTag.Entity, Entity = Value }; }
        /// <summary>枚举</summary>
        public static TypeDef CreateEnum(EnumDef Value) { return new TypeDef { _Tag = TypeDefTag.Enum, Enum = Value }; }
        /// <summary>查询列表</summary>
        public static TypeDef CreateQueryList(QueryListDef Value) { return new TypeDef { _Tag = TypeDefTag.QueryList, QueryList = Value }; }

        /// <summary>基元</summary>
        public Boolean OnPrimitive { get { return _Tag == TypeDefTag.Primitive; } }
        /// <summary>实体</summary>
        public Boolean OnEntity { get { return _Tag == TypeDefTag.Entity; } }
        /// <summary>枚举</summary>
        public Boolean OnEnum { get { return _Tag == TypeDefTag.Enum; } }
        /// <summary>查询列表</summary>
        public Boolean OnQueryList { get { return _Tag == TypeDefTag.QueryList; } }
    }
    /// <summary>类型引用</summary>
    [Alias]
    public sealed class TypeRef
    {
        public String Value;

        public static implicit operator TypeRef(String o)
        {
            return new TypeRef {Value = o};
        }
        public static implicit operator String(TypeRef c)
        {
            return c.Value;
        }
    }
    public enum TypeSpecTag
    {
        /// <summary>类型引用</summary>
        TypeRef = 0,
        /// <summary>列表</summary>
        List = 1,
        /// <summary>可选类型</summary>
        Optional = 2
    }
    /// <summary>类型说明</summary>
    [TaggedUnion]
    public sealed class TypeSpec
    {
        [Tag] public TypeSpecTag _Tag;

        /// <summary>类型引用</summary>
        public TypeRef TypeRef;
        /// <summary>列表</summary>
        public TypeRef List;
        /// <summary>可选类型</summary>
        public TypeRef Optional;

        /// <summary>类型引用</summary>
        public static TypeSpec CreateTypeRef(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.TypeRef, TypeRef = Value }; }
        /// <summary>列表</summary>
        public static TypeSpec CreateList(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.List, List = Value }; }
        /// <summary>可选类型</summary>
        public static TypeSpec CreateOptional(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.Optional, Optional = Value }; }

        /// <summary>类型引用</summary>
        public Boolean OnTypeRef { get { return _Tag == TypeSpecTag.TypeRef; } }
        /// <summary>列表</summary>
        public Boolean OnList { get { return _Tag == TypeSpecTag.List; } }
        /// <summary>可选类型</summary>
        public Boolean OnOptional { get { return _Tag == TypeSpecTag.Optional; } }
    }
    /// <summary>基元定义</summary>
    [Record]
    public sealed class PrimitiveDef
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes;
        /// <summary>描述</summary>
        public String Description;
    }
    /// <summary>变量定义</summary>
    [Record]
    public sealed class VariableDef
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>类型</summary>
        public TypeSpec Type;
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes;
        /// <summary>描述</summary>
        public String Description;
        /// <summary>特性</summary>
        public FieldAttribute Attribute;
    }
    public enum FieldAttributeTag
    {
        /// <summary>列特性</summary>
        Column = 0,
        /// <summary>导航特性</summary>
        Navigation = 1
    }
    /// <summary>字段特性</summary>
    [TaggedUnion]
    public sealed class FieldAttribute
    {
        [Tag] public FieldAttributeTag _Tag;

        /// <summary>列特性</summary>
        public ColumnAttribute Column;
        /// <summary>导航特性</summary>
        public NavigationAttribute Navigation;

        /// <summary>列特性</summary>
        public static FieldAttribute CreateColumn(ColumnAttribute Value) { return new FieldAttribute { _Tag = FieldAttributeTag.Column, Column = Value }; }
        /// <summary>导航特性</summary>
        public static FieldAttribute CreateNavigation(NavigationAttribute Value) { return new FieldAttribute { _Tag = FieldAttributeTag.Navigation, Navigation = Value }; }

        /// <summary>列特性</summary>
        public Boolean OnColumn { get { return _Tag == FieldAttributeTag.Column; } }
        /// <summary>导航特性</summary>
        public Boolean OnNavigation { get { return _Tag == FieldAttributeTag.Navigation; } }
    }
    /// <summary>列特性</summary>
    [Record]
    public sealed class ColumnAttribute
    {
        /// <summary>是否为自增字段</summary>
        public Boolean IsIdentity;
        /// <summary>类型参数</summary>
        public String TypeParameters;
    }
    /// <summary>导航特性</summary>
    [Record]
    public sealed class NavigationAttribute
    {
        /// <summary>是否为反向导航</summary>
        public Boolean IsReverse;
        /// <summary>是否为唯一导航</summary>
        public Boolean IsUnique;
        /// <summary>当前表的键</summary>
        public List<String> ThisKey;
        /// <summary>目标表的键</summary>
        public List<String> OtherKey;
    }
    /// <summary>键中的列</summary>
    [Record]
    public sealed class KeyColumn
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>是否逆序</summary>
        public Boolean IsDescending;
    }
    /// <summary>键</summary>
    [Record]
    public sealed class Key
    {
        /// <summary>列</summary>
        public List<KeyColumn> Columns;
        /// <summary>是否为聚合索引</summary>
        public Boolean IsClustered;
    }
    /// <summary>实体定义</summary>
    [Record]
    public sealed class EntityDef
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>集合名称</summary>
        public String CollectionName;
        /// <summary>字段</summary>
        public List<VariableDef> Fields;
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes;
        /// <summary>描述</summary>
        public String Description;
        /// <summary>主键</summary>
        public Key PrimaryKey;
        /// <summary>唯一键</summary>
        public List<Key> UniqueKeys;
        /// <summary>非唯一键</summary>
        public List<Key> NonUniqueKeys;
    }
    /// <summary>字面量定义</summary>
    [Record]
    public sealed class LiteralDef
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>值</summary>
        public Int64 Value;
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes;
        /// <summary>描述</summary>
        public String Description;
    }
    /// <summary>枚举定义</summary>
    [Record]
    public sealed class EnumDef
    {
        /// <summary>名称</summary>
        public String Name;
        /// <summary>基础类型</summary>
        public TypeSpec UnderlyingType;
        /// <summary>字面量</summary>
        public List<LiteralDef> Literals;
        /// <summary>特性</summary>
        public List<KeyValuePair<String, List<String>>> Attributes;
        /// <summary>描述</summary>
        public String Description;
    }
    public enum VerbTag
    {
        /// <summary>映射</summary>
        Select = 0,
        /// <summary>锁定</summary>
        Lock = 1,
        /// <summary>插入</summary>
        Insert = 2,
        /// <summary>更新</summary>
        Update = 3,
        /// <summary>覆盖</summary>
        Upsert = 4,
        /// <summary>删除</summary>
        Delete = 5
    }
    /// <summary>动词</summary>
    [TaggedUnion]
    public sealed class Verb
    {
        [Tag] public VerbTag _Tag;

        /// <summary>映射</summary>
        public Unit Select;
        /// <summary>锁定</summary>
        public Unit Lock;
        /// <summary>插入</summary>
        public Unit Insert;
        /// <summary>更新</summary>
        public Unit Update;
        /// <summary>覆盖</summary>
        public Unit Upsert;
        /// <summary>删除</summary>
        public Unit Delete;

        /// <summary>映射</summary>
        public static Verb CreateSelect() { return new Verb { _Tag = VerbTag.Select, Select = default(Unit) }; }
        /// <summary>锁定</summary>
        public static Verb CreateLock() { return new Verb { _Tag = VerbTag.Lock, Lock = default(Unit) }; }
        /// <summary>插入</summary>
        public static Verb CreateInsert() { return new Verb { _Tag = VerbTag.Insert, Insert = default(Unit) }; }
        /// <summary>更新</summary>
        public static Verb CreateUpdate() { return new Verb { _Tag = VerbTag.Update, Update = default(Unit) }; }
        /// <summary>覆盖</summary>
        public static Verb CreateUpsert() { return new Verb { _Tag = VerbTag.Upsert, Upsert = default(Unit) }; }
        /// <summary>删除</summary>
        public static Verb CreateDelete() { return new Verb { _Tag = VerbTag.Delete, Delete = default(Unit) }; }

        /// <summary>映射</summary>
        public Boolean OnSelect { get { return _Tag == VerbTag.Select; } }
        /// <summary>锁定</summary>
        public Boolean OnLock { get { return _Tag == VerbTag.Lock; } }
        /// <summary>插入</summary>
        public Boolean OnInsert { get { return _Tag == VerbTag.Insert; } }
        /// <summary>更新</summary>
        public Boolean OnUpdate { get { return _Tag == VerbTag.Update; } }
        /// <summary>覆盖</summary>
        public Boolean OnUpsert { get { return _Tag == VerbTag.Upsert; } }
        /// <summary>删除</summary>
        public Boolean OnDelete { get { return _Tag == VerbTag.Delete; } }
    }
    public enum NumeralTag
    {
        /// <summary>0..1</summary>
        Optional = 0,
        /// <summary>1</summary>
        One = 1,
        /// <summary>*</summary>
        Many = 2,
        /// <summary>全部</summary>
        All = 3,
        /// <summary>区间</summary>
        Range = 4,
        /// <summary>数量</summary>
        Count = 5
    }
    /// <summary>量词</summary>
    [TaggedUnion]
    public sealed class Numeral
    {
        [Tag] public NumeralTag _Tag;

        /// <summary>0..1</summary>
        public Unit Optional;
        /// <summary>1</summary>
        public Unit One;
        /// <summary>*</summary>
        public Unit Many;
        /// <summary>全部</summary>
        public Unit All;
        /// <summary>区间</summary>
        public Unit Range;
        /// <summary>数量</summary>
        public Unit Count;

        /// <summary>0..1</summary>
        public static Numeral CreateOptional() { return new Numeral { _Tag = NumeralTag.Optional, Optional = default(Unit) }; }
        /// <summary>1</summary>
        public static Numeral CreateOne() { return new Numeral { _Tag = NumeralTag.One, One = default(Unit) }; }
        /// <summary>*</summary>
        public static Numeral CreateMany() { return new Numeral { _Tag = NumeralTag.Many, Many = default(Unit) }; }
        /// <summary>全部</summary>
        public static Numeral CreateAll() { return new Numeral { _Tag = NumeralTag.All, All = default(Unit) }; }
        /// <summary>区间</summary>
        public static Numeral CreateRange() { return new Numeral { _Tag = NumeralTag.Range, Range = default(Unit) }; }
        /// <summary>数量</summary>
        public static Numeral CreateCount() { return new Numeral { _Tag = NumeralTag.Count, Count = default(Unit) }; }

        /// <summary>0..1</summary>
        public Boolean OnOptional { get { return _Tag == NumeralTag.Optional; } }
        /// <summary>1</summary>
        public Boolean OnOne { get { return _Tag == NumeralTag.One; } }
        /// <summary>*</summary>
        public Boolean OnMany { get { return _Tag == NumeralTag.Many; } }
        /// <summary>全部</summary>
        public Boolean OnAll { get { return _Tag == NumeralTag.All; } }
        /// <summary>区间</summary>
        public Boolean OnRange { get { return _Tag == NumeralTag.Range; } }
        /// <summary>数量</summary>
        public Boolean OnCount { get { return _Tag == NumeralTag.Count; } }
    }
    /// <summary>查询</summary>
    [Record]
    public sealed class QueryDef
    {
        /// <summary>实体名称</summary>
        public String EntityName;
        /// <summary>动词</summary>
        public Verb Verb;
        /// <summary>量词</summary>
        public Numeral Numeral;
        /// <summary>选择索引</summary>
        public List<String> By;
        /// <summary>排序索引</summary>
        public List<KeyColumn> OrderBy;
    }
    /// <summary>查询列表</summary>
    [Record]
    public sealed class QueryListDef
    {
        /// <summary>查询</summary>
        public List<QueryDef> Queries;
    }
    /// <summary>类型路径</summary>
    [Record]
    public sealed class TypePath
    {
        /// <summary>类型名称</summary>
        public String Name;
        /// <summary>文件路径</summary>
        public String Path;
    }
    /// <summary>类型定义集</summary>
    [Record]
    public sealed class Schema
    {
        /// <summary>类型</summary>
        public List<TypeDef> Types;
        /// <summary>类型引用</summary>
        public List<TypeDef> TypeRefs;
        /// <summary>命名空间导入</summary>
        public List<String> Imports;
    }
}
