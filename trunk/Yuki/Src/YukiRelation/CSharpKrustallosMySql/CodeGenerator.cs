//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos-MySQL代码生成器
//  Version:     2014.10.25.
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

namespace Yuki.RelationSchema.CSharpKrustallosMySql
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpKrustallosMySql(this Schema Schema, String EntityNamespaceName, String KrustallosContextNamespaceName, String MySqlContextNamespaceName, String ContextNamespaceName)
        {
            Writer w = new Writer(Schema, EntityNamespaceName, KrustallosContextNamespaceName, MySqlContextNamespaceName, ContextNamespaceName);
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
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, TypeDef> TypeDict;
            private Dictionary<String, OS.TypeDef> InnerTypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpKrustallosMySql);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String EntityNamespaceName, String KrustallosContextNamespaceName, String MySqlContextNamespaceName, String NamespaceName)
            {
                this.Schema = Schema;
                this.EntityNamespaceName = EntityNamespaceName;
                this.KrustallosContextNamespaceName = KrustallosContextNamespaceName;
                this.MySqlContextNamespaceName = MySqlContextNamespaceName;
                this.NamespaceName = NamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema);
                TypeDict = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                InnerTypeDict = Yuki.ObjectSchema.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CSharpPlain.CodeGenerator.Writer(Schema, NamespaceName);
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                var Imports = new List<String>();
                if (KrustallosContextNamespaceName != "")
                {
                    Imports.Add(KrustallosContextNamespaceName);
                }
                if (MySqlContextNamespaceName != "")
                {
                    Imports.Add(MySqlContextNamespaceName);
                }
                Imports.AddRange(Schema.Imports);

                var DataLoad = GetDataLoad();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Imports.ToArray()).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes).Substitute("DataLoad", DataLoad)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Imports.ToArray()).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes).Substitute("DataLoad", DataLoad)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
            }

            public String[] GetHeader()
            {
                if (EntityNamespaceName == NamespaceName || EntityNamespaceName == "")
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", new String[] { });
                }
                else
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", EntityNamespaceName);
                }
            }

            public String[] GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String[] GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
                var QueryName = q.FriendlyName();
                var Parameters = InnerWriter.GetQueryParameterList(q);
                String[] Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    Content = GetTemplate("SelectLock").Substitute("QueryName", QueryName).Substitute("Parameters", Parameters);
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert || q.Verb.OnDelete)
                {
                    Content = GetTemplate("InsertUpdateUpsertDelete").Substitute("QueryName", QueryName).Substitute("Parameters", Parameters);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return GetTemplate("Query").Substitute("Signature", Signature).Substitute("Content", Content);
            }

            public String[] GetComplexTypes()
            {
                var l = new List<String>();

                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("DataAccessBase"));
                l.Add("");

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToArray();
                var ql = new List<String>();
                foreach (var q in Queries)
                {
                    ql.AddRange(GetQuery(q));
                    ql.Add("");
                }
                if (ql.Count > 0)
                {
                    ql = ql.Take(ql.Count - 1).ToList();
                }
                l.AddRange(GetTemplate("DataAccess").Substitute("Queries", ql.ToArray()));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessPool").Substitute("Hash", Hash));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
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

            public String[] GetDataLoadLoads()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys.Select(k => ConvertNonUniqueKeyToUniqueKey(k, e.PrimaryKey))).ToArray();
                    var IndexNames = new List<String>();
                    var Updates = new List<String>();
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        IndexNames.Add(IndexName);
                        Updates.AddRange(GetTemplate("DataLoad_Update").Substitute("IndexName", IndexName).Substitute("Key", Key));
                    }
                    var SQL = GetSelectAllQueryString(e);
                    var ResultSets = new List<String>();
                    var Columns = e.Fields.Where(f => f.Attribute.OnColumn).ToArray();
                    int j = 0;
                    foreach (var c in Columns)
                    {
                        if (j == Columns.Length - 1)
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet_Last").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        else
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        j += 1;
                    }
                    l.AddRange(GetTemplate("DataLoad_Load").Substitute("IndexNames", IndexNames.ToArray()).Substitute("Updates", Updates.ToArray()).Substitute("EntityName", e.Name).Substitute("SQL", SQL).Substitute("ResultSets", ResultSets.ToArray()));
                }
                return l.ToArray();
            }

            public String[] GetDataLoad()
            {
                var Loads = GetDataLoadLoads();

                if (MySqlContextNamespaceName == "")
                {
                    return GetTemplate("DataLoadWithoutNamespace").Substitute("Loads", Loads.ToArray());
                }
                else
                {
                    return GetTemplate("DataLoadWithNamespace").Substitute("NamespaceName", MySqlContextNamespaceName).Substitute("Loads", Loads.ToArray());
                }
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static String[] GetLines(String Value)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
