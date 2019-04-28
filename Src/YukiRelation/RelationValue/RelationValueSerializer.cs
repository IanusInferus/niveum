//==========================================================================
//
//  File:        RelationValueSerializer.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构数据序列化器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Yuki.RelationSchema;

namespace Yuki.RelationValue
{
    public class RelationValueSerializer
    {
        private Schema Schema;
        private Dictionary<String, String> EnumUnderlyingTypes;

        public RelationValueSerializer(Schema s)
        {
            Schema = s;
            EnumUnderlyingTypes = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnEnum)
                {
                    if (!t.Enum.UnderlyingType.OnTypeRef)
                    {
                        throw new InvalidOperationException("EnumUnderlyingTypeNotTypeRef: {0}".Formats(t.Enum.Name));
                    }
                    EnumUnderlyingTypes.Add(t.Enum.Name, t.Enum.UnderlyingType.TypeRef);
                }
            }
        }

        public RelationVal Read(IReadableStream s)
        {
            var Tables = new List<TableVal>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var tv = ReadTable(s, e);
                Tables.Add(tv);
            }
            return new RelationVal { Tables = Tables };
        }
        public TableVal ReadTable(IReadableStream s, EntityDef e)
        {
            var NumRow = s.ReadInt32();
            var Rows = new List<RowVal>();
            var Reader = GetRowReader(e);
            for (int k = 0; k < NumRow; k += 1)
            {
                var r = Reader(s);
                Rows.Add(r);
            }
            return new TableVal { Rows = Rows };
        }
        public Func<IReadableStream, RowVal> GetRowReader(EntityDef e)
        {
            var cReaders = new List<Func<IReadableStream, ColumnVal>>();
            foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn))
            {
                var cReader = GetColumnReader(c);
                cReaders.Add(cReader);
            }
            Func<IReadableStream, RowVal> Reader = s =>
            {
                var Columns = new List<ColumnVal>();
                foreach (var cReader in cReaders)
                {
                    var c = cReader(s);
                    Columns.Add(c);
                }
                return new RowVal { Columns = Columns };
            };
            return Reader;
        }
        private Func<IReadableStream, ColumnVal> GetColumnReader(VariableDef c)
        {
            String TypeName;
            Boolean IsOptional;
            if (c.Type.OnTypeRef)
            {
                TypeName = c.Type.TypeRef.Value;
                IsOptional = false;
            }
            else if (c.Type.OnOptional)
            {
                TypeName = c.Type.Optional.Value;
                IsOptional = true;
            }
            else if (c.Type.OnList)
            {
                var ElementTypeName = c.Type.List.Value;
                if (!ElementTypeName.Equals("Byte", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("InvalidColumnListType: List<{0}>".Formats(ElementTypeName));
                }

                TypeName = "Binary";
                IsOptional = false;
            }
            else
            {
                throw new InvalidOperationException();
            }
            if (EnumUnderlyingTypes.ContainsKey(TypeName))
            {
                TypeName = EnumUnderlyingTypes[TypeName];
            }

            Func<IReadableStream, ColumnVal> Reader;
            if (!IsOptional)
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s => ColumnVal.CreatePrimitive(PrimitiveVal.CreateBooleanValue(s.ReadByte() != 0));
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var NumBytes = s.ReadInt32();
                        var Bytes = s.Read(NumBytes);
                        var t = TextEncoding.UTF16.GetString(Bytes);
                        return ColumnVal.CreatePrimitive(PrimitiveVal.CreateStringValue(t));
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s => ColumnVal.CreatePrimitive(PrimitiveVal.CreateIntValue(s.ReadInt32()));
                }
                else if (TypeName.Equals("Int64", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s => ColumnVal.CreatePrimitive(PrimitiveVal.CreateInt64Value(s.ReadInt64()));
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s => ColumnVal.CreatePrimitive(PrimitiveVal.CreateRealValue(s.ReadFloat64()));
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var NumBytes = s.ReadInt32();
                        var Bytes = s.Read(NumBytes);
                        return ColumnVal.CreatePrimitive(PrimitiveVal.CreateBinaryValue(Bytes.ToList()));
                    };
                }
                else
                {
                    throw new InvalidOperationException("InvalidType: {0}".Formats(TypeName));
                }
            }
            else
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateBooleanValue(s.ReadByte() != 0));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            var NumBytes = s.ReadInt32();
                            var Bytes = s.Read(NumBytes);
                            var t = TextEncoding.UTF16.GetString(Bytes);
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateStringValue(t));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateIntValue(s.ReadInt32()));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else if (TypeName.Equals("Int64", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateInt64Value(s.ReadInt64()));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateRealValue(s.ReadFloat64()));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = s =>
                    {
                        var OnSome = s.ReadInt32() != 0;
                        if (OnSome)
                        {
                            var NumBytes = s.ReadInt32();
                            var Bytes = s.Read(NumBytes);
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateBinaryValue(Bytes.ToList()));
                        }
                        else
                        {
                            return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                        }
                    };
                }
                else
                {
                    throw new InvalidOperationException("InvalidType: {0}".Formats(TypeName));
                }
            }
            return Reader;
        }

        public void Write(IWritableStream s, RelationVal v)
        {
            int k = 0;
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                WriteTable(s, e, v.Tables[k]);
                k += 1;
            }
            if (k != v.Tables.Count) { throw new InvalidOperationException(); }
        }
        public void WriteTable(IWritableStream s, EntityDef e, TableVal v)
        {
            var Writer = GetRowWriter(e);
            var NumRow = v.Rows.Count;
            s.WriteInt32(NumRow);
            for (int k = 0; k < NumRow; k += 1)
            {
                Writer(s, v.Rows[k]);
            }
        }
        public Action<IWritableStream, RowVal> GetRowWriter(EntityDef e)
        {
            var cWriters = new List<Action<IWritableStream, ColumnVal>>();
            foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn))
            {
                var cWriter = GetColumnWriter(c);
                cWriters.Add(cWriter);
            }
            Action<IWritableStream, RowVal> Writer = (s, r) =>
            {
                int k = 0;
                foreach (var cWriter in cWriters)
                {
                    cWriter(s, r.Columns[k]);
                    k += 1;
                }
                if (k != r.Columns.Count) { throw new InvalidOperationException(); }
            };
            return Writer;
        }
        private Action<IWritableStream, ColumnVal> GetColumnWriter(VariableDef c)
        {
            String TypeName;
            Boolean IsOptional;
            if (c.Type.OnTypeRef)
            {
                TypeName = c.Type.TypeRef.Value;
                IsOptional = false;
            }
            else if (c.Type.OnOptional)
            {
                TypeName = c.Type.Optional.Value;
                IsOptional = true;
            }
            else if (c.Type.OnList)
            {
                var ElementTypeName = c.Type.List.Value;
                if (!ElementTypeName.Equals("Byte", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("InvalidColumnListType: List<{0}>".Formats(ElementTypeName));
                }

                TypeName = "Binary";
                IsOptional = false;
            }
            else
            {
                throw new InvalidOperationException();
            }
            if (EnumUnderlyingTypes.ContainsKey(TypeName))
            {
                TypeName = EnumUnderlyingTypes[TypeName];
            }

            Action<IWritableStream, ColumnVal> Writer;
            if (!IsOptional)
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnBooleanValue) { throw new InvalidOperationException(); }
                        s.WriteByte((Byte)(vv.BooleanValue ? 0xFF : 0));
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnStringValue) { throw new InvalidOperationException(); }
                        var Bytes = TextEncoding.UTF16.GetBytes(vv.StringValue);
                        s.WriteInt32(Bytes.Length);
                        s.Write(Bytes);
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                        s.WriteInt32(vv.IntValue);
                    };
                }
                else if (TypeName.Equals("Int64", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnInt64Value) { throw new InvalidOperationException(); }
                        s.WriteInt64(vv.Int64Value);
                    };
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnRealValue) { throw new InvalidOperationException(); }
                        s.WriteFloat64(vv.RealValue);
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnBinaryValue) { throw new InvalidOperationException(); }
                        var Bytes = vv.BinaryValue.ToArray();
                        s.WriteInt32(Bytes.Length);
                        s.Write(Bytes);
                    };
                }
                else
                {
                    throw new InvalidOperationException("InvalidType: {0}".Formats(TypeName));
                }
            }
            else
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnBooleanValue) { throw new InvalidOperationException(); }
                        s.WriteByte((Byte)(vv.BooleanValue ? 0xFF : 0));
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnStringValue) { throw new InvalidOperationException(); }
                        var Bytes = TextEncoding.UTF16.GetBytes(vv.StringValue);
                        s.WriteInt32(Bytes.Length);
                        s.Write(Bytes);
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                        s.WriteInt32(vv.IntValue);
                    };
                }
                else if (TypeName.Equals("Int64", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                        s.WriteInt64(vv.IntValue);
                    };
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnRealValue) { throw new InvalidOperationException(); }
                        s.WriteFloat64(vv.RealValue);
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = (s, v) =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNone)
                        {
                            s.WriteInt32(0);
                            return;
                        }
                        else
                        {
                            s.WriteInt32(1);
                        }
                        var vv = v.Optional.Some;
                        if (!vv.OnBinaryValue) { throw new InvalidOperationException(); }
                        var Bytes = vv.BinaryValue.ToArray();
                        s.WriteInt32(Bytes.Length);
                        s.Write(Bytes);
                    };
                }
                else
                {
                    throw new InvalidOperationException("InvalidType: {0}".Formats(TypeName));
                }
            }
            return Writer;
        }
    }
}
