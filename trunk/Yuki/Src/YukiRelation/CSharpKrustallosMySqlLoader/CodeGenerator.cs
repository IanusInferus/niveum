//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos-MySQL加载代码生成器
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.CSharpKrustallosMySqlLoader
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpKrustallosMySqlLoader(this Schema Schema, String EntityNamespaceName, String KrustallosContextNamespaceName, String MySqlContextNamespaceName)
        {
            var w = new Writer(Schema, EntityNamespaceName, KrustallosContextNamespaceName, MySqlContextNamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private CSharpPlain.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String EntityNamespaceName;
            private String KrustallosContextNamespaceName;
            private String MySqlContextNamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, OS.TypeDef> InnerTypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpKrustallosMySqlLoader);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String EntityNamespaceName, String KrustallosContextNamespaceName, String MySqlContextNamespaceName)
            {
                this.Schema = Schema;
                this.EntityNamespaceName = EntityNamespaceName;
                this.KrustallosContextNamespaceName = KrustallosContextNamespaceName;
                this.MySqlContextNamespaceName = MySqlContextNamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema);
                InnerTypeDict = Yuki.ObjectSchema.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CSharpPlain.CodeGenerator.Writer(Schema, MySqlContextNamespaceName);
            }

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                var Imports = new List<String>();
                if (KrustallosContextNamespaceName != "")
                {
                    Imports.Add(KrustallosContextNamespaceName);
                }
                Imports.AddRange(Schema.Imports);

                if (MySqlContextNamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", MySqlContextNamespaceName).Substitute("Imports", Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public List<String> GetHeader()
            {
                if (EntityNamespaceName == MySqlContextNamespaceName || EntityNamespaceName == "")
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", new List<String> { });
                }
                else
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", EntityNamespaceName);
                }
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();

                var Loads = GetDataLoadLoads();
                l.AddRange(GetTemplate("DataLoad").Substitute("Loads", Loads));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l;
            }

            public String GetTypeGetName(TypeSpec t)
            {
                if (t.OnTypeRef)
                {
                    return "Get" + t.TypeRef.Value;
                }
                else if (t.OnOptional)
                {
                    return "GetOptionalOf" + t.Optional.Value;
                }
                else if (t.OnList)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private String GetSelectAllQueryString(EntityDef e)
            {
                var l = new List<String>();
                l.Add("SELECT " + String.Join(", ", e.Fields.Where(f => f.Attribute.OnColumn).Select(f => "`{0}`".Formats(f.Name))));
                l.Add("FROM `{0}`".Formats(e.CollectionName));
                return String.Join(" ", l.ToArray());
            }

            public Key ConvertNonUniqueKeyToUniqueKey(Key NonUniqueKey, Key PrimaryKey)
            {
                return new Key { Columns = NonUniqueKey.Columns.Concat(PrimaryKey.Columns.Select(c => c.Name).Except(NonUniqueKey.Columns.Select(c => c.Name)).Select(Name => new KeyColumn { Name = Name, IsDescending = false })).ToList(), IsClustered = NonUniqueKey.IsClustered };
            }

            public List<String> GetDataLoadLoads()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys.Select(k => ConvertNonUniqueKeyToUniqueKey(k, e.PrimaryKey))).Select(k => new Key { Columns = k.Columns, IsClustered = false }).Distinct(new KeyComparer()).ToList();
                    var IndexNames = new List<String>();
                    var Partitions = new List<String>();
                    var Updates = new List<String>();
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        var FirstColumnName = k.Columns.First().Name;
                        var FirstColumnType = d[FirstColumnName].Type;
                        var PartitionIndex = (FirstColumnType.OnTypeRef && FirstColumnType.TypeRef.Name.Equals("Int", StringComparison.OrdinalIgnoreCase)) ? ("v.[[" + FirstColumnName + "]] % Data.[[${IndexName}]].NumPartition") : "0";
                        IndexNames.Add(IndexName);
                        Partitions.AddRange(GetTemplate("DataLoad_Partition").Substitute("PartitionIndex", PartitionIndex).Substitute("IndexName", IndexName));
                        Updates.AddRange(GetTemplate("DataLoad_Update").Substitute("IndexName", IndexName).Substitute("Key", Key));
                    }
                    var SQL = GetSelectAllQueryString(e);
                    var ResultSets = new List<String>();
                    var Columns = e.Fields.Where(f => f.Attribute.OnColumn).ToList();
                    int j = 0;
                    foreach (var c in Columns)
                    {
                        if (j == Columns.Count - 1)
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet_Last").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        else
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        j += 1;
                    }
                    l.AddRange(GetTemplate("DataLoad_Load").Substitute("IndexNames", IndexNames).Substitute("Partitions", Partitions).Substitute("Updates", Updates).Substitute("EntityName", e.Name).Substitute("SQL", SQL).Substitute("ResultSets", ResultSets));
                }
                return l;
            }

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
