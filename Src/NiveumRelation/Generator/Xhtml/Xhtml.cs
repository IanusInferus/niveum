//==========================================================================
//
//  File:        Xhtml.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构XHTML代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;

namespace Niveum.RelationSchema.Xhtml
{
    public sealed class FileResult
    {
        public String Path { get; init; }
        public String Content { get; init; }
    }

    public static class CodeGenerator
    {
        public static List<FileResult> CompileToXhtml(this RelationSchemaLoaderResult rslr, String Title, String CopyrightText)
        {
            var t = new Templates(rslr);
            var Files = t.GetFiles(rslr.Schema, Title, CopyrightText);
            return Files;
        }
    }

    public partial class Templates
    {
        public Templates(RelationSchemaLoaderResult rslr)
        {
            TypeInfoDict = new Dictionary<String, TypeInfo>(StringComparer.OrdinalIgnoreCase);

            var Schema = rslr.Schema;

            String Root = "";
            if (rslr.Positions.Count > 0)
            {
                Func<String, String, String> GetCommonHead = (a, b) =>
                {
                    var lc = new List<Char>();
                    var k = 0;
                    while (true)
                    {
                        if (k >= a.Length || k >= b.Length) { break; }
                        if (a[k] != b[k]) { break; }
                        lc.Add(a[k]);
                        k += 1;
                    }
                    return new String(lc.ToArray());
                };
                Root = rslr.Positions.Select(p => FileNameHandling.GetDirectoryPathWithTailingSeparator(FileNameHandling.GetFileDirectory(p.Value.Text.Path))).Aggregate((a, b) => GetCommonHead(a, b));
                if (Root != FileNameHandling.GetDirectoryPathWithTailingSeparator(Root))
                {
                    Root = FileNameHandling.GetFileDirectory(Root);
                }
            }

            var Map = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value);
            foreach (var t in Schema.Types)
            {
                if (t.OnQueryList) { continue; }
                var Name = t.Name();
                var Path = "Default.tree";
                if (rslr.Positions.ContainsKey(t))
                {
                    Path = FileNameHandling.GetRelativePath(rslr.Positions[t].Text.Path, Root);
                }
                var PathWithoutExt = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Path), FileNameHandling.GetMainFileName(Path));
                var DocFilePath = PathWithoutExt.Replace(@"\", @"_").Replace(@"/", @"_").Replace(@".", "_").Replace(@":", @"_").Replace(@"#", @"_") + @".html";
                var tli = new TypeInfo { Def = Map[Name], FriendlyPath = PathWithoutExt.Replace(@"\", @"/"), DocFilePath = DocFilePath, DocPath = String.Format("{0}#{1}", DocFilePath, Name) };
                TypeInfoDict.Add(Name, tli);
            }
        }

        public static String GetEscaped(String v)
        {
            return v.Replace(@"&", @"&amp;").Replace(@"""", @"&quot;").Replace(@"'", @"&#39;").Replace(@"<", @"&lt;").Replace(@">", @"&gt;").Replace("\r", "&#13;").Replace("\n", "&#10;");
        }

        public class TypeInfo
        {
            public TypeDef Def;
            public String FriendlyPath;
            public String DocFilePath;
            public String DocPath;
        }
        private Dictionary<String, TypeInfo> TypeInfoDict;

        public String GetTypeString(TypeSpec Type, Boolean WithDescription, Boolean IsInBar = false)
        {
            if (Type.OnTypeRef)
            {
                var Name = Type.TypeRef.Value;
                if (TypeInfoDict.ContainsKey(Name))
                {
                    var tl = TypeInfoDict[Name];
                    if (IsInBar)
                    {
                        return BarRef(Name, tl.DocPath, WithDescription ? tl.Def.Description() : "").Single();
                    }
                    else
                    {
                        return Ref(Name, tl.DocPath, WithDescription ? tl.Def.Description() : "").Single();
                    }
                }
                else
                {
                    return GetEscaped(Name);
                }
            }
            else if (Type.OnList)
            {
                return GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = "List" }), WithDescription, IsInBar) + GetEscaped("<") + GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = Type.List.Value }), WithDescription, IsInBar) + GetEscaped(">");
            }
            else if (Type.OnOptional)
            {
                return GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = "Optional" }), WithDescription, IsInBar) + GetEscaped("<") + GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = Type.Optional.Value }), WithDescription, IsInBar) + GetEscaped(">");
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public String GetMetaType(TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return "基元";
            }
            else if (t.OnEntity)
            {
                return "实体";
            }
            else if (t.OnEnum)
            {
                return "枚举";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public String GetFieldTypeString(VariableDef v)
        {
            if (v.Attribute.OnColumn)
            {
                return GetTypeString(v.Type, true);
            }
            else if (v.Attribute.OnNavigation)
            {
                return GetEscaped("[导航] ") + GetTypeString(v.Type, true);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public List<FileResult> GetFiles(Schema Schema, String Title, String CopyrightText)
        {
            var Types = Schema.GetMap();
            var Files = Types.Where(Type => TypeInfoDict.ContainsKey(Type.Key)).GroupBy(Type => CollectionOperations.CreatePair(TypeInfoDict[Type.Key].FriendlyPath, TypeInfoDict[Type.Key].DocFilePath), (Pair, gt) => new { FriendlyPath = Pair.Key, DocFilePath = Pair.Value, Types = gt.Select(t => t.Value).ToList() }).ToList();

            var l = new List<FileResult>();

            l.Add(new FileResult { Path = "style.css", Content = String.Join("\r\n", Css()) });

            var IndexPageTypeContent = new List<String>();
            var IndexBarTypeContent = new List<String>();

            foreach (var File in Files)
            {
                var Content = new List<String>();

                var IndexPageTypeContentByFile = new List<String>();
                var IndexBarTypeContentByFile = new List<String>();

                foreach (var t in File.Types)
                {
                    if (t.OnQueryList) { continue; }
                    var Lines = Type(t);
                    Content.AddRange(Lines);

                    IndexPageTypeContentByFile.AddRange(TypeBrief(t));
                    IndexBarTypeContentByFile.AddRange(BarTypeBrief(t));
                }

                var Page = PageWrapper(PageContent(File.FriendlyPath, Title, CopyrightText, Content, true));
                l.Add(new FileResult() { Path = File.DocFilePath, Content = String.Join("\r\n", Page) });

                if (IndexPageTypeContentByFile.Count != 0)
                {
                    IndexPageTypeContent.AddRange(Brief(File.FriendlyPath, IndexPageTypeContentByFile));
                }
                if (IndexBarTypeContentByFile.Count != 0)
                {
                    IndexBarTypeContent.AddRange(BarBrief(File.FriendlyPath, IndexBarTypeContentByFile));
                }
            }

            l.Add(new FileResult { Path = "main.html", Content = String.Join("\r\n", PageWrapper(PageContent("所有类型", Title, CopyrightText, IndexPageTypeContent, false))) });
            l.Add(new FileResult { Path = "bar.html", Content = String.Join("\r\n", PageWrapper(BarPageContent("导航栏", Title, IndexBarTypeContent))) });
            l.Add(new FileResult { Path = "index.html", Content = String.Join("\r\n", PageWrapper(IndexPageContent("首页", Title))) });

            return l;
        }
    }
}
