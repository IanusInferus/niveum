//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 对象类型结构Dbml数据库代码生成器
//  Version:     2012.06.19.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.DbmlDatabase
{
    public static class CodeGenerator
    {
        public static XElement CompileToDbmlDatabase(this Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            Writer w = new Writer(Schema, DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            var a = w.GetSchema();
            return a;
        }
        public static XElement CompileToDbmlDatabase(this OS.Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            return CompileToDbmlDatabase(RelationSchemaTranslator.Translate(Schema), DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
        }

        private class Writer
        {

            private static Dictionary<String, String> ClrPrimitiveMappings;
            private static Dictionary<String, String> DbPrimitiveMappings;

            private Schema Schema;
            private String DatabaseName;
            private String EntityNamespaceName;
            private String ContextNamespaceName;
            private String ContextClassName;

            static Writer()
            {
                ClrPrimitiveMappings = new Dictionary<String, String>();
                ClrPrimitiveMappings.Add("Boolean", "System.Boolean");
                ClrPrimitiveMappings.Add("String", "System.String");
                ClrPrimitiveMappings.Add("Int", "System.Int32");
                ClrPrimitiveMappings.Add("Real", "System.Double");
                ClrPrimitiveMappings.Add("Binary", "System.Byte[]");

                DbPrimitiveMappings = new Dictionary<String, String>();
                DbPrimitiveMappings.Add("Boolean", "Bit");
                DbPrimitiveMappings.Add("String", "NVarChar");
                DbPrimitiveMappings.Add("Int", "Int");
                DbPrimitiveMappings.Add("Real", "Float");
                DbPrimitiveMappings.Add("Binary", "VarBinary");
            }

            public Writer(Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
            {
                this.Schema = Schema;
                this.DatabaseName = DatabaseName;
                this.EntityNamespaceName = EntityNamespaceName;
                this.ContextNamespaceName = ContextNamespaceName;
                this.ContextClassName = ContextClassName;
            }

            private XNamespace ns = XNamespace.Get(@"http://schemas.microsoft.com/linqtosql/dbml/2007");
            private Dictionary<String, Primitive> Primitives;
            private Dictionary<String, Enum> Enums;
            private Dictionary<String, Record> Records;
            private Dictionary<ForeignKey, String> AssociationNames;
            public XElement GetSchema()
            {
                Primitives = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive).Select(t => t.Primitive).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                Records = Schema.Types.Where(t => t.OnRecord).Select(t => t.Record).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

                AssociationNames = new Dictionary<ForeignKey, String>();
                foreach (var r in Records.Values)
                {
                    foreach (var f in r.Fields)
                    {
                        if (f.Attribute.OnNavigation && !f.Attribute.Navigation.IsReverse)
                        {
                            var fk = new ForeignKey { ThisTableName = r.CollectionName, ThisKeyColumns = f.Attribute.Navigation.ThisKey, OtherTableName = Records[f.Type.TypeRef.Value].CollectionName, OtherKeyColumns = f.Attribute.Navigation.OtherKey };
                            var AssociationName = fk.ThisTableName + "_" + fk.ThisKeyColumns.Aggregate((a, b) => a + "_" + b) + "_" + fk.OtherTableName + "_" + fk.OtherKeyColumns.Aggregate((a, b) => a + "_" + b);
                            if (!AssociationNames.ContainsKey(fk))
                            {
                                AssociationNames.Add(fk, AssociationName);
                            }
                        }
                    }
                }
                foreach (var r in Records.Values)
                {
                    foreach (var f in r.Fields)
                    {
                        if (f.Attribute.OnNavigation && f.Attribute.Navigation.IsReverse)
                        {
                            Record ThisTable = null;
                            if (f.Type.OnTypeRef)
                            {
                                ThisTable = Records[f.Type.TypeRef.Value];
                            }
                            else if (f.Type.OnList)
                            {
                                ThisTable = Records[f.Type.List.ElementType.TypeRef.Value];
                            }
                            else
                            {
                                throw new InvalidOperationException();
                            }
                            var fk = new ForeignKey { ThisTableName = ThisTable.CollectionName, ThisKeyColumns = f.Attribute.Navigation.OtherKey, OtherTableName = r.CollectionName, OtherKeyColumns = f.Attribute.Navigation.ThisKey };
                            var AssociationName = fk.ThisTableName + "_" + fk.ThisKeyColumns.Aggregate((a, b) => a + "_" + b) + "_" + fk.OtherTableName + "_" + fk.OtherKeyColumns.Aggregate((a, b) => a + "_" + b);
                            if (!AssociationNames.ContainsKey(fk))
                            {
                                AssociationNames.Add(fk, AssociationName);
                            }
                        }
                    }
                }

                var x = new XElement(ns + "Database");
                x.SetAttributeValue("Name", DatabaseName);
                x.SetAttributeValue("EntityNamespace", EntityNamespaceName);
                x.SetAttributeValue("ContextNamespace", ContextNamespaceName);
                x.SetAttributeValue("Class", ContextClassName);

                foreach (var t in Schema.Types)
                {
                    if (t.OnRecord)
                    {
                        x.Add(GetTable(t.Record));
                    }
                }

                return x;
            }

            private XElement GetTable(Record r)
            {
                var t = new XElement(ns + "Type");
                t.SetAttributeValue("Name", r.Name);
                foreach (var f in r.Fields)
                {
                    t.Add(GetField(r, f));
                }

                var x = new XElement(ns + "Table");
                x.SetAttributeValue("Name", r.CollectionName);
                x.SetAttributeValue("Member", r.CollectionName);
                x.Add(t);
                return x;
            }

            private XElement GetField(Record r, Field f)
            {
                if (f.Attribute.OnColumn)
                {
                    var ca = f.Attribute.Column;

                    var x = new XElement(ns + "Column");
                    x.SetAttributeValue("Name", f.Name);
                    x.SetAttributeValue("Type", GetClrTypeString(f.Type));
                    x.SetAttributeValue("DbType", GetDbTypeString(f.Type, ca));
                    if (r.PrimaryKey.Columns.Select(c => c.Name).Contains(f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        x.SetAttributeValue("IsPrimaryKey", "true");
                    }
                    if (ca.IsIdentity)
                    {
                        x.SetAttributeValue("IsDbGenerated", "true");
                    }
                    if (ca.IsNullable)
                    {
                        x.SetAttributeValue("CanBeNull", "true");
                    }
                    else
                    {
                        x.SetAttributeValue("CanBeNull", "false");
                    }

                    return x;
                }
                else if (f.Attribute.OnNavigation)
                {
                    var na = f.Attribute.Navigation;
                    var x = new XElement(ns + "Association");
                    if (na.IsReverse)
                    {
                        Record ThisTable = null;
                        if (f.Type.OnTypeRef)
                        {
                            ThisTable = Records[f.Type.TypeRef.Value];
                        }
                        else if (f.Type.OnList)
                        {
                            ThisTable = Records[f.Type.List.ElementType.TypeRef.Value];
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                        var fk = new ForeignKey { ThisTableName = ThisTable.CollectionName, ThisKeyColumns = na.OtherKey, OtherTableName = r.CollectionName, OtherKeyColumns = na.ThisKey };
                        var AssociationName = AssociationNames[fk];
                        x.SetAttributeValue("Name", AssociationName);
                    }
                    else
                    {
                        var fk = new ForeignKey { ThisTableName = r.CollectionName, ThisKeyColumns = na.ThisKey, OtherTableName = Records[f.Type.TypeRef.Value].CollectionName, OtherKeyColumns = na.OtherKey };
                        var AssociationName = AssociationNames[fk];
                        x.SetAttributeValue("Name", AssociationName);
                    }

                    x.SetAttributeValue("Member", f.Name);
                    x.SetAttributeValue("ThisKey", na.ThisKey.Aggregate((a, b) => a + ", " + b));
                    x.SetAttributeValue("OtherKey", na.OtherKey.Aggregate((a, b) => a + ", " + b));

                    var IsMultiple = false;
                    var Type = "";
                    if (f.Type.OnList)
                    {
                        IsMultiple = true;
                        Type = GetClrTypeString(f.Type.List.ElementType);
                    }
                    else
                    {
                        IsMultiple = false;
                        Type = GetClrTypeString(f.Type);
                    }
                    x.SetAttributeValue("Type", Type);

                    if (!IsMultiple)
                    {
                        x.SetAttributeValue("Cardinality", "One");
                    }

                    if (!na.IsReverse)
                    {
                        x.SetAttributeValue("IsForeignKey", "true");
                    }
                                        
                    return x;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private String GetClrTypeString(TypeSpec Type)
            {
                if (!Type.OnTypeRef)
                {
                    throw new InvalidOperationException();
                }
                if (Primitives.ContainsKey(Type.TypeRef.Value))
                {
                    return ClrPrimitiveMappings[Type.TypeRef.Value];
                }
                else
                {
                    return Type.TypeRef.Value;
                }
            }

            private String GetDbTypeString(TypeSpec Type, ColumnAttribute ca)
            {
                if (!Type.OnTypeRef)
                {
                    throw new InvalidOperationException();
                }
                var l = new List<String>();
                var TypeName = "";
                if (Primitives.ContainsKey(Type.TypeRef.Value))
                {
                    TypeName = DbPrimitiveMappings[Type.TypeRef.Value];
                }
                else if (Enums.ContainsKey(Type.TypeRef.Value))
                {
                    TypeName = DbPrimitiveMappings[Enums[Type.TypeRef.Value].UnderlyingType.TypeRef.Value];
                }
                else
                {
                    throw new InvalidOperationException();
                }
                if (ca.TypeParameters == "")
                {
                    l.Add(TypeName);
                }
                else
                {
                    l.Add(String.Format("{0}({1})", TypeName, ca.TypeParameters));
                }
                if (ca.IsNullable)
                {
                    l.Add("NULL");
                }
                else
                {
                    l.Add("NOT NULL");
                }
                if (ca.IsIdentity)
                {
                    l.Add("IDENTITY");
                }
                return String.Join(" ", l.ToArray());
            }

            private class ForeignKey
            {
                public String ThisTableName;
                public String[] ThisKeyColumns;
                public String OtherTableName;
                public String[] OtherKeyColumns;

                public override bool Equals(object obj)
                {
                    var o = obj as ForeignKey;
                    if (o == null) { return false; }
                    if (!ThisTableName.Equals(o.ThisTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
                    if (!OtherTableName.Equals(o.OtherTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
                    if (ThisKeyColumns.Length != o.ThisKeyColumns.Length) { return false; }
                    if (OtherKeyColumns.Length != o.OtherKeyColumns.Length) { return false; }
                    if (ThisKeyColumns.Intersect(o.ThisKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != ThisKeyColumns.Length) { return false; }
                    if (OtherKeyColumns.Intersect(o.OtherKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != OtherKeyColumns.Length) { return false; }
                    return true;
                }

                public override int GetHashCode()
                {
                    Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                    return h(ThisTableName) ^ h(OtherTableName) ^ ThisKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b) ^ OtherKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
                }
            }
        }
    }
}
