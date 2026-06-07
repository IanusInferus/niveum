//==========================================================================
//
//  File:        CSharpPlain.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C#简单类型代码生成器
//  Version:     2026.06.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpPlain
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpPlain(this Schema Schema, String NamespaceName, Boolean WithFirefly, Boolean EnableNullableDeclaration)
        {
            var t = new Templates(Schema, NamespaceName, WithFirefly, EnableNullableDeclaration);
            var Lines = t.GetSchema().Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
    }

    public partial class Templates
    {
        private OS.CSharp.Templates Inner;
        private Schema Schema;
        private String NamespaceName;
        private OS.Schema InnerSchema;
        private Dictionary<String, OS.TypeDef> TypeDict;
        private Boolean EnableNullableDeclaration;

        public Templates(Schema Schema, String NamespaceName, Boolean WithFirefly = true, Boolean EnableNullableDeclaration = false)
        {
            this.Schema = Schema;
            this.NamespaceName = NamespaceName;
            this.EnableNullableDeclaration = EnableNullableDeclaration;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, NamespaceName);
            TypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);
            Inner = new OS.CSharp.Templates(InnerSchema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public List<String> GetPrimitives()
        {
            return Inner.GetPrimitives(InnerSchema);
        }

        public String GetTypeString(OS.TypeSpec Type)
        {
            return Inner.GetTypeString(Type, NamespaceName);
        }

        public String GetQuerySignature(QueryDef q)
        {
            var or = TypeDict[q.EntityName].Record;
            var Name = q.FriendlyName();
            var pl = new List<String>();
            if (q.Verb.OnInsert || q.Verb.OnUpdate)
            {
                if (q.Numeral.OnOptional)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier(q.EntityName), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnOne)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier(q.EntityName), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("List<{0}>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (q.Verb.OnUpsert)
            {
                if (q.Numeral.OnOne)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier(q.EntityName), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("List<{0}>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            pl.AddRange(q.By.Select(c => "{0} {1}".Formats(GetEscapedIdentifier(GetTypeString(or.Fields.Where(f => f.Name == c).Single().Type)), GetEscapedIdentifier(c))).ToArray());
            if (q.Numeral.OnRange)
            {
                pl.Add("Int _Skip_");
                pl.Add("Int _Take_");
            }
            var ParameterList = String.Join(", ", pl.ToArray());
            String Type;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                if (q.Numeral.OnOptional)
                {
                    Type = GetEscapedIdentifier("Optional<{0}>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnOne)
                {
                    Type = GetEscapedIdentifier(q.EntityName);
                }
                else if (q.Numeral.OnMany)
                {
                    Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnAll)
                {
                    Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnRange)
                {
                    Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnCount)
                {
                    Type = GetEscapedIdentifier("Int");
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                Type = "void";
            }
            return QuerySignature(Name, ParameterList, Type).Single();
        }

        public String GetQueryParameterList(QueryDef q)
        {
            var pl = new List<String>();
            if (q.Verb.OnInsert || q.Verb.OnUpdate)
            {
                if (q.Numeral.OnOptional)
                {
                    pl.Add(GetEscapedIdentifier("v"));
                }
                else if (q.Numeral.OnOne)
                {
                    pl.Add(GetEscapedIdentifier("v"));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add(GetEscapedIdentifier("l"));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (q.Verb.OnUpsert)
            {
                if (q.Numeral.OnOne)
                {
                    pl.Add(GetEscapedIdentifier("v"));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add(GetEscapedIdentifier("l"));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            pl.AddRange(q.By.Select(c => GetEscapedIdentifier(c)).ToArray());
            if (q.Numeral.OnRange)
            {
                pl.Add("_Skip_");
                pl.Add("_Take_");
            }
            return String.Join(", ", pl.ToArray());
        }

        public IEnumerable<String> GetDataAccessTypes()
        {
            var l = new List<String>();
            var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();
            if (Queries.Count > 0)
            {
                l.AddRange(IDataAccess(Queries.Select(q => GetQuerySignature(q)).ToList()));
                l.Add("");
                l.AddRange(ITransactionLock());
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetTypes()
        {
            var l = new List<String>();
            l.AddRange(Inner.GetTypes(InnerSchema, NamespaceName));
            l.Add("");
            l.AddRange(Inner.WrapNamespace(NamespaceName, GetDataAccessTypes()));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }
            
            return l;
        }

        public IEnumerable<String> GetSchema()
        {
            var Types = GetTypes();
            return Main(Schema.Imports, Types, EnableNullableDeclaration);
        }
    }
}
