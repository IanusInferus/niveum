//==========================================================================
//
//  File:        CSharpLinqToEntities.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C# Linq to Entities数据库代码生成器
//  Version:     2026.06.04.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.TextEncoding;
using Niveum.RelationSchema;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpLinqToEntities
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpLinqToEntities(this Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var w = new Templates(Schema, DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            var a = w.GetSchema();
            return String.Join("\r\n", a.Select(Line => Line.TrimEnd(' ')));
        }
    }

    public partial class Templates
    {
        private OS.CSharp.Templates Inner;
        private Schema Schema;
        private String DatabaseName;
        private String EntityNamespaceName;
        private String ContextNamespaceName;
        private String ContextClassName;

        private Dictionary<String, EnumDef> Enums;
        private Dictionary<String, EntityDef> Records;

        public Templates(Schema Schema, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            this.Inner = new OS.CSharp.Templates(new OS.Schema
            {
                Types = new List<OS.TypeDef> { },
                TypeRefs = new List<OS.TypeDef>
                {
                    OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Unit" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } }),
                    OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Boolean" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } })
                },
                Imports = new List<String> { }
            });

            this.Schema = Schema;
            this.DatabaseName = DatabaseName;
            this.EntityNamespaceName = EntityNamespaceName;
            this.ContextNamespaceName = ContextNamespaceName;
            this.ContextClassName = ContextClassName;

            Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            Records = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public IEnumerable<String> GetXmlComment(String Description)
        {
            if (Description == "") { return new List<String> { }; }

            var d = Description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

            var Lines = d.UnifyNewLineToLf().Split('\n').ToList();
            if (Lines.Count == 1)
            {
                return SingleLineXmlComment(d);
            }
            else
            {
                return MultiLineXmlComment(Lines);
            }
        }

        public String GetEnumTypeString(TypeSpec Type)
        {
            if (!Type.OnTypeRef)
            {
                throw new InvalidOperationException();
            }
            if (!PrimitiveMapping.ContainsKey(Type.TypeRef.Value))
            {
                return GetEscapedIdentifier(Type.TypeRef.Value);
            }
            switch (PrimitiveMapping[Type.TypeRef.Value])
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

        private Boolean IsValueType(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.Value))
                {
                    var t = PrimitiveMapping[Type.TypeRef.Value];
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

        public String GetPropertyTypeString(VariableDef f)
        {
            var Type = f.Type;

            if (f.Attribute.OnColumn)
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
                if (PrimitiveMapping.ContainsKey(Name))
                {
                    var PlatformName = PrimitiveMapping[Name];
                    if (PlatformName.Contains("[") || PlatformName.Contains("]"))
                    {
                        Name = PlatformName;
                    }
                    if (IsNullable && IsValueType(Type))
                    {
                        return GetEscapedIdentifier(Name) + "?";
                    }
                    else
                    {
                        return Name;
                    }
                }
                else if (Enums.ContainsKey(Name))
                {
                    if (IsNullable)
                    {
                        return GetEscapedIdentifier(Name) + "?";
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
                else if (Type.OnOptional)
                {
                    return Type.Optional.Value;
                }
                else if (Type.OnList)
                {
                    return "ICollection<" + GetEscapedIdentifier(Type.List.Value) + ">";
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

        public String GetColumnAttributes(EntityDef r, VariableDef f, int Index)
        {
            var a = f.Attribute.Column;
            var l = new List<String>();
            l.Add(String.Format(@"Column(""{0}"", Order = {1})", f.Name, Index.ToInvariantString()));
            if (r.PrimaryKey.Columns.Select(co => co.Name).Contains(f.Name, StringComparer.OrdinalIgnoreCase))
            {
                l.Add("Key");
            }
            if (a.IsIdentity)
            {
                l.Add("DatabaseGenerated(DatabaseGeneratedOption.Identity)");
            }
            if (!f.Type.OnOptional)
            {
                l.Add("Required");
            }
            return String.Join(", ", l.ToArray());
        }

        public String GetAssociationParameters(EntityDef r, VariableDef f)
        {
            var a = f.Attribute.Navigation;
            var l = new List<String>();
            ForeignKey fk;
            if (a.IsReverse)
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
                fk = new ForeignKey { ThisTableName = ThisTable.CollectionName, ThisKeyColumns = f.Attribute.Navigation.OtherKey, OtherTableName = r.CollectionName, OtherKeyColumns = f.Attribute.Navigation.ThisKey };
            }
            else
            {
                fk = new ForeignKey { ThisTableName = r.CollectionName, ThisKeyColumns = f.Attribute.Navigation.ThisKey, OtherTableName = Records[f.Type.TypeRef.Value].CollectionName, OtherKeyColumns = f.Attribute.Navigation.OtherKey };
            }
            var AssociationName = fk.ThisTableName + "_" + String.Join("_", fk.ThisKeyColumns) + "_" + fk.OtherTableName + "_" + String.Join("_", fk.OtherKeyColumns);
            l.Add(String.Format(@"Association(""{0}"", ""{1}"", ""{2}"", IsForeignKey = {3})", AssociationName, String.Join(", ", a.ThisKey), String.Join(", ", a.OtherKey), !a.IsReverse ? "true" : "false"));
            return String.Join(", ", l.ToArray());
        }

        public IEnumerable<String> GetProperty(EntityDef r, VariableDef f, int Index)
        {
            var Type = f.Type;
            var PropertyType = GetPropertyTypeString(f);
            if (f.Attribute.OnColumn)
            {
                var ColumnAttributes = GetColumnAttributes(r, f, Index);
                var d = f.Description;
                if (f.Type.OnOptional)
                {
                    if (d == "")
                    {
                        d = "Optional";
                    }
                    else
                    {
                        d = "Optional\r\n" + d;
                    }
                }
                return ColumnProperty(f.Name, ColumnAttributes, PropertyType, GetXmlComment(d));
            }
            else if (f.Attribute.OnNavigation)
            {
                var a = f.Attribute.Navigation;
                var AssociationParameters = GetAssociationParameters(r, f);
                return AssociationProperty(f.Name, AssociationParameters, PropertyType, GetXmlComment(f.Description));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public IEnumerable<String> GetProperties(EntityDef r)
        {
            var l = new List<String>();
            int Index = 0;
            foreach (var f in r.Fields)
            {
                l.AddRange(GetProperty(r, f, Index));
                Index += 1;
            }
            return l;
        }

        public IEnumerable<String> GetTable(EntityDef r)
        {
            var Properties = GetProperties(r).ToList();
            return Table(r.Name, r.CollectionName, Properties, GetXmlComment(r.Description));
        }

        public IEnumerable<String> GetTableGetter(EntityDef r)
        {
            return TableGetter(r.Name, r.CollectionName, GetXmlComment(r.Description));
        }

        public IEnumerable<String> GetContext()
        {
            var l = new List<String>();
            foreach (var t in Schema.Types)
            {
                if (!t.OnEntity) { continue; }
                l.AddRange(GetTableGetter(t.Entity));
            }
            var TableGetters = l.ToList();
            return Context(ContextClassName, TableGetters);
        }

        public IEnumerable<String> GetIReadonlyContextTableGetter(EntityDef r)
        {
            return IReadonlyContextTableGetter(r.Name, r.CollectionName, GetXmlComment(r.Description));
        }

        public IEnumerable<String> GetIReadonlyContext()
        {
            var TableGetters = Schema.Types.Where(t => t.OnEntity).SelectMany(t => GetIReadonlyContextTableGetter(t.Entity)).ToList();
            return IReadonlyContext(TableGetters);
        }

        public String GetKeyParameters(EntityDef r, Key k)
        {
            var l = new List<String>();
            foreach (var c in k.Columns)
            {
                var t = GetPropertyTypeString(r.Fields.Where(f => f.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)).Single());
                l.Add(String.Format("{0} {1}", t, c.Name));
            }
            return String.Join(", ", l.ToArray());
        }

        public String GetKeyWhereExpressions(EntityDef r, Key k)
        {
            var l = new List<String>();
            foreach (var c in k.Columns)
            {
                l.Add(String.Format("Where(e => e.{0} == {0})", c.Name));
            }
            return String.Join(".", l.ToArray());
        }

        public IEnumerable<String> GetWhereKeyIs(EntityDef r, Key k)
        {
            var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
            var KeyParameters = GetKeyParameters(r, k);
            var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
            return WhereKeyIs(r.Name, KeyFriendlyName, KeyParameters, KeyWhereExpressions);
        }

        public IEnumerable<String> GetByKey(EntityDef r, Key k)
        {
            var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
            var KeyParameters = GetKeyParameters(r, k);
            var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
            return ByKey(r.Name, KeyFriendlyName, KeyParameters, KeyWhereExpressions);
        }

        public IEnumerable<String> GetByKeyT(EntityDef r, Key k)
        {
            var KeyFriendlyName = String.Join("And", k.Columns.Select(c => c.Name).ToArray());
            var KeyParameters = GetKeyParameters(r, k);
            var KeyWhereExpressions = GetKeyWhereExpressions(r, k);
            return ByKeyT(r.Name, KeyFriendlyName, KeyParameters, KeyWhereExpressions);
        }

        public IEnumerable<String> GetMethods(List<EntityDef> Records)
        {
            var l = new List<String>();
            foreach (var r in Records)
            {
                var Keys = (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys).Concat(r.NonUniqueKeys);
                var UniqueKeys = (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys);
                var h = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in Keys)
                {
                    for (int m = 1; m <= k.Columns.Count; m += 1)
                    {
                        var Subkey = new Key { Columns = k.Columns.Take(m).ToList(), IsClustered = k.IsClustered };
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
            return l;
        }

        public IEnumerable<String> GetDbExtensions()
        {
            return DbExtensions(GetMethods(Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList()).ToList());
        }

        public IEnumerable<String> GetPrimitive(String Name, String PlatformName)
        {
            return Primitive(Name, PlatformName);
        }

        public IEnumerable<String> GetPrimitives()
        {
            var l = new List<String>();
            foreach (var p in Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnPrimitive).Select(c => c.Primitive))
            {
                if (PrimitiveMapping.ContainsKey(p.Name))
                {
                    var Name = p.Name;
                    var PlatformName = PrimitiveMapping[Name];
                    if (Name != PlatformName && !(PlatformName.Contains("[") || PlatformName.Contains("]")))
                    {
                        l.AddRange(GetPrimitive(Name, PlatformName));
                    }
                }
                else if (p.Name == "Unit" || p.Name == "Type" || p.Name == "Byte" || p.Name == "List" || p.Name == "Optional")
                {
                }
                else
                {
                    throw new NotSupportedException(p.Name);
                }
            }
            return l;
        }

        public IEnumerable<String> GetLiteral(LiteralDef lrl)
        {
            return Literal(lrl.Name, lrl.Value.ToInvariantString(), GetXmlComment(lrl.Description));
        }

        public IEnumerable<String> GetLiterals(IEnumerable<LiteralDef> Literals)
        {
            var l = new List<String>();
            foreach (var lrl in Literals)
            {
                l.AddRange(GetLiteral(lrl));
            }
            return l;
        }

        public IEnumerable<String> GetEnum(EnumDef e)
        {
            var Literals = GetLiterals(e.Literals).ToList();
            return Enum(e.Name, GetEnumTypeString(e.UnderlyingType), Literals, GetXmlComment(e.Description));
        }

        public IEnumerable<String> GetEntityComplexTypes()
        {
            var l = new List<String>();

            foreach (var c in Schema.Types)
            {
                if (c.OnEnum)
                {
                    l.AddRange(GetEnum(c.Enum));
                    l.Add("");
                }
                else if (c.OnEntity)
                {
                    l.AddRange(GetTable(c.Entity));
                    l.Add("");
                }
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetContextComplexTypes()
        {
            var l = new List<String>();

            l.AddRange(GetContext());
            l.Add("");
            l.AddRange(GetIReadonlyContext());
            l.Add("");
            l.AddRange(GetDbExtensions());
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetSchema()
        {
            var Primitives = GetPrimitives().ToList();
            var EntityComplexTypes = GetEntityComplexTypes().ToList();
            var ContextComplexTypes = GetContextComplexTypes().ToList();

            return Main(EntityNamespaceName, ContextNamespaceName, Schema.Imports, Primitives, EntityComplexTypes, ContextComplexTypes);
        }
    }
}
