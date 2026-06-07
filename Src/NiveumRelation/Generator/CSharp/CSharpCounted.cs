//==========================================================================
//
//  File:        CSharpCounted.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C# 计时包装代码生成器
//  Version:     2026.06.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpCounted
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpCounted(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName, Boolean EnableNullableDeclaration)
        {
            var t = new Templates(Schema, EntityNamespaceName, ContextNamespaceName, EnableNullableDeclaration);
            var Lines = t.GetSchema().Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
    }

    public partial class Templates
    {
        private CSharpPlain.Templates Inner;
        private Schema Schema;
        private String EntityNamespaceName;
        private String NamespaceName;
        private OS.Schema InnerSchema;
        private Dictionary<String, TypeDef> TypeDict;
        private Boolean EnableNullableDeclaration;

        public Templates(Schema Schema, String EntityNamespaceName, String NamespaceName, Boolean EnableNullableDeclaration)
        {
            this.Schema = Schema;
            this.EntityNamespaceName = EntityNamespaceName;
            this.NamespaceName = NamespaceName;
            this.EnableNullableDeclaration = EnableNullableDeclaration;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, EntityNamespaceName);
            TypeDict = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            Inner = new CSharpPlain.Templates(Schema, EntityNamespaceName);

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public IEnumerable<String> GetPrimitives()
        {
            return Inner.GetPrimitives();
        }

        public IEnumerable<String> GetQuery(QueryDef q)
        {
            var Signature = Inner.GetQuerySignature(q);
            var QueryName = q.FriendlyName();
            var Parameters = Inner.GetQueryParameterList(q);
            IEnumerable<String> Content;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                Content = SelectLock(QueryName, Parameters);
            }
            else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert || q.Verb.OnDelete)
            {
                Content = InsertUpdateUpsertDelete(QueryName, Parameters);
            }
            else
            {
                throw new InvalidOperationException();
            }
            return Query(Signature, Content);
        }

        public IEnumerable<String> GetComplexTypes()
        {
            var l = new List<String>();

            l.AddRange(DataAccessBase());
            l.Add("");

            var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();
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
            l.AddRange(DataAccess(ql));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetSchema()
        {
            var Primitives = GetPrimitives();
            var ComplexTypes = GetComplexTypes();

            return Main(NamespaceName, Schema.Imports, Primitives, ComplexTypes, EnableNullableDeclaration);
        }
    }
}
