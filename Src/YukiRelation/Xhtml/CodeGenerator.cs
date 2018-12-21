//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构XHTML代码生成器
//  Version:     2018.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.TextEncoding;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.Xhtml
{
    public static class CodeGenerator
    {
        public class FileResult
        {
            public String Path;
            public String Content;
        }

        public static List<FileResult> CompileToXhtml(this RelationSchemaLoaderResult rslr, String Title, String CopyrightText)
        {
            var w = new Writer(rslr, Title, CopyrightText);
            var Files = w.GetFiles();
            return Files;
        }

        private class Writer
        {
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String Title;
            private String CopyrightText;

            static Writer()
            {
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Xhtml);
            }

            public Writer(RelationSchemaLoaderResult rslr, String Title, String CopyrightText)
            {
                this.Schema = rslr.Schema;
                this.Title = Title;
                this.CopyrightText = CopyrightText;

                TypeInfoDict = new Dictionary<String, TypeInfo>(StringComparer.OrdinalIgnoreCase);

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
                var Files = Types.Where(Type => TypeInfoDict.ContainsKey(Type.Key)).GroupBy(Type => CollectionOperations.CreatePair(TypeInfoDict[Type.Key].FriendlyPath, TypeInfoDict[Type.Key].DocFilePath), (Pair, gt) => new { FriendlyPath = Pair.Key, DocFilePath = Pair.Value, Types = gt.Select(t => t.Value).ToArray() }).ToList();

                List<FileResult> l = new List<FileResult>();

                l.Add(new FileResult { Path = "style.css", Content = String.Join(ControlChars.CrLf, GetTemplate("Css")) });

                var IndexPageTypeContent = new List<String>();
                var IndexBarTypeContent = new List<String>();

                foreach (var File in Files)
                {
                    var Content = new List<String>();

                    var IndexPageTypeContentByFile = new List<String>();
                    var IndexBarTypeContentByFile = new List<String>();

                    foreach (var c in File.Types)
                    {
                        if (c.OnPrimitive)
                        {
                            var Lines = GetTemplate("Primitive").Substitute("Name", GetEscaped(c.Name())).Substitute("MetaType", "基元").Substitute("GenericParameters", new List<String> { }).Substitute("Description", GetEscaped(c.Primitive.Description));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), false)).Substitute("MetaType", "基元").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), true, true)));
                        }
                        else if (c.OnEntity)
                        {
                            var Fields = GetVariables(c.Entity.Fields);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.Name())).Substitute("MetaType", "实体").Substitute("GenericParameters", new List<String> { }).Substitute("Fields", Fields).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), false)).Substitute("MetaType", "记录").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), true, true)));
                        }
                        else if (c.OnEnum)
                        {
                            var Literals = c.Enum.Literals.SelectMany(f => GetTemplate("Literal").Substitute("Name", GetEscaped(f.Name)).Substitute("Value", GetEscaped(f.Value.ToInvariantString())).Substitute("Description", GetEscaped(f.Description))).ToList();
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.Name())).Substitute("MetaType", "枚举").Substitute("GenericParameters", GetGenericParameters(new List<VariableDef> { })).Substitute("Fields", Literals).Substitute("Description", GetEscaped(c.Description()));
                            Content.AddRange(Lines);

                            IndexPageTypeContentByFile.AddRange(GetTemplate("TypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), false)).Substitute("MetaType", "枚举").Substitute("Description", GetEscaped(c.Description())));
                            IndexBarTypeContentByFile.AddRange(GetTemplate("BarTypeBrief").Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Value = c.Name() }), true, true)));
                        }
                        else if (c.OnQueryList)
                        {
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }

                    var Page = GetPage(File.FriendlyPath, Content, true);
                    l.Add(new FileResult() { Path = File.DocFilePath, Content = String.Join("\r\n", Page) });

                    if (IndexPageTypeContentByFile.Count != 0)
                    {
                        IndexPageTypeContent.AddRange(GetTemplate("Brief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexPageTypeContentByFile));
                    }
                    if (IndexBarTypeContentByFile.Count != 0)
                    {
                        IndexBarTypeContent.AddRange(GetTemplate("BarBrief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Types", IndexBarTypeContentByFile));
                    }
                }

                l.Add(new FileResult { Path = "main.html", Content = String.Join(ControlChars.CrLf, GetPage("所有类型", IndexPageTypeContent, false)) });
                l.Add(new FileResult { Path = "bar.html", Content = String.Join(ControlChars.CrLf, GetBarPage("导航栏", IndexBarTypeContent)) });
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

            public List<String> GetVariable(VariableDef v)
            {
                String TypeSpecString;
                if (v.Attribute.OnColumn)
                {
                    TypeSpecString = GetTypeString(v.Type, true);
                }
                else if (v.Attribute.OnNavigation)
                {
                    TypeSpecString = GetEscaped("[导航] ") + GetTypeString(v.Type, true);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var l = GetTemplate("Variable").Substitute("Name", GetEscaped(v.Name)).Substitute("TypeSpec", TypeSpecString).Substitute("Description", GetEscaped(v.Description));
                return l;
            }
            public List<String> GetVariables(IEnumerable<VariableDef> Variables)
            {
                if (!Variables.Any())
                {
                    return GetTemplate("EmptyVariable");
                }
                return Variables.SelectMany(v => GetVariable(v)).ToList();
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
                if (Type.OnTypeRef)
                {
                    var Name = Type.TypeRef.Value;
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
