//==========================================================================
//
//  File:        CppPlain.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C++简单类型代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CppPlain
{
    public static class CodeGenerator
    {
        public static String CompileToCppPlain(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema, NamespaceName);
            var Lines = t.GetSchema().Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }

        public static List<String> Substitute(List<String> Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var l = new List<String>();
            foreach (var Line in Lines)
            {
                var NewLine = Line;
                if (Line.Contains(ParameterString))
                {
                    NewLine = NewLine.Replace(ParameterString, Value);
                }
                l.Add(NewLine);
            }
            return l;
        }
        public static List<String> Substitute(List<String> Lines, String Parameter, IEnumerable<String> Value)
        {
            var l = new List<String>();
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
            return l;
        }
    }

    public partial class Templates
    {
        private OS.Cpp.Templates Inner;
        private Schema Schema;
        private String NamespaceName;
        private OS.Schema InnerSchema;
        private Dictionary<String, OS.TypeDef> TypeDict;

        public Templates(Schema Schema, String NamespaceName)
        {
            this.Schema = Schema;
            this.NamespaceName = NamespaceName;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, NamespaceName);
            TypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);
            Inner = new OS.Cpp.Templates(InnerSchema);
        }

        public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
        {
            return Inner.WrapNamespace(Namespace, Contents);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public Boolean IsInclude(String s)
        {
            return Inner.IsInclude(s);
        }

        public IEnumerable<String> GetPrimitives()
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
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnOne)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
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
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                }
                else if (q.Numeral.OnMany)
                {
                    pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
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
                    Type = GetEscapedIdentifier("std::optional<std::shared_ptr<class {0}>>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnOne)
                {
                    Type = GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnMany)
                {
                    Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnAll)
                {
                    Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
                }
                else if (q.Numeral.OnRange)
                {
                    Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
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

        public IEnumerable<String> GetDataAccessTypes()
        {
            var l = new List<String>();

            var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();

            if (Queries.Count > 0)
            {
                l.AddRange(IDataAccess(Queries.Select(q => GetQuerySignature(q)).ToList()));
                l.Add("");
                l.AddRange(IDataAccessPool());
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

            l.AddRange(WrapNamespace(NamespaceName, GetDataAccessTypes()));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetSchema()
        {
            var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
            var Types = GetTypes();
            return Main(Includes, Types);
        }
    }
}
