//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构Dbml数据库代码生成器
//  Version:     2014.12.06.
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
                ClrPrimitiveMappings.Add("Int64", "System.Int64");
                ClrPrimitiveMappings.Add("Real", "System.Double");
                ClrPrimitiveMappings.Add("Binary", "System.Byte[]");

                DbPrimitiveMappings = new Dictionary<String, String>();
                DbPrimitiveMappings.Add("Boolean", "Bit");
                DbPrimitiveMappings.Add("String", "NVarChar");
                DbPrimitiveMappings.Add("Int", "Int");
                DbPrimitiveMappings.Add("Int64", "BigInt");
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

                Primitives = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive).Select(t => t.Primitive).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                Records = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

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
                            EntityDef ThisTable = null;
                            if (f.Type.OnTypeRef)
                            {
                                ThisTable = Records[f.Type.TypeRef.Value];
                            }
                            else if (f.Type.OnOptional)
                            {
                                ThisTable = Records[f.Type.Optional.Value];
                            }
                            else if (f.Type.OnList)
                            {
                                ThisTable = Records[f.Type.List.Value];
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
            }

            private XNamespace ns = XNamespace.Get(@"http://schemas.microsoft.com/linqtosql/dbml/2007");
            private Dictionary<String, PrimitiveDef> Primitives;
            private Dictionary<String, EnumDef> Enums;
            private Dictionary<String, EntityDef> Records;
            private Dictionary<ForeignKey, String> AssociationNames;
            public XElement GetSchema()
            {
                var x = new XElement(ns + "Database");
                x.SetAttributeValue("Name", DatabaseName);
                x.SetAttributeValue("EntityNamespace", EntityNamespaceName);
                x.SetAttributeValue("ContextNamespace", ContextNamespaceName);
                x.SetAttributeValue("Class", ContextClassName);

                foreach (var t in Schema.Types)
                {
                    if (t.OnEntity)
                    {
                        x.Add(GetTable(t.Entity));
                    }
                }

                return x;
            }

            private XElement GetTable(EntityDef r)
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

            private XElement GetField(EntityDef r, VariableDef f)
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
                    if (f.Type.OnOptional)
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
                        EntityDef ThisTable = null;
                        if (f.Type.OnTypeRef)
                        {
                            ThisTable = Records[f.Type.TypeRef.Value];
                        }
                        else if (f.Type.OnOptional)
                        {
                            ThisTable = Records[f.Type.Optional.Value];
                        }
                        else if (f.Type.OnList)
                        {
                            ThisTable = Records[f.Type.List.Value];
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
                        Type = GetClrTypeString(TypeSpec.CreateTypeRef(f.Type.List));
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
                String Name;
                if (Type.OnTypeRef)
                {
                    Name = Type.TypeRef.Value;
                }
                else if (Type.OnOptional)
                {
                    Name = Type.Optional.Value;
                }
                else
                {
                    throw new InvalidOperationException();
                }
                if (Primitives.ContainsKey(Name))
                {
                    return ClrPrimitiveMappings[Name];
                }
                else
                {
                    return Name;
                }
            }

            private String GetDbTypeString(TypeSpec Type, ColumnAttribute ca)
            {
                String Name;
                Boolean IsNullable;
                if (Type.OnTypeRef)
                {
                    Name = Type.TypeRef.Value;
                    IsNullable = false;
                }
                else if (Type.OnOptional)
                {
                    Name = Type.Optional.Value;
                    IsNullable = true;
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var l = new List<String>();
                var TypeName = "";
                if (Primitives.ContainsKey(Name))
                {
                    TypeName = DbPrimitiveMappings[Name];
                }
                else if (Enums.ContainsKey(Name))
                {
                    TypeName = DbPrimitiveMappings[Enums[Name].UnderlyingType.TypeRef.Value];
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
                if (IsNullable)
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
        }
    }
}
