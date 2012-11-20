//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

using System;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaSchema;
using IntegerLiteral = System.Int64;
using StringLiteral = System.String;
using BooleanLiteral = System.Boolean;

namespace Yuki.RelationSchema
{
    public enum TypeDefTag
    {
        Primitive,
        Record,
        Enum,
    }
    [TaggedUnion, DebuggerDisplay("{ToString()}")]
    public sealed class TypeDef
    {
        [Tag] public TypeDefTag _Tag;
        public PrimitiveDef Primitive;
        public RecordDef Record;
        public EnumDef Enum;
    
        public static TypeDef CreatePrimitive(PrimitiveDef Value) { return new TypeDef { _Tag = TypeDefTag.Primitive, Primitive = Value }; }
        public static TypeDef CreateRecord(RecordDef Value) { return new TypeDef { _Tag = TypeDefTag.Record, Record = Value }; }
        public static TypeDef CreateEnum(EnumDef Value) { return new TypeDef { _Tag = TypeDefTag.Enum, Enum = Value }; }
    
        public Boolean OnPrimitive { get { return _Tag == TypeDefTag.Primitive; } }
        public Boolean OnRecord { get { return _Tag == TypeDefTag.Record; } }
        public Boolean OnEnum { get { return _Tag == TypeDefTag.Enum; } }
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Alias, DebuggerDisplay("{ToString()}")]
    public sealed class TypeRef
    {
        public StringLiteral Value;
    
        public static implicit operator TypeRef(StringLiteral o)
        {
            return new TypeRef {Value = o};
        }
        public static implicit operator StringLiteral(TypeRef c)
        {
            return c.Value;
        }
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    public enum TypeSpecTag
    {
        TypeRef,
        List,
        Optional,
    }
    [TaggedUnion, DebuggerDisplay("{ToString()}")]
    public sealed class TypeSpec
    {
        [Tag] public TypeSpecTag _Tag;
        public TypeRef TypeRef;
        public TypeRef List;
        public TypeRef Optional;
    
        public static TypeSpec CreateTypeRef(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.TypeRef, TypeRef = Value }; }
        public static TypeSpec CreateList(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.List, List = Value }; }
        public static TypeSpec CreateOptional(TypeRef Value) { return new TypeSpec { _Tag = TypeSpecTag.Optional, Optional = Value }; }
    
        public Boolean OnTypeRef { get { return _Tag == TypeSpecTag.TypeRef; } }
        public Boolean OnList { get { return _Tag == TypeSpecTag.List; } }
        public Boolean OnOptional { get { return _Tag == TypeSpecTag.Optional; } }
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class PrimitiveDef
    {
        public StringLiteral Name;
        public StringLiteral Description;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class VariableDef
    {
        public StringLiteral Name;
        public TypeSpec Type;
        public StringLiteral Description;
        public FieldAttribute Attribute;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    public enum FieldAttributeTag
    {
        Column,
        Navigation,
    }
    [TaggedUnion, DebuggerDisplay("{ToString()}")]
    public sealed class FieldAttribute
    {
        [Tag] public FieldAttributeTag _Tag;
        public ColumnAttribute Column;
        public NavigationAttribute Navigation;
    
        public static FieldAttribute CreateColumn(ColumnAttribute Value) { return new FieldAttribute { _Tag = FieldAttributeTag.Column, Column = Value }; }
        public static FieldAttribute CreateNavigation(NavigationAttribute Value) { return new FieldAttribute { _Tag = FieldAttributeTag.Navigation, Navigation = Value }; }
    
        public Boolean OnColumn { get { return _Tag == FieldAttributeTag.Column; } }
        public Boolean OnNavigation { get { return _Tag == FieldAttributeTag.Navigation; } }
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class ColumnAttribute
    {
        public BooleanLiteral IsIdentity;
        public StringLiteral TypeParameters;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class NavigationAttribute
    {
        public BooleanLiteral IsReverse;
        public BooleanLiteral IsUnique;
        public StringLiteral[] ThisKey;
        public StringLiteral[] OtherKey;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class KeyColumn
    {
        public StringLiteral Name;
        public BooleanLiteral IsDescending;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class Key
    {
        public KeyColumn[] Columns;
        public BooleanLiteral IsClustered;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class RecordDef
    {
        public StringLiteral Name;
        public StringLiteral CollectionName;
        public VariableDef[] Fields;
        public StringLiteral Description;
        public Key PrimaryKey;
        public Key[] UniqueKeys;
        public Key[] NonUniqueKeys;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class LiteralDef
    {
        public StringLiteral Name;
        public IntegerLiteral Value;
        public StringLiteral Description;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class EnumDef
    {
        public StringLiteral Name;
        public TypeSpec UnderlyingType;
        public LiteralDef[] Literals;
        public StringLiteral Description;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
    
    [Record, DebuggerDisplay("{ToString()}")]
    public sealed class Schema
    {
        public TypeDef[] Types;
        public TypeDef[] TypeRefs;
        public StringLiteral[] Imports;
    
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
}
