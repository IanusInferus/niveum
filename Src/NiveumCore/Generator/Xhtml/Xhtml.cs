//==========================================================================
//
//  File:        Xhtml.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构XHTML代码生成器
//  Version:     2018.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;

namespace Niveum.ObjectSchema.Xhtml
{
    public class FileResult
    {
        public String Path;
        public String Content;
    }
    public static class CodeGenerator
    {
        public static List<FileResult> CompileToXhtml(this ObjectSchemaLoaderResult oslr, String Title, String CopyrightText)
        {
            var t = new Templates(oslr);
            var Files = t.GetFiles(oslr.Schema, Title, CopyrightText);
            return Files;
        }
    }

    public partial class Templates
    {
        public Templates(ObjectSchemaLoaderResult oslr)
        {
            TypeInfoDict = new Dictionary<String, TypeInfo>();

            var Schema = oslr.Schema;

            String Root = "";
            if (oslr.Positions.Count > 0)
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
                Root = oslr.Positions.Select(p => FileNameHandling.GetDirectoryPathWithTailingSeparator(FileNameHandling.GetFileDirectory(p.Value.Text.Path))).Aggregate((a, b) => GetCommonHead(a, b));
                if (Root != FileNameHandling.GetDirectoryPathWithTailingSeparator(Root))
                {
                    Root = FileNameHandling.GetFileDirectory(Root);
                }
            }

            var Map = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value);
            foreach (var t in Schema.Types)
            {
                var Name = t.VersionedName();
                var Path = "Default.tree";
                if (oslr.Positions.ContainsKey(t))
                {
                    Path = FileNameHandling.GetRelativePath(oslr.Positions[t].Text.Path, Root);
                }
                var PathWithoutExt = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Path), FileNameHandling.GetMainFileName(Path));
                var DocFilePath = PathWithoutExt.Replace(@"\", @"_").Replace(@"/", @"_").Replace(@".", "_").Replace(@":", @"_").Replace(@"#", @"_") + @".html";
                var tli = new TypeInfo { Def = Map[Name], FriendlyPath = PathWithoutExt.Replace(@"\", @"/"), DocFilePath = DocFilePath, DocPath = String.Format("{0}#{1}", DocFilePath, Name) };
                TypeInfoDict.Add(Name, tli);
            }
        }

        private String GetEscapedIdentifier(String Identifier)
        {
            throw new InvalidOperationException();
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
                var Name = Type.TypeRef.VersionedName();
                if (TypeInfoDict.ContainsKey(Name))
                {
                    var tl = TypeInfoDict[Name];
                    if (IsInBar)
                    {
                        return BarRef(Name, tl.DocPath, tl.Def.Description()).Single();
                    }
                    else
                    {
                        return Ref(Name, tl.DocPath, tl.Def.Description()).Single();
                    }
                }
                else
                {
                    return GetEscaped(Name);
                }
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscaped("'" + Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return GetEscaped("Tuple<") + String.Join(GetEscaped(", "), Type.Tuple.Select(t => GetTypeString(t, WithDescription, IsInBar))) + GetEscaped(">");
            }
            else if (Type.OnGenericTypeSpec)
            {
                return GetTypeString(Type.GenericTypeSpec.TypeSpec, WithDescription, IsInBar) + GetEscaped("<") + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(gpv => GetTypeString(gpv, WithDescription, IsInBar))) + GetEscaped(">");
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
            else if (t.OnAlias)
            {
                return "别名";
            }
            else if (t.OnRecord)
            {
                return "记录";
            }
            else if (t.OnTaggedUnion)
            {
                return "标签联合";
            }
            else if (t.OnEnum)
            {
                return "枚举";
            }
            else if (t.OnClientCommand)
            {
                return "客户端方法";
            }
            else if (t.OnServerCommand)
            {
                return "服务端事件";
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

            var IndexPageCommandContent = new List<String>();
            var IndexPageTypeContent = new List<String>();
            var IndexBarCommandContent = new List<String>();
            var IndexBarTypeContent = new List<String>();

            foreach (var File in Files)
            {
                var Content = new List<String>();

                var IndexPageCommandContentByFile = new List<String>();
                var IndexPageTypeContentByFile = new List<String>();
                var IndexBarCommandContentByFile = new List<String>();
                var IndexBarTypeContentByFile = new List<String>();

                foreach (var t in File.Types)
                {
                    var Lines = Type(t);
                    Content.AddRange(Lines);

                    IndexPageTypeContentByFile.AddRange(TypeBrief(t));
                    IndexBarTypeContentByFile.AddRange(BarTypeBrief(t));
                }

                var Page = PageWrapper(PageContent(File.FriendlyPath, Title, CopyrightText, Content, true));
                l.Add(new FileResult() { Path = File.DocFilePath, Content = String.Join("\r\n", Page) });

                if (IndexPageCommandContentByFile.Count != 0)
                {
                    IndexPageCommandContent.AddRange(Brief(File.FriendlyPath, IndexPageCommandContentByFile));
                }
                if (IndexPageTypeContentByFile.Count != 0)
                {
                    IndexPageTypeContent.AddRange(Brief(File.FriendlyPath, IndexPageTypeContentByFile));
                }
                if (IndexBarCommandContentByFile.Count != 0)
                {
                    IndexBarCommandContent.AddRange(BarBrief(File.FriendlyPath, IndexBarCommandContentByFile));
                }
                if (IndexBarTypeContentByFile.Count != 0)
                {
                    IndexBarTypeContent.AddRange(BarBrief(File.FriendlyPath, IndexBarTypeContentByFile));
                }
            }

            if (IndexPageCommandContent.Count > 0)
            {
                l.Add(new FileResult { Path = "main.html", Content = String.Join("\r\n", PageWrapper(PageContent("所有命令", Title, CopyrightText, IndexPageCommandContent, false))) });
                l.Add(new FileResult { Path = "bar.html", Content = String.Join("\r\n", PageWrapper(BarPageContent("导航栏", Title, IndexBarCommandContent))) });
            }
            else
            {
                l.Add(new FileResult { Path = "main.html", Content = String.Join("\r\n", PageWrapper(PageContent("所有类型", Title, CopyrightText, IndexPageTypeContent, false))) });
                l.Add(new FileResult { Path = "bar.html", Content = String.Join("\r\n", PageWrapper(BarPageContent("导航栏", Title, IndexBarTypeContent))) });
            }
            l.Add(new FileResult { Path = "index.html", Content = String.Join("\r\n", PageWrapper(IndexPageContent("首页", Title))) });

            return l;
        }
    }
}
