using System;
using System.Diagnostics;

namespace Firefly.Mapping.MetaSchema
{
    public class RecordAttribute : Attribute { }

    public class AliasAttribute : Attribute { }

    public class TaggedUnionAttribute : Attribute { }

    public class TupleAttribute : Attribute { }

    public class TagAttribute : Attribute { }


    public enum ConceptDefTag
    {
        Primitive,
        Alias,
        Record,
        TaggedUnion
    }

    public enum ConceptSpecTag
    {
        ConceptRef,
        Tuple,
        List
    }

    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public sealed class ConceptDef
    {
        [Tag] public ConceptDefTag _Tag;
        public Primitive Primitive;
        public Alias Alias;
        public Record Record;
        public TaggedUnion TaggedUnion;

        public static ConceptDef CreatePrimitive(Primitive Value) { return new ConceptDef { _Tag = ConceptDefTag.Primitive, Primitive = Value }; }
        public static ConceptDef CreateAlias(Alias Value) { return new ConceptDef { _Tag = ConceptDefTag.Alias, Alias = Value }; }
        public static ConceptDef CreateRecord(Record Value) { return new ConceptDef { _Tag = ConceptDefTag.Record, Record = Value }; }
        public static ConceptDef CreateTaggedUnion(TaggedUnion Value) { return new ConceptDef { _Tag = ConceptDefTag.TaggedUnion, TaggedUnion = Value }; }

        public bool OnPrimitive { get { return _Tag == ConceptDefTag.Primitive; } }
        public bool OnAlias { get { return _Tag == ConceptDefTag.Alias; } }
        public bool OnRecord { get { return _Tag == ConceptDefTag.Record; } }
        public bool OnTaggedUnion { get { return _Tag == ConceptDefTag.TaggedUnion; } }

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Alias]
    [DebuggerDisplay("{ToString()}")]
    public sealed class ConceptRef
    {
        public string Value;

        public static implicit operator ConceptRef(string o) { return new ConceptRef { Value = o }; }
        public static implicit operator string(ConceptRef c) { return c.Value; }

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public sealed class ConceptSpec
    {
        [Tag] public ConceptSpecTag _Tag;
        public ConceptRef ConceptRef;
        public Tuple Tuple;
        public List List;

        public static ConceptSpec CreateConceptRef(ConceptRef Value) { return new ConceptSpec { _Tag = ConceptSpecTag.ConceptRef, ConceptRef = Value }; }
        public static ConceptSpec CreateTuple(Tuple Value) { return new ConceptSpec { _Tag = ConceptSpecTag.Tuple, Tuple = Value }; }
        public static ConceptSpec CreateList(List Value) { return new ConceptSpec { _Tag = ConceptSpecTag.List, List = Value }; }

        public bool OnConceptRef { get { return _Tag == ConceptSpecTag.ConceptRef; } }
        public bool OnTuple { get { return _Tag == ConceptSpecTag.Tuple; } }
        public bool OnList { get { return _Tag == ConceptSpecTag.List; } }

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public struct Unit
    {
        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Alias]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Primitive
    {
        public string Value;

        public static implicit operator Primitive(string o) { return new Primitive { Value = o }; }
        public static implicit operator string(Primitive c) { return c.Value; }

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Alias
    {
        public string Name;
        public ConceptSpec Type;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Tuple
    {
        public ConceptSpec[] Types;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class List
    {
        public ConceptSpec ElementType;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Field
    {
        public string Name;
        public ConceptSpec Type;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Record
    {
        public string Name;
        public Field[] Fields;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Alternative
    {
        public string Name;
        public ConceptSpec Type;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class TaggedUnion
    {
        public string Name;
        public Alternative[] Alternatives;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public sealed class Schema
    {
        public ConceptDef[] Concepts;

        public override string ToString() { return DebuggerDisplayer.ConvertToString(this); }
    }
}
