//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构XHTML代码生成器
//  Version:     2016.05.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.Xhtml
{
    public class FileResult
    {
        public String Path;
        public String Content;
    }
    public static class CodeGenerator
    {
        public static List<FileResult> CompileToXhtml(this Schema Schema, String Title, String CopyrightText)
        {
            var w = new Common.CodeGenerator.Writer(Schema, Title, CopyrightText);
            var Files = w.GetFiles();
            return Files;
        }
    }
}

namespace Yuki.ObjectSchema.Xhtml.Common
{
    public static class CodeGenerator
    {
        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String Title;
            private String CopyrightText;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Xhtml);
            }

            public Writer(Schema Schema, String Title, String CopyrightText)
            {
                this.Schema = Schema;
                this.Title = Title;
                this.CopyrightText = CopyrightText;

                TypeInfoDict = new Dictionary<String, TypeInfo>(StringComparer.OrdinalIgnoreCase);

                String Root = "";
                if (Schema.TypePaths.Count > 0)
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
                    Root = Schema.TypePaths.Select(tp => FileNameHandling.GetDirectoryPathWithTailingSeparator(FileNameHandling.GetFileDirectory(tp.Path))).Aggregate((a, b) => GetCommonHead(a, b));
                    if (Root != FileNameHandling.GetDirectoryPathWithTailingSeparator(Root))
                    {
                        Root = FileNameHandling.GetFileDirectory(Root);
                    }
                }

                var Map = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value);
                foreach (var p in Schema.TypePaths)
                {
                    var Path = FileNameHandling.GetRelativePath(p.Path, Root);
                    var PathWithoutExt = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Path), FileNameHandling.GetMainFileName(Path));
                    var DocFilePath = PathWithoutExt.Replace(@"\", @"_").Replace(@"/", @"_").Replace(@".", "_").Replace(@":", @"_").Replace(@"#", @"_") + @".html";
                    var tli = new TypeInfo { Def = Map[p.Name], FriendlyPath = PathWithoutExt.Replace(@"\", @"/"), DocFilePath = DocFilePath, DocPath = String.Format("{0}#{1}", DocFilePath, p.Name) };
                    TypeInfoDict.Add(p.Name, tli);
                }
                foreach (var t in Schema.Types)
                {
                    if (!TypeInfoDict.ContainsKey(t.VersionedName()))
                    {
                        var p = new TypePath { Name = t.VersionedName(), Path = "Default.tree" };
                        var Path = FileNameHandling.GetRelativePath(p.Path, Root);
                        var PathWithoutExt = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Path), FileNameHandling.GetMainFileName(Path));
                        var DocFilePath = PathWithoutExt.Replace(@"\", @"_").Replace(@"/", @"_").Replace(@".", "_").Replace(@":", @"_").Replace(@"#", @"_") + @".html";
                        var tli = new TypeInfo { Def = Map[p.Name], FriendlyPath = PathWithoutExt.Replace(@"\", @"/"), DocFilePath = DocFilePath, DocPath = String.Format("{0}#{1}", DocFilePath, p.Name) };
                        TypeInfoDict.Add(p.Name, tli);
                    }
                }
            }

            public class TypeInfo
            {
                public TypeDef Def;
                public String FriendlyPath;
                public String DocFilePath;
                public String DocPath;
            }
            private Dictionary<String, TypeInfo> TypeInfoDict;

            public List<FileResult> GetFiles()
            {
                var Types = Schema.GetMap();
                var Files = Types.GroupBy(Type => CollectionOperations.CreatePair(TypeInfoDict[Type.Key].FriendlyPath, TypeInfoDict[Type.Key].DocFilePath), (Pair, gt) => new { FriendlyPath = Pair.Key, DocFilePath = Pair.Value, Types = gt.Select(t => t.Value).ToList() }).ToList();

                var l = new List<FileResult>();

                l.Add(new FileResult { Path = "style.css", Content = String.Join(ControlChars.CrLf, GetTemplate("Css")) });

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

                    foreach (var c in File.Types)
                    {
                        if (c.OnPrimitive)
                        {
                            var Lines = GetTemplate("Primitive").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "基元").Substitute("GenericParameters", GetGenericParameters(c.Primitive.GenericParameters)).Substitute("Description", GetEscaped(c.Primitive.Description));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "基元").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnAlias)
                        {
                            var Lines = GetTemplate("Alias").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "别名").Substitute("GenericParameters", GetGenericParameters(c.Alias.GenericParameters)).Substitute("TypeSpec", GetTypeString(c.Alias.Type, true)).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "别名").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnRecord)
                        {
                            var Fields = GetVariables(c.Record.Fields);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "记录").Substitute("GenericParameters", GetGenericParameters(c.Record.GenericParameters)).Substitute("Fields", Fields).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "记录").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnTaggedUnion)
                        {
                            var Alternatives = GetVariables(c.TaggedUnion.Alternatives);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "标签联合").Substitute("GenericParameters", GetGenericParameters(c.TaggedUnion.GenericParameters)).Substitute("Fields", Alternatives).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "标签联合").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnEnum)
                        {
                            var Literals = c.Enum.Literals.SelectMany(f => GetTemplate("Literal").Substitute("Name", GetEscaped(f.Name)).Substitute("Value", GetEscaped(f.Value.ToInvariantString())).Substitute("Description", GetEscaped(f.Description))).ToList();
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "枚举").Substitute("GenericParameters", GetGenericParameters(new List<VariableDef> { })).Substitute("Fields", Literals).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "枚举").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnClientCommand)
                        {
                            var OutParameters = GetVariables(c.ClientCommand.OutParameters);
                            var InParameters = GetVariables(c.ClientCommand.InParameters);
                            var Lines = GetTemplate("ClientCommand").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "客户端方法").Substitute("OutParameters", OutParameters).Substitute("InParameters", InParameters).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageCommandContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "客户端方法").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarCommandContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else if (c.OnServerCommand)
                        {
                            var OutParameters = GetVariables(c.ServerCommand.OutParameters);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "服务端事件").Substitute("GenericParameters", GetGenericParameters(new List<VariableDef> { })).Substitute("Fields", OutParameters).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageCommandContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), false)).Substitute("MetaType", "服务端事件").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarCommandContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.Name(), Version = c.Version() }), true, true)));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }

                    var Page = GetPage(File.FriendlyPath, Content, true);
                    l.Add(new FileResult() { Path = File.DocFilePath, Content = String.Join("\r\n", Page) });

                    if (IndexPageCommandContentByFile.Count != 0)
                    {
                        IndexPageCommandContent.AddRange(GetTemplate("Brief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexPageCommandContentByFile));
                    }
                    if (IndexPageTypeContentByFile.Count != 0)
                    {
                        IndexPageTypeContent.AddRange(GetTemplate("Brief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexPageTypeContentByFile));
                    }
                    if (IndexBarCommandContentByFile.Count != 0)
                    {
                        IndexBarCommandContent.AddRange(GetTemplate("BarBrief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexBarCommandContentByFile));
                    }
                    if (IndexBarTypeContentByFile.Count != 0)
                    {
                        IndexBarTypeContent.AddRange(GetTemplate("BarBrief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexBarTypeContentByFile));
                    }
                }

                if (IndexPageCommandContent.Count > 0)
                {
                    l.Add(new FileResult { Path = "main.html", Content = String.Join(ControlChars.CrLf, GetPage("所有命令", IndexPageCommandContent, false)) });
                    l.Add(new FileResult { Path = "bar.html", Content = String.Join(ControlChars.CrLf, GetBarPage("导航栏", IndexBarCommandContent)) });
                }
                else
                {
                    l.Add(new FileResult { Path = "main.html", Content = String.Join(ControlChars.CrLf, GetPage("所有类型", IndexPageTypeContent, false)) });
                    l.Add(new FileResult { Path = "bar.html", Content = String.Join(ControlChars.CrLf, GetBarPage("导航栏", IndexBarTypeContent)) });
                }
                l.Add(new FileResult { Path = "index.html", Content = String.Join(ControlChars.CrLf, GetIndexPage("首页")) });

                return l;
            }

            public List<String> GetPage(String Name, List<String> Content, Boolean UseBackToMain)
            {
                var Page = GetTemplate("PageContent").Substitute("Name", GetEscaped(Name)).Substitute("Title", GetEscaped(Title)).Substitute("CopyrightText", GetEscaped(CopyrightText)).Substitute("Content", Content);
                if (UseBackToMain)
                {
                    return WrapPage(Page.Substitute("BackToMain", GetTemplate("BackToMain")));
                }
                else
                {
                    return WrapPage(Page.Substitute("BackToMain", new List<String> { }));
                }
            }
            public List<String> GetBarPage(String Name, List<String> Content)
            {
                var Page = GetTemplate("BarPageContent").Substitute("Name", GetEscaped(Name)).Substitute("Title", GetEscaped(Title)).Substitute("Content", Content).Substitute("BackToMain", GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTemplate("BarBackToMain")));
                return WrapPage(Page);
            }
            public List<String> GetIndexPage(String Name)
            {
                var Page = GetTemplate("IndexPageContent").Substitute("Name", GetEscaped(Name)).Substitute("Title", GetEscaped(Title));
                return WrapPage(Page);
            }
            public List<String> WrapPage(List<String> Content)
            {
                return GetTemplate("PageWrapper").Substitute("Content", Content);
            }

            public List<String> GetVariables(List<VariableDef> Fields)
            {
                if (Fields.Count == 0)
                {
                    return GetTemplate("EmptyVariable");
                }
                return Fields.SelectMany(f => GetTemplate("Variable").Substitute("Name", GetEscaped(f.Name)).Substitute("TypeSpec", GetTypeString(f.Type, true)).Substitute("Description", GetEscaped(f.Description))).ToList();
            }
            public List<String> GetGenericParameters(List<VariableDef> GenericParameters)
            {
                if (GenericParameters.Count == 0)
                {
                    return new List<String> { };
                }
                return GetTemplate("GenericParameters").Substitute("GenericParameters", GenericParameters.SelectMany(f => GetTemplate("Variable").Substitute("Name", GetEscaped("'" + f.Name)).Substitute("TypeSpec", GetTypeString(f.Type, true)).Substitute("Description", GetEscaped(f.Description))).ToList());
            }
            public List<String> GetLiterals(List<LiteralDef> Literals)
            {
                if (Literals.Count == 0)
                {
                    return GetTemplate("EmptyField");
                }
                return Literals.SelectMany(f => GetTemplate("Literal").Substitute("Name", GetEscaped(f.Name)).Substitute("Value", GetEscaped(f.Value.ToInvariantString())).Substitute("Description", GetEscaped(f.Description))).ToList();
            }

            public String GetTypeString(TypeSpec Type, Boolean WithDescription, Boolean IsInBar = false)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        {
                            var Name = Type.TypeRef.VersionedName();
                            if (TypeInfoDict.ContainsKey(Name))
                            {
                                var tl = TypeInfoDict[Name];
                                var Ref = tl.DocPath;
                                if (IsInBar)
                                {
                                    if (WithDescription)
                                    {
                                        return GetTemplate("BarRefWithDescription").Substitute("Name", GetEscaped(Name)).Substitute("Ref", GetEscaped(Ref)).Substitute("Description", tl.Def.Description()).Single();
                                    }
                                    else
                                    {
                                        return GetTemplate("BarRef").Substitute("Name", GetEscaped(Name)).Substitute("Ref", GetEscaped(Ref)).Single();
                                    }
                                }
                                else
                                {
                                    if (WithDescription)
                                    {
                                        return GetTemplate("RefWithDescription").Substitute("Name", GetEscaped(Name)).Substitute("Ref", GetEscaped(Ref)).Substitute("Description", tl.Def.Description()).Single();
                                    }
                                    else
                                    {
                                        return GetTemplate("Ref").Substitute("Name", GetEscaped(Name)).Substitute("Ref", GetEscaped(Ref)).Single();
                                    }
                                }
                            }
                            else
                            {
                                return GetEscaped(Name);
                            }
                        }
                    case TypeSpecTag.GenericParameterRef:
                        {
                            return GetEscaped("'" + Type.GenericParameterRef);
                        }
                    case TypeSpecTag.Tuple:
                        {
                            return GetEscaped("Tuple<") + String.Join(GetEscaped(", "), Type.Tuple.Select(t => GetTypeString(t, WithDescription, IsInBar))) + GetEscaped(">");
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            return GetTypeString(Type.GenericTypeSpec.TypeSpec, WithDescription, IsInBar) + GetEscaped("<") + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(gpv => GetTypeString(gpv, WithDescription, IsInBar))) + GetEscaped(">");
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n').ToList();
            }
            public static String GetEscaped(String v)
            {
                return v.Replace(@"&", @"&amp;").Replace(@"""", @"&quot;").Replace(@"'", @"&#39;").Replace(@"<", @"&lt;").Replace(@">", @"&gt;").Replace("\r", "&#13;").Replace("\n", "&#10;");
            }
        }

        public static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
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
        public static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
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
}
