//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C#枚举数据库代码生成器
//  Version:     2012.06.26.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using OS = Yuki.ObjectSchema;
using Yuki.RelationSchema.DbmlDatabase;

namespace Yuki.RelationSchema.CSharpDatabase
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpDatabase(this Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            Writer w = new Writer(Schema, DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpDatabase(this OS.Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            return CompileToCSharpDatabase(RelationSchemaTranslator.Translate(Schema), DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
        }

        private class Writer
        {

            private static OS.ObjectSchemaTemplateInfo TemplateInfo;
            private XElement Dbml;

            private Schema Schema;
            private String DatabaseName;
            private String EntityNamespaceName;
            private String ContextNamespaceName;
            private String ContextClassName;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(OS.Properties.Resources.CSharp);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpDatabase);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
            }

            public Writer(Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
            {
                this.Schema = Schema;
                this.DatabaseName = DatabaseName;
                this.EntityNamespaceName = EntityNamespaceName;
                this.ContextNamespaceName = ContextNamespaceName;
                this.ContextClassName = ContextClassName;
            }

            private Dictionary<String, Enum> Enums;
            private Dictionary<String, Record> Records;
            public String[] GetSchema()
            {
                Dbml = Schema.CompileToDbmlDatabase(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);

                Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                Records = Schema.Types.Where(t => t.OnRecord).Select(t => t.Record).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

                var Primitives = GetPrimitives();
                var EntityComplexTypes = GetEntityComplexTypes(Schema);
                var ContextComplexTypes = GetContextComplexTypes(Schema);

                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("EntityNamespaceName", EntityNamespaceName).Substitute("ContextNamespaceName", ContextNamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("EntityComplexTypes", EntityComplexTypes).Substitute("ContextComplexTypes", ContextComplexTypes));
            }

            public String[] GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }
            public String[] GetPrimitives()
            {
                List<String> l = new List<String>();

                foreach (var p in Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnPrimitive).Select(c => c.Primitive))
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(p.Name))
                    {
                        var Name = p.Name;
                        var PlatformName = TemplateInfo.PrimitiveMappings[Name].PlatformName;
                        if (Name != PlatformName && !(PlatformName.Contains("[") || PlatformName.Contains("]")))
                        {
                            l.AddRange(GetPrimitive(Name, PlatformName));
                        }
                    }
                }
                return l.ToArray();
            }

            public String GetTypeFriendlyName(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        return Type.TypeRef.Value;
                    case TypeSpecTag.List:
                        return "ListOf" + GetTypeFriendlyName(Type.List.ElementType);
                    default:
                        throw new InvalidOperationException();
                }
            }

            public String GetEnumTypeString(TypeSpec Type)
            {
                if (!Type.OnTypeRef)
                {
                    throw new InvalidOperationException();
                }
                if (!TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                {
                    return GetEscapedIdentifier(Type.TypeRef.Value);
                }
                switch (TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName)
                {
                    case "System.UInt8":
                        return "byte";
                    case "System.UInt16":
                        return "ushort";
                    case "System.UInt32":
                        return "uint";
                    case "System.UInt64":
                        return "ulong";
                    case "System.Int8":
                        return "sbyte";
                    case "System.Int16":
                        return "short";
                    case "System.Int32":
                        return "int";
                    case "System.Int64":
                        return "long";
                    default:
                        return GetEscapedIdentifier(Type.TypeRef.Value);
                }
            }

            public String[] GetLiteral(Literal lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(Literal[] Literals)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(Enum e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.Name).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
            }

            private Boolean IsValueType(TypeSpec Type)
            {
                if (Type.OnTypeRef)
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                    {
                        var t = TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName;
                        try
                        {
                            var rt = System.Type.GetType(t);
                            return rt.IsValueType;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                    else if (Enums.ContainsKey(Type.TypeRef.Value))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            public String GetStorageTypeString(Field f)
            {
                var Type = f.Type;

                if (f.Attribute.OnColumn)
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                    {
                        var Name = Type.TypeRef.Value;
                        var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName;
                        if (PlatformName.Contains("[") || PlatformName.Contains("]"))
                        {
                            Name = PlatformName;
                        }
                        if (f.Attribute.Column.IsNullable && IsValueType(Type))
                        {
                            return GetEscapedIdentifier(Name) + "?";
                        }
                        else
                        {
                            return Name;
                        }
                    }
                    else if (Enums.ContainsKey(Type.TypeRef.Value))
                    {
                        if (f.Attribute.Column.IsNullable)
                        {
                            return GetEscapedIdentifier(Type.TypeRef.Value) + "?";
                        }
                        else
                        {
                            return Type.TypeRef.Value;
                        }
                    }
                    else
                    {
                        return Type.TypeRef.Value;
                    }
                }
                else if (f.Attribute.OnNavigation)
                {
                    if (Type.OnTypeRef)
                    {
                        return "System.Data.Linq.EntityRef<" + GetEscapedIdentifier(Type.TypeRef.Value) + ">";
                    }
                    else if (Type.OnList)
                    {
                        return "System.Data.Linq.EntitySet<" + GetEscapedIdentifier(Type.List.ElementType.TypeRef.Value) + ">";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public String GetPropertyTypeString(Field f)
            {
                var Type = f.Type;

                if (f.Attribute.OnColumn)
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                    {
                        var Name = Type.TypeRef.Value;
                        var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName;
                        if (PlatformName.Contains("[") || PlatformName.Contains("]"))
                        {
                            Name = PlatformName;
                        }
                        if (f.Attribute.Column.IsNullable && IsValueType(Type))
                        {
                            return GetEscapedIdentifier(Name) + "?";
                        }
                        else
                        {
                            return Name;
                        }
                    }
                    else if (Enums.ContainsKey(Type.TypeRef.Value))
                    {
                        if (f.Attribute.Column.IsNullable)
                        {
                            return GetEscapedIdentifier(Type.TypeRef.Value) + "?";
                        }
                        else
                        {
                            return Type.TypeRef.Value;
                        }
                    }
                    else
                    {
                        return Type.TypeRef.Value;
                    }
                }
                else if (f.Attribute.OnNavigation)
                {
                    if (Type.OnTypeRef)
                    {
                        return Type.TypeRef.Value;
                    }
                    else if (Type.OnList)
                    {
                        return "System.Data.Linq.EntitySet<" + GetEscapedIdentifier(Type.List.ElementType.TypeRef.Value) + ">";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public String[] GetStorageField(Field f)
            {
                var Type = f.Type;
                var StorageType = GetStorageTypeString(f);
                if (f.Attribute.OnColumn)
                {
                    return GetTemplate("SimpleStorageField").Substitute("Name", f.Name).Substitute("StorageType", StorageType);
                }
                else if (f.Attribute.OnNavigation)
                {
                    if (Type.OnTypeRef)
                    {
                        return GetTemplate("OneStorageField").Substitute("Name", f.Name).Substitute("StorageType", StorageType);
                    }
                    else if (Type.OnList)
                    {
                        return GetTemplate("ManyStorageField").Substitute("Name", f.Name).Substitute("StorageType", StorageType);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public String[] GetStorageFields(Record r)
            {
                var l = new List<String>();
                foreach (var f in r.Fields)
                {
                    l.AddRange(GetStorageField(f));
                }
                return l.ToArray();
            }

            public String GetColumnParameters(Record r, Field f)
            {
                var c = Dbml.Elements().Where(x => x.Name.LocalName == "Table" && x.Attribute("Name") != null && x.Attribute("Name").Value == r.CollectionName).Single().Elements().Single().Elements().Where(x => x.Name.LocalName == "Column" && x.Attribute("Name") != null && x.Attribute("Name").Value == f.Name).Single();
                var a = f.Attribute.Column;
                var l = new List<String>();
                l.Add(String.Format(@"Storage = ""_{0}""", f.Name));
                if (a.IsIdentity)
                {
                    l.Add("AutoSync = System.Data.Linq.Mapping.AutoSync.OnInsert");
                }
                var DbTypeString = c.Attribute("DbType").Value;
                l.Add(String.Format(@"DbType = ""{0}""", DbTypeString));
                if (r.PrimaryKey.Columns.Select(co => co.Name).Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    l.Add("IsPrimaryKey = true");
                }
                if (a.IsIdentity)
                {
                    l.Add("IsDbGenerated = true");
                }
                if (a.IsNullable)
                {
                    l.Add("CanBeNull = true");
                }
                else
                {
                    l.Add("CanBeNull = false");
                }
                return String.Join(", ", l.ToArray());
            }
            public String GetAssociationParameters(Record r, Field f)
            {
                var c = Dbml.Elements().Where(x => x.Name.LocalName == "Table" && x.Attribute("Name") != null && x.Attribute("Name").Value == r.CollectionName).Single().Elements().Single().Elements().Where(x => x.Name.LocalName == "Association" && x.Attribute("Member") != null && x.Attribute("Member").Value == f.Name).Single();
                var a = f.Attribute.Navigation;
                var l = new List<String>();
                var Name = c.Attribute("Name").Value;
                l.Add(String.Format(@"Name = ""{0}""", Name));
                l.Add(String.Format(@"Storage = ""_{0}""", f.Name));
                l.Add(String.Format(@"ThisKey = ""{0}""", String.Join(", ", a.ThisKey)));
                l.Add(String.Format(@"OtherKey = ""{0}""", String.Join(", ", a.OtherKey)));
                if (!a.IsReverse)
                {
                    l.Add("IsForeignKey = true");
                }
                else
                {
                    if (f.Type.OnTypeRef)
                    {
                        l.Add("IsUnique = true");
                    }
                    l.Add("IsForeignKey = false");
                }
                return String.Join(", ", l.ToArray());
            }
            public String[] GetProperty(Record r, Field f)
            {
                var Type = f.Type;
                var PropertyType = GetPropertyTypeString(f);
                if (f.Attribute.OnColumn)
                {
                    var ColumnParameters = GetColumnParameters(r, f);
                    var AssociationChecks = r.Fields.Where(rf => rf.Attribute.OnNavigation && !rf.Attribute.Navigation.IsReverse && rf.Attribute.Navigation.ThisKey.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).Select(rf => GetTemplate("AssociationCheck").Substitute("AssociationName", rf.Name).Single()).ToArray();
                    return GetTemplate("ColumnProperty").Substitute("Name", f.Name).Substitute("ColumnParameters", ColumnParameters).Substitute("PropertyType", PropertyType).Substitute("AssociationChecks", AssociationChecks).Substitute("XmlComment", GetXmlComment(f.Description));
                }
                else if (f.Attribute.OnNavigation)
                {
                    var a = f.Attribute.Navigation;

                    var AssociationParameters = GetAssociationParameters(r, f);
                    if (!a.IsReverse)
                    {
                        Func<Field, Boolean> IsOtherAssociation = of =>
                        {
                            if (!of.Attribute.OnNavigation) { return false; }
                            if (!of.Attribute.Navigation.ThisKey.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).SequenceEqual(a.OtherKey.OrderBy(k => k, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) { return false; }
                            if (of.Type.OnTypeRef)
                            {
                                if (of.Type.TypeRef.Value != r.Name)
                                {
                                    return false;
                                }
                            }
                            else if (of.Type.OnList)
                            {
                                if (of.Type.List.ElementType.TypeRef.Value != r.Name)
                                {
                                    return false;
                                }
                            }
                            return true;
                        };

                        if (f.Type.OnTypeRef)
                        {
                            var Other = Records[f.Type.TypeRef.Value];
                            var OtherAssociations = Other.Fields.Where(IsOtherAssociation).ToArray();
                            if (OtherAssociations.Length > 0)
                            {
                                var of = OtherAssociations.Single();

                                var KeyColumnAssignments = a.ThisKey.ZipStrict(a.OtherKey, (t, o) => GetTemplate("KeyColumnAssignment").Substitute("Name", t).Substitute("OtherMember", o).Single()).ToArray();
                                var ThisKeyColumnsClear = a.ThisKey.Select(t => GetTemplate("KeyColumnAssignmentNull").Substitute("Name", t).Substitute("StorageType", GetStorageTypeString(r.Fields.Where(sf => sf.Name.Equals(t, StringComparison.OrdinalIgnoreCase)).Single())).Single()).ToArray();

                                if (of.Type.OnTypeRef)
                                {
                                    return GetTemplate("TwoWayForeignKeyOneAssociationProperty").Substitute("Name", f.Name).Substitute("AssociationParameters", AssociationParameters).Substitute("PropertyType", PropertyType).Substitute("KeyColumnAssignments", KeyColumnAssignments).Substitute("ThisKeyColumnsClear", ThisKeyColumnsClear).Substitute("OtherMember", of.Name).Substitute("XmlComment", GetXmlComment(f.Description));
                                }
                                else if (of.Type.OnList)
                                {
                                    return GetTemplate("TwoWayForeignKeyManyAssociationProperty").Substitute("Name", f.Name).Substitute("AssociationParameters", AssociationParameters).Substitute("PropertyType", PropertyType).Substitute("KeyColumnAssignments", KeyColumnAssignments).Substitute("ThisKeyColumnsClear", ThisKeyColumnsClear).Substitute("OtherMember", of.Name).Substitute("XmlComment", GetXmlComment(f.Description));
                                }
                                else
                                {
                                    throw new InvalidOperationException();
                                }
                            }
                        }
                        else if (f.Type.OnList)
                        {
                            throw new InvalidOperationException();
                        }
                    }

                    if (Type.OnTypeRef)
                    {
                        return GetTemplate("OneAssociationProperty").Substitute("Name", f.Name).Substitute("AssociationParameters", AssociationParameters).Substitute("PropertyType", PropertyType).Substitute("XmlComment", GetXmlComment(f.Description));
                    }
                    else if (Type.OnList)
                    {
                        return GetTemplate("ManyAssociationProperty").Substitute("Name", f.Name).Substitute("AssociationParameters", AssociationParameters).Substitute("PropertyType", PropertyType).Substitute("XmlComment", GetXmlComment(f.Description));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public String[] GetProperties(Record r)
            {
                var l = new List<String>();
                foreach (var f in r.Fields)
                {
                    l.AddRange(GetProperty(r, f));
                }
                return l.ToArray();
            }

            public String[] GetTable(Record r)
            {
                var StorageFields = GetStorageFields(r);
                var Properties = GetProperties(r);
                return GetTemplate("Table").Substitute("RecordName", r.Name).Substitute("TableName", r.CollectionName).Substitute("StorageFields", StorageFields).Substitute("Properties", Properties).Substitute("XmlComment", GetXmlComment(r.Description));
            }

            public String[] GetTableGetter(Record r)
            {
                return GetTemplate("TableGetter").Substitute("RecordName", r.Name).Substitute("TableName", r.CollectionName).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetContext(Schema s)
            {
                var l = new List<String>();
                foreach (var t in s.Types)
                {
                    if (!t.OnRecord) { continue; }
                    l.AddRange(GetTableGetter(t.Record));
                }
                var TableGetters = l.ToArray();
                return GetTemplate("Context").Substitute("DatabaseName", DatabaseName).Substitute("ContextClassName", ContextClassName).Substitute("TableGetters", TableGetters);
            }

            public String[] GetStaticDataTableGetter(Record r)
            {
                return GetTemplate("StaticDataTableGetter").Substitute("RecordName", r.Name).Substitute("TableName", r.CollectionName).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetStaticDataLoadWith(Record r)
            {
                var LoadWiths = r.Fields.Where(f => f.Attribute.OnNavigation).SelectMany(f => GetTemplate("StaticDataLoadWith").Substitute("RecordName", r.Name).Substitute("AssociationFieldName", f.Name)).ToArray();
                return LoadWiths;
            }
            public String[] GetStaticDataTableSet(Record r)
            {
                return GetTemplate("StaticDataTableSet").Substitute("TableName", r.CollectionName);
            }
            public String[] GetStaticDataContext(Schema s)
            {
                var TableGetters = s.Types.Where(t => t.OnRecord).SelectMany(t => GetStaticDataTableGetter(t.Record)).ToArray();
                var StaticDataLoadWiths = s.Types.Where(t => t.OnRecord).SelectMany(t => GetStaticDataLoadWith(t.Record)).ToArray();
                var StaticDataTableSets = s.Types.Where(t => t.OnRecord).SelectMany(t => GetStaticDataTableSet(t.Record)).ToArray();
                return GetTemplate("StaticDataContext").Substitute("ContextClassName", ContextClassName).Substitute("TableGetters", TableGetters).Substitute("StaticDataLoadWiths", StaticDataLoadWiths).Substitute("StaticDataTableSets", StaticDataTableSets);
            }

            public String[] GetIReadonlyContextTableGetter(Record r)
            {
                return GetTemplate("IReadonlyContextTableGetter").Substitute("RecordName", r.Name).Substitute("TableName", r.CollectionName).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetIReadonlyContext(Schema s)
            {
                var TableGetters = s.Types.Where(t => t.OnRecord).SelectMany(t => GetIReadonlyContextTableGetter(t.Record)).ToArray();
                return GetTemplate("IReadonlyContext").Substitute("TableGetters", TableGetters);
            }

            public String GetKeyParameters(Record r, Key k)
            {
                var l = new List<String>();
                foreach (var c in k.Columns)
                {
                    var t = GetTypeFriendlyName(r.Fields.Where(f => f.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).Single().Type);
                    l.Add(String.Format("[[{0}]] [[{1}]]", t, c.Name));
                }
                return String.Join(", ", l.ToArray());
            }
            public String GetKeyWhereExpressions(Record r, Key k)
            {
                var l = new List<String>();
                foreach (var c in k.Columns)
                {
                    l.Add(String.Format("Where(e => e.[[{0}]] == [[{0}]])", c.Name));
                }
                return String.Join(".", l.ToArray());
            }
            public String[] GetWhereKeyIs(Record r, Key k)
            {
                var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
                var KeyParameters = GetKeyParameters(r, k);
                var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
                return GetTemplate("WhereKeyIs").Substitute("RecordName", r.Name).Substitute("KeyFriendlyName", KeyFriendlyName).Substitute("KeyParameters", KeyParameters).Substitute("KeyWhereExpressions", KeyWhereExpressions);
            }
            public String[] GetByKey(Record r, Key k)
            {
                var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
                var KeyParameters = GetKeyParameters(r, k);
                var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
                return GetTemplate("ByKey").Substitute("RecordName", r.Name).Substitute("KeyFriendlyName", KeyFriendlyName).Substitute("KeyParameters", KeyParameters).Substitute("KeyWhereExpressions", KeyWhereExpressions);
            }
            public String[] GetByKeyT(Record r, Key k)
            {
                var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
                var KeyParameters = GetKeyParameters(r, k);
                var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
                return GetTemplate("ByKeyT").Substitute("RecordName", r.Name).Substitute("KeyFriendlyName", KeyFriendlyName).Substitute("KeyParameters", KeyParameters).Substitute("KeyWhereExpressions", KeyWhereExpressions);
            }
            public String[] GetMethods(Record[] Records)
            {
                List<String> l = new List<String>();
                foreach (var r in Records)
                {
                    var Keys = (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys).Concat(r.NonUniqueKeys);
                    var UniqueKeys = (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys);
                    var h = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                    foreach (var k in Keys)
                    {
                        for (int m = 1; m <= k.Columns.Length; m += 1)
                        {
                            var Subkey = new Key { Columns = k.Columns.Take(m).ToArray(), IsClustered = k.IsClustered };
                            var KeyFriendlyName = String.Join("And", Subkey.Columns.Select(c => c.Name).ToArray());
                            if (h.Contains(KeyFriendlyName))
                            {
                                continue;
                            }
                            h.Add(KeyFriendlyName);
                            l.AddRange(GetWhereKeyIs(r, Subkey));
                        }
                    }
                    foreach (var k in UniqueKeys)
                    {
                        l.AddRange(GetByKey(r, k));
                        l.AddRange(GetByKeyT(r, k));
                    }
                }
                return l.ToArray();
            }
            public String[] GetDbExtensions(Schema Schema)
            {
                return GetTemplate("DbExtensions").Substitute("Methods", GetMethods(Schema.Types.Where(t => t.OnRecord).Select(t => t.Record).ToArray()));
            }

            public String[] GetXmlComment(String Description)
            {
                if (Description == "") { return new String[] { }; }

                var d = Description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

                var Lines = d.UnifyNewLineToLf().Split('\n');
                if (Lines.Length == 1)
                {
                    return GetTemplate("SingleLineXmlComment").Substitute("Description", d);
                }
                else
                {
                    return GetTemplate("MultiLineXmlComment").Substitute("Description", Lines);
                }
            }

            public String[] GetEntityComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                foreach (var c in Schema.Types)
                {
                    if (c.OnEnum)
                    {
                        l.AddRange(GetEnum(c.Enum));
                        l.Add("");
                    }
                    else if (c.OnRecord)
                    {
                        l.AddRange(GetTable(c.Record));
                        l.Add("");
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            public String[] GetContextComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                l.AddRange(GetContext(Schema));
                l.Add("");
                l.AddRange(GetStaticDataContext(Schema));
                l.Add("");
                l.AddRange(GetIReadonlyContext(Schema));
                l.Add("");
                l.AddRange(GetDbExtensions(Schema));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public String[] GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n');
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                if (TemplateInfo.Keywords.Contains(Identifier))
                {
                    return "@" + Identifier;
                }
                else
                {
                    return Identifier;
                }
            }
            private Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToArray();
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);

            List<String> l = new List<String>();
            foreach (var Line in Lines)
            {
                var NewLine = Line;

                if (Line.Contains(ParameterString))
                {
                    NewLine = NewLine.Replace(ParameterString, Value);
                }

                if (Line.Contains(LowercaseParameterString))
                {
                    NewLine = NewLine.Replace(LowercaseParameterString, LowercaseValue);
                }

                l.Add(NewLine);
            }
            return l.ToArray();
        }
        private static String LowercaseCamelize(String PascalName)
        {
            var l = new List<Char>();
            foreach (var c in PascalName)
            {
                if (Char.IsLower(c))
                {
                    break;
                }

                l.Add(Char.ToLower(c));
            }

            return new String(l.ToArray()) + new String(PascalName.Skip(l.Count).ToArray());
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            List<String> l = new List<String>();
            foreach (var Line in Lines)
            {
                var ParameterString = "${" + Parameter + "}";
                if (Line.Contains(ParameterString))
                {
                    foreach (var vLine in Value)
                    {
                        l.Add(Line.Replace(ParameterString, vLine));
                    }
                }
                else
                {
                    l.Add(Line);
                }
            }
            return l.ToArray();
        }
    }
}
