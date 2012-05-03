//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构XHTML代码生成器
//  Version:     2012.04.24.
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
    public static class CodeGenerator
    {
        public class FileResult
        {
            public String Path;
            public String Content;
        }

        public static FileResult[] CompileToXhtml(this Schema Schema, String Title, String CopyrightText)
        {
            Writer w = new Writer(Schema, Title, CopyrightText);
            var Files = w.GetFiles();
            return Files;
        }

        private class Writer
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
            }

            public class TypeLocationInfo
            {
                public String Name;
                public String FriendlyPath;
                public String DocFilePath;
                public String DocPath;
            }
            private Dictionary<String, TypeLocationInfo> TypeLocations;

            public FileResult[] GetFiles()
            {
                TypeLocations = new Dictionary<String, TypeLocationInfo>(StringComparer.OrdinalIgnoreCase);

                String Root = "";
                if (Schema.TypePaths.Length > 0)
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
                }

                foreach (var p in Schema.TypePaths)
                {
                    var Path = FileNameHandling.GetRelativePath(p.Path, Root);
                    var PathWithoutExt = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Path), FileNameHandling.GetMainFileName(Path));
                    var DocFilePath = PathWithoutExt.Replace(@"\", @"_").Replace(@"/", @"_").Replace(@".", "_").Replace(@":", @"_").Replace(@"#", @"_") + @".html";
                    var tli = new TypeLocationInfo { Name = p.Name, FriendlyPath = PathWithoutExt.Replace(@"\", @"/"), DocFilePath = DocFilePath, DocPath = String.Format("{0}#{1}", DocFilePath, p.Name) };
                    TypeLocations.Add(p.Name, tli);
                }

                var Types = Schema.GetMap();
                var Files = Types.GroupBy(Type => CollectionOperations.CreatePair(TypeLocations[Type.Key].FriendlyPath, TypeLocations[Type.Key].DocFilePath), (Pair, gt) => new { FriendlyPath = Pair.Key, DocFilePath = Pair.Value, Types = gt.Select(t => t.Value).ToArray() }).ToArray();

                List<FileResult> l = new List<FileResult>();

                l.Add(new FileResult { Path = "style.css", Content = String.Join(ControlChars.CrLf, GetTemplate("Css")) });

                var IndexPageContent = new List<String>();

                foreach (var File in Files)
                {
                    var Content = new List<String>();

                    var Commands = new List<String>();

                    foreach (var c in File.Types)
                    {
                        if (c.OnPrimitive)
                        {
                            var Lines = GetTemplate("Primitive").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "基元").Substitute("GenericParameters", GetGenericParameters(c.Primitive.GenericParameters)).Substitute("Description", GetEscaped(c.Primitive.Description));
                            Content.AddRange(Lines);
                        }
                        else if (c.OnAlias)
                        {
                            var Lines = GetTemplate("Alias").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "别名").Substitute("GenericParameters", GetGenericParameters(c.Alias.GenericParameters)).Substitute("TypeSpec", GetTypeString(c.Alias.Type)).Substitute("Description", GetEscaped(c.Alias.Description));
                            Content.AddRange(Lines);
                        }
                        else if (c.OnRecord)
                        {
                            var Fields = GetVariables(c.Record.Fields);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "记录").Substitute("GenericParameters", GetGenericParameters(c.Record.GenericParameters)).Substitute("Fields", Fields).Substitute("Description", GetEscaped(c.Record.Description));
                            Content.AddRange(Lines);
                        }
                        else if (c.OnTaggedUnion)
                        {
                            var Alternatives = GetVariables(c.TaggedUnion.Alternatives);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "标签联合").Substitute("GenericParameters", GetGenericParameters(c.TaggedUnion.GenericParameters)).Substitute("Fields", Alternatives).Substitute("Description", GetEscaped(c.TaggedUnion.Description));
                            Content.AddRange(Lines);
                        }
                        else if (c.OnEnum)
                        {
                            var Literals = c.Enum.Literals.SelectMany(f => GetTemplate("Literal").Substitute("Name", GetEscaped(f.Name)).Substitute("Value", GetEscaped(f.Value.ToInvariantString())).Substitute("Description", GetEscaped(f.Description))).ToArray();
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "枚举").Substitute("GenericParameters", GetGenericParameters(new VariableDef[] { })).Substitute("Fields", Literals).Substitute("Description", GetEscaped(c.Enum.Description));
                            Content.AddRange(Lines);
                        }
                        else if (c.OnClientCommand)
                        {
                            var OutParameters = GetVariables(c.ClientCommand.OutParameters);
                            var InParameters = GetVariables(c.ClientCommand.InParameters);
                            var Lines = GetTemplate("ClientCommand").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "客户端方法").Substitute("OutParameters", OutParameters).Substitute("InParameters", InParameters).Substitute("Description", GetEscaped(c.ClientCommand.Description));
                            Content.AddRange(Lines);

                            Commands.AddRange(GetTemplate("TypeBrief").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("GenericParameters", GetGenericParameters(new VariableDef[] { })).Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.ClientCommand.Name, Version = c.Version() }))).Substitute("MetaType", "客户端方法").Substitute("Description", GetEscaped(c.ClientCommand.Description)));
                        }
                        else if (c.OnServerCommand)
                        {
                            var OutParameters = GetVariables(c.ServerCommand.OutParameters);
                            var Lines = GetTemplate("Type").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("MetaType", "服务端事件").Substitute("GenericParameters", GetGenericParameters(new VariableDef[] { })).Substitute("Fields", OutParameters).Substitute("Description", GetEscaped(c.ServerCommand.Description));
                            Content.AddRange(Lines);

                            Commands.AddRange(GetTemplate("TypeBrief").Substitute("Name", GetEscaped(c.VersionedName())).Substitute("GenericParameters", GetGenericParameters(new VariableDef[] { })).Substitute("TypeSpec", GetTypeString(TypeSpec.CreateTypeRef(new TypeRef { Name = c.ServerCommand.Name, Version = c.Version() }))).Substitute("MetaType", "服务端事件").Substitute("Description", GetEscaped(c.ServerCommand.Description)));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }

                    var Page = GetPage(File.FriendlyPath, Content.ToArray(), true);
                    l.Add(new FileResult() { Path = File.DocFilePath, Content = String.Join("\r\n", Page) });

                    if (Commands.Count != 0)
                    {
                        IndexPageContent.AddRange(GetTemplate("Brief").Substitute("FilePath", GetEscaped(File.FriendlyPath)).Substitute("Commands", Commands.ToArray()));
                    }
                }

                l.Add(new FileResult { Path = "index.html", Content = String.Join(ControlChars.CrLf, GetPage("首页", IndexPageContent.ToArray(), false)) });

                return l.ToArray();
            }

            public String[] GetPage(String Name, String[] Content, Boolean UseBackToMain)
            {
                var Page = GetTemplate("Page").Substitute("Name", GetEscaped(Name)).Substitute("Title", GetEscaped(Title)).Substitute("CopyrightText", GetEscaped(CopyrightText)).Substitute("Content", Content);
                if (UseBackToMain)
                {
                    return Page.Substitute("BackToMain", GetTemplate("BackToMain"));
                }
                else
                {
                    return Page.Substitute("BackToMain", new String[] { });
                }
            }

            public String[] GetVariables(VariableDef[] Fields)
            {
                if (Fields.Length == 0)
                {
                    return GetTemplate("EmptyVariable");
                }
                return Fields.SelectMany(f => GetTemplate("Variable").Substitute("Name", GetEscaped(f.Name)).Substitute("TypeSpec", GetTypeString(f.Type)).Substitute("Description", GetEscaped(f.Description))).ToArray();
            }
            public String[] GetGenericParameters(VariableDef[] GenericParameters)
            {
                if (GenericParameters.Length == 0)
                {
                    return new String[] { };
                }
                return GetTemplate("GenericParameters").Substitute("GenericParameters", GenericParameters.SelectMany(f => GetTemplate("Variable").Substitute("Name", GetEscaped("'" + f.Name)).Substitute("TypeSpec", GetTypeString(f.Type)).Substitute("Description", GetEscaped(f.Description))).ToArray());
            }
            public String[] GetLiterals(LiteralDef[] Literals)
            {
                if (Literals.Length == 0)
                {
                    return GetTemplate("EmptyField");
                }
                return Literals.SelectMany(f => GetTemplate("Literal").Substitute("Name", GetEscaped(f.Name)).Substitute("Value", GetEscaped(f.Value.ToInvariantString())).Substitute("Description", GetEscaped(f.Description))).ToArray();
            }

            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        {
                            var Name = Type.TypeRef.VersionedName();
                            var tl = TypeLocations[Name];
                            var Ref = tl.DocPath;
                            return GetTemplate("Ref").Substitute("Name", GetEscaped(Name)).Substitute("Ref", GetEscaped(Ref)).Single();
                        }
                    case TypeSpecTag.GenericParameterRef:
                        {
                            return GetEscaped("'" + Type.GenericParameterRef.Value);
                        }
                    case TypeSpecTag.Tuple:
                        {
                            return GetEscaped("Tuple<") + String.Join(GetEscaped(", "), Type.Tuple.Types.Select(t => GetTypeString(t)).ToArray()) + GetEscaped(">");
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            return GetTypeString(Type.GenericTypeSpec.TypeSpec) + GetEscaped("<") + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(gpv => GetTypeString(gpv)).ToArray()) + GetEscaped(">");
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
            public String GetTypeString(GenericParameterValue gpv)
            {
                if (gpv.OnLiteral)
                {
                    return GetEscaped(gpv.Literal);
                }
                else if (gpv.OnTypeSpec)
                {
                    return GetTypeString(gpv.TypeSpec);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public String[] GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n');
            }
            public String GetEscaped(String v)
            {
                return v.Replace(@"&", @"&amp;").Replace(@"""", @"&quot;").Replace(@"'", @"&#39;").Replace(@"<", @"&lt;").Replace(@">", @"&gt;").Replace("\r", "&#13;").Replace("\n", "&#10;");
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";

            List<String> l = new List<String>();
            foreach (var Line in Lines)
            {
                var NewLine = Line;

                if (Line.Contains(ParameterString))
                {
                    NewLine = NewLine.Replace(ParameterString, Value);
                }

                l.Add(NewLine);
            }
            return l.ToArray();
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            List<String> l = new List<String>();
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
            return l.ToArray();
        }
    }
}
