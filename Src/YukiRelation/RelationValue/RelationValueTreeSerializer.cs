//==========================================================================
//
//  File:        RelationValueTreeSerializer.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构数据Tree序列化器
//  Version:     2013.05.08.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Yuki.RelationSchema;

namespace Yuki.RelationValue
{
    public class RelationValueTreeSerializer
    {
        private Schema Schema;
        private Dictionary<String, EntityDef> EntityMetas;
        private Dictionary<String, String> EnumUnderlyingTypes;
        private Dictionary<String, Dictionary<String, Int64>> EnumParsers;
        private Dictionary<String, Dictionary<Int64, String>> EnumWriters;

        public RelationValueTreeSerializer(Schema s)
        {
            Schema = s;
            EntityMetas = new Dictionary<String, EntityDef>(StringComparer.OrdinalIgnoreCase);
            EnumUnderlyingTypes = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            EnumParsers = new Dictionary<String, Dictionary<String, Int64>>(StringComparer.OrdinalIgnoreCase);
            EnumWriters = new Dictionary<String, Dictionary<Int64, String>>();
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnEnum)
                {
                    if (!t.Enum.UnderlyingType.OnTypeRef)
                    {
                        throw new InvalidOperationException("EnumUnderlyingTypeNotTypeRef: {0}".Formats(t.Enum.Name));
                    }
                    EnumUnderlyingTypes.Add(t.Enum.Name, t.Enum.UnderlyingType.TypeRef);
                    if (!EnumParsers.ContainsKey(t.Enum.Name))
                    {
                        var d = new Dictionary<String, Int64>(StringComparer.OrdinalIgnoreCase);
                        var eh = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                        foreach (var l in t.Enum.Literals)
                        {
                            if (!eh.Contains(l.Name))
                            {
                                if (d.ContainsKey(l.Name))
                                {
                                    eh.Add(l.Name);
                                    d.Remove(l.Name);
                                }
                                else
                                {
                                    d.Add(l.Name, l.Value);
                                }
                            }
                            if (!eh.Contains(l.Description))
                            {
                                if (d.ContainsKey(l.Description))
                                {
                                    eh.Add(l.Description);
                                    d.Remove(l.Description);
                                }
                                else
                                {
                                    d.Add(l.Description, l.Value);
                                }
                            }
                        }
                        EnumParsers.Add(t.Enum.Name, d);
                    }
                    if (!EnumWriters.ContainsKey(t.Enum.Name))
                    {
                        var Parser = EnumParsers[t.Enum.Name];
                        var d = new Dictionary<Int64, String>();
                        foreach (var l in t.Enum.Literals)
                        {
                            if (Parser.ContainsKey(l.Description))
                            {
                                if (!d.ContainsKey(l.Value))
                                {
                                    d.Add(l.Value, l.Description);
                                }
                            }
                            else
                            {
                                if (!d.ContainsKey(l.Value))
                                {
                                    d.Add(l.Value, l.Name);
                                }
                            }
                        }
                        EnumWriters.Add(t.Enum.Name, d);
                    }
                }
            }
            foreach (var t in s.Types)
            {
                if (t.OnEntity)
                {
                    EntityMetas.Add(t.Entity.Name, t.Entity);
                }
            }
        }

        public RelationVal Read(Dictionary<String, List<Node>> TreeRelation)
        {
            var Tables = new List<TableVal>();
            var Missings = EntityMetas.Keys.Except(TreeRelation.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
            if (Missings.Length > 0)
            {
                throw new InvalidOperationException("TableMissing: " + String.Join(" ", Missings));
            }
            var NotExists = TreeRelation.Keys.Except(EntityMetas.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
            if (NotExists.Length > 0)
            {
                throw new InvalidOperationException("TableUnknown: " + String.Join(" ", NotExists));
            }
            foreach (var e in EntityMetas.Values)
            {
                var tv = ReadTable(TreeRelation[e.Name], e);
                Tables.Add(tv);
            }
            return new RelationVal { Tables = Tables };
        }
        public TableVal ReadTable(List<Node> TreeTable, EntityDef e)
        {
            var Rows = new List<RowVal>();
            var Reader = GetRowReader(e);
            foreach (var TreeRow in TreeTable)
            {
                var r = Reader(TreeRow);
                Rows.Add(r);
            }
            return new TableVal { Rows = Rows };
        }
        public Func<Node, RowVal> GetRowReader(EntityDef e)
        {
            var cReaders = new List<Func<Node, ColumnVal>>();
            foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn))
            {
                var cReader = GetColumnReader(e, c);
                cReaders.Add(cReader);
            }
            Func<Node, RowVal> Reader = TreeRow =>
            {
                var Columns = new List<ColumnVal>();
                foreach (var cReader in cReaders)
                {
                    if (!TreeRow.OnStem)
                    {
                        throw new InvalidOperationException(String.Format("InvalidData: {0}[{1}]", e.Name, Columns.Count));
                    }
                    var c = cReader(TreeRow);
                    Columns.Add(c);
                }
                return new RowVal { Columns = Columns };
            };
            return Reader;
        }
        private Func<Node, ColumnVal> GetColumnReader(EntityDef e, VariableDef c)
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
            Dictionary<String, Int64> EnumParser = null;
            if (EnumUnderlyingTypes.ContainsKey(TypeName))
            {
                EnumParser = EnumParsers[TypeName];
                TypeName = EnumUnderlyingTypes[TypeName];
            }

            Func<Node, ColumnVal> Reader;
            if (!IsOptional)
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        try
                        {
                            var v = Boolean.Parse(cv);
                            return ColumnVal.CreatePrimitive(PrimitiveVal.CreateBooleanValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        var v = cv;
                        return ColumnVal.CreatePrimitive(PrimitiveVal.CreateStringValue(v));
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    if (EnumParser != null)
                    {
                        Reader = TreeRow =>
                        {
                            var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (cvs.Length != 1)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                            var cv = cvs.Single().Stem.Children.Single().Leaf;
                            if (EnumParser.ContainsKey(cv))
                            {
                                var v = (int)(EnumParser[cv]);
                                return ColumnVal.CreatePrimitive(PrimitiveVal.CreateIntValue(v));
                            }
                            else
                            {
                                try
                                {
                                    var v = NumericStrings.InvariantParseInt32(cv);
                                    return ColumnVal.CreatePrimitive(PrimitiveVal.CreateIntValue(v));
                                }
                                catch (FormatException)
                                {
                                    throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                                }
                            }
                        };
                    }
                    else
                    {
                        Reader = TreeRow =>
                        {
                            var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (cvs.Length != 1)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                            var cv = cvs.Single().Stem.Children.Single().Leaf;
                            try
                            {
                                var v = NumericStrings.InvariantParseInt32(cv);
                                return ColumnVal.CreatePrimitive(PrimitiveVal.CreateIntValue(v));
                            }
                            catch (FormatException)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                        };
                    }
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        try
                        {
                            var v = NumericStrings.InvariantParseFloat64(cv);
                            return ColumnVal.CreatePrimitive(PrimitiveVal.CreateRealValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        try
                        {
                            var v = Regex.Split(cv.Trim(" \t\r\n".ToCharArray()), "( |\t|\r|\n)+", RegexOptions.ExplicitCapture).Select(s => Byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToList();
                            return ColumnVal.CreatePrimitive(PrimitiveVal.CreateBinaryValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
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
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                        try
                        {
                            var v = Boolean.Parse(cv);
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateBooleanValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                        var v = cv;
                        return ColumnVal.CreateOptional(PrimitiveVal.CreateStringValue(v));
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    if (EnumParser != null)
                    {
                        Reader = TreeRow =>
                        {
                            var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (cvs.Length != 1)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                            var cv = cvs.Single().Stem.Children.Single().Leaf;
                            if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                            if (EnumParser.ContainsKey(cv))
                            {
                                var v = (int)(EnumParser[cv]);
                                return ColumnVal.CreateOptional(PrimitiveVal.CreateIntValue(v));
                            }
                            else
                            {
                                try
                                {
                                    var v = NumericStrings.InvariantParseInt32(cv);
                                    return ColumnVal.CreateOptional(PrimitiveVal.CreateIntValue(v));
                                }
                                catch (FormatException)
                                {
                                    throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                                }
                            }
                        };
                    }
                    else
                    {
                        Reader = TreeRow =>
                        {
                            var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (cvs.Length != 1)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                            var cv = cvs.Single().Stem.Children.Single().Leaf;
                            if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                            try
                            {
                                var v = NumericStrings.InvariantParseInt32(cv);
                                return ColumnVal.CreateOptional(PrimitiveVal.CreateIntValue(v));
                            }
                            catch (FormatException)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                            }
                        };
                    }
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                        try
                        {
                            var v = NumericStrings.InvariantParseFloat64(cv);
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateRealValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Reader = TreeRow =>
                    {
                        var cvs = TreeRow.Stem.Children.Where(col => col.OnStem && col.Stem.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (cvs.Length != 1)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
                        }
                        var cv = cvs.Single().Stem.Children.Single().Leaf;
                        if (cv == "-") { return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty); }
                        try
                        {
                            var v = Regex.Split(cv.Trim(" \t\r\n".ToCharArray()), "( |\t|\r|\n)+", RegexOptions.ExplicitCapture).Select(s => Byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToList();
                            return ColumnVal.CreateOptional(PrimitiveVal.CreateBinaryValue(v));
                        }
                        catch (FormatException)
                        {
                            throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", e.Name, c.Name));
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

        public Dictionary<String, List<Node>> Write(RelationVal v)
        {
            var TreeRelation = new Dictionary<String, List<Node>>();
            int k = 0;
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var t = WriteTable(e, v.Tables[k]);
                TreeRelation.Add(e.Name, t);
                k += 1;
            }
            if (k != v.Tables.Count) { throw new InvalidOperationException(); }
            return TreeRelation;
        }
        public List<Node> WriteTable(EntityDef e, TableVal v)
        {
            var Writer = GetRowWriter(e);
            var TreeTable = new List<Node>();
            foreach (var Row in v.Rows)
            {
                var r = Writer(Row);
                TreeTable.Add(r);
            }
            return TreeTable;
        }
        public Func<RowVal, Node> GetRowWriter(EntityDef e)
        {
            var cWriters = new List<Func<ColumnVal, Node>>();
            foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn))
            {
                var cWriter = GetColumnWriter(c);
                cWriters.Add(cWriter);
            }
            Func<RowVal, Node> Writer = r =>
            {
                var Nodes = new List<Node>();
                int k = 0;
                foreach (var cWriter in cWriters)
                {
                    var c = cWriter(r.Columns[k]);
                    Nodes.Add(c);
                    k += 1;
                }
                if (k != r.Columns.Count) { throw new InvalidOperationException(); }
                return Node.CreateStem(new Stem { Name = e.Name, Children = Nodes.ToArray() });
            };
            return Writer;
        }
        private Func<ColumnVal, Node> GetColumnWriter(VariableDef c)
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
            Dictionary<Int64, String> EnumWriter = null;
            if (EnumUnderlyingTypes.ContainsKey(TypeName))
            {
                EnumWriter = EnumWriters[TypeName];
                TypeName = EnumUnderlyingTypes[TypeName];
            }

            Func<ColumnVal, Node> Writer;
            if (!IsOptional)
            {
                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnBooleanValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.BooleanValue.ToInvariantString()) } });
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnStringValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.StringValue) } });
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    if (EnumWriter != null)
                    {
                        Writer = v =>
                        {
                            if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                            var vv = v.Primitive;
                            if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                            if (EnumWriter.ContainsKey(vv.IntValue))
                            {
                                return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(EnumWriter[vv.IntValue]) } });
                            }
                            else
                            {
                                return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.IntValue.ToInvariantString()) } });
                            }
                        };
                    }
                    else
                    {
                        Writer = v =>
                        {
                            if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                            var vv = v.Primitive;
                            if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.IntValue.ToInvariantString()) } });
                        };
                    }
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnRealValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.RealValue.ToInvariantString()) } });
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnPrimitive) { throw new InvalidOperationException(); }
                        var vv = v.Primitive;
                        if (!vv.OnBinaryValue) { throw new InvalidOperationException(); }
                        var ByteString = String.Join(" ", vv.BinaryValue.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(ByteString) } });
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
                    Writer = v =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNotHasValue)
                        {
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateEmpty() } });
                        }
                        var vv = v.Optional.HasValue;
                        if (!vv.OnBooleanValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.BooleanValue.ToInvariantString()) } });
                    };
                }
                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNotHasValue)
                        {
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateEmpty() } });
                        }
                        var vv = v.Optional.HasValue;
                        if (!vv.OnStringValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.StringValue) } });
                    };
                }
                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNotHasValue)
                        {
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateEmpty() } });
                        }
                        var vv = v.Optional.HasValue;
                        if (!vv.OnIntValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.IntValue.ToInvariantString()) } });
                    };
                }
                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNotHasValue)
                        {
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateEmpty() } });
                        }
                        var vv = v.Optional.HasValue;
                        if (!vv.OnRealValue) { throw new InvalidOperationException(); }
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(vv.RealValue.ToInvariantString()) } });
                    };
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Writer = v =>
                    {
                        if (!v.OnOptional) { throw new InvalidOperationException(); }
                        if (v.Optional.OnNotHasValue)
                        {
                            return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateEmpty() } });
                        }
                        var vv = v.Optional.HasValue;
                        if (!vv.OnBinaryValue) { throw new InvalidOperationException(); }
                        var ByteString = String.Join(" ", vv.BinaryValue.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
                        return Node.CreateStem(new Stem { Name = c.Name, Children = new Node[] { Node.CreateLeaf(ByteString) } });
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
