//==========================================================================
//
//  File:        RelationSchemaLoader.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构加载器
//  Version:     2012.11.24.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using Semantics = Firefly.Texting.TreeFormat.Semantics;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema
{
    public sealed class RelationSchemaLoader
    {
        private List<Semantics.Node> Types;
        private List<Semantics.Node> TypeRefs;
        private List<Semantics.Node> Imports;
        private List<Semantics.Node> TypePaths;
        private Dictionary<Object, Syntax.FileTextRange> Positions;
        private HashSet<String> Parsed;
        private TreeFormatParseSetting tfpo = null;
        private TreeFormatEvaluateSetting tfeo = null;
        private XmlSerializer xs = new XmlSerializer();

        public RelationSchemaLoader()
        {
            Types = new List<Semantics.Node>();
            TypeRefs = new List<Semantics.Node>();
            Imports = new List<Semantics.Node>();
            TypePaths = new List<Semantics.Node>();
            Positions = new Dictionary<Object, Syntax.FileTextRange>();
            Parsed = new HashSet<String>();
        }

        public RelationSchemaLoader(TreeFormatParseSetting OuterParsingSetting, TreeFormatEvaluateSetting OuterEvaluateSetting)
        {
            Types = new List<Semantics.Node>();
            TypeRefs = new List<Semantics.Node>();
            Imports = new List<Semantics.Node>();
            TypePaths = new List<Semantics.Node>();
            Positions = new Dictionary<Object, Syntax.FileTextRange>();
            Parsed = new HashSet<String>();
            this.tfpo = OuterParsingSetting;
            this.tfeo = OuterEvaluateSetting;
        }

        public Schema GetResult()
        {
            var TypesNode = MakeStemNode("Types", Types.Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var TypeRefsNode = MakeStemNode("TypeRefs", TypeRefs.Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var ImportsNode = MakeStemNode("Imports", Imports.ToArray());
            var TypePathsNode = MakeStemNode("TypePaths", TypePaths.ToArray());
            var Schema = MakeStemNode("Schema", TypesNode, TypeRefsNode, ImportsNode, TypePathsNode);
            var tfr = new TreeFormatResult { Value = new Semantics.Forest { Nodes = new Semantics.Node[] { Schema } }, Positions = Positions };

            var x = XmlInterop.TreeToXml(tfr);
            var os = xs.Read<OS.Schema>(x);

            OS.ObjectSchemaExtensions.VerifyDuplicatedNames(os);
            var Map = OS.ObjectSchemaExtensions.GetMap(os).ToDictionary(t => t.Key, t => t.Value);
            var TypePathDict = os.TypePaths.ToDictionary(tp => tp.Name);

            foreach (var t in os.TypeRefs.Concat(os.Types))
            {
                if (Parsed.Contains(OS.ObjectSchemaExtensions.VersionedName(t)))
                {
                    continue;
                }
                switch (t._Tag)
                {
                    case OS.TypeDefTag.Primitive:
                        foreach (var v in t.Primitive.GenericParameters)
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Primitive.Name, Map, TypePathDict);
                        }
                        break;
                    case OS.TypeDefTag.Record:
                        foreach (var v in t.Record.GenericParameters.Concat(t.Record.Fields))
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Record.Name, Map, TypePathDict);
                        }
                        break;
                    case OS.TypeDefTag.Enum:
                        t.Enum.UnderlyingType = ParseTypeSpec(t.Enum.UnderlyingType.TypeRef.Name, t.Enum.Name, Map, TypePathDict);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            var s = RelationSchemaTranslator.Translate(os);
            return s;
        }
        private Semantics.Node MakeLeafNode(String Value)
        {
            var n = new Semantics.Node { _Tag = Semantics.NodeTag.Leaf, Leaf = Value };
            return n;
        }
        private Semantics.Node MakeStemNode(String Name, params Semantics.Node[] Children)
        {
            var s = new Semantics.Stem { Name = Name, Children = Children };
            var n = new Semantics.Node { _Tag = Semantics.NodeTag.Stem, Stem = s };
            return n;
        }

        public void AddImport(String Import)
        {
            Imports.Add(MakeStemNode("String", MakeLeafNode(Import)));
        }

        public void LoadType(String TreePath)
        {
            Load(TreePath, Types);
        }
        public void LoadType(String TreePath, StreamReader Reader)
        {
            Load(TreePath, Reader, Types);
        }
        public void LoadTypeRef(String TreePath)
        {
            Load(TreePath, TypeRefs);
        }
        public void LoadTypeRef(String TreePath, StreamReader Reader)
        {
            Load(TreePath, Reader, TypeRefs);
        }
        private void Load(String TreePath, List<Semantics.Node> Types)
        {
            using (var Reader = Txt.CreateTextReader(TreePath))
            {
                if (Debugger.IsAttached)
                {
                    Load(TreePath, Reader, Types);
                }
                else
                {
                    try
                    {
                        Load(TreePath, Reader, Types);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new Syntax.TextLine[] { } }, Range = Opt<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void Load(String TreePath, StreamReader Reader, List<Semantics.Node> Types)
        {
            var Functions = new HashSet<String>() { "Primitive", "Entity", "Enum", "Query" };
            var TableParameterFunctions = Functions;
            var TableContentFunctions = Functions;
            TreeFormatParseSetting ps;
            if (tfpo == null)
            {
                ps = new TreeFormatParseSetting()
                {
                    IsTableParameterFunction = Name => TableParameterFunctions.Contains(Name),
                    IsTableContentFunction = Name => TableContentFunctions.Contains(Name)
                };
            }
            else
            {
                ps = new TreeFormatParseSetting()
                {
                    IsTableParameterFunction = Name =>
                    {
                        if (TableParameterFunctions.Contains(Name)) { return true; }
                        return tfpo.IsTableParameterFunction(Name);
                    },
                    IsTableContentFunction = Name =>
                    {
                        if (TableContentFunctions.Contains(Name)) { return true; }
                        return tfpo.IsTableContentFunction(Name);
                    },
                    IsTreeParameterFunction = tfpo.IsTreeParameterFunction,
                    IsTreeContentFunction = tfpo.IsTreeContentFunction
                };
            }
            var es = new TreeFormatEvaluateSetting()
            {
                FunctionCallEvaluator = (f, nm) =>
                {
                    if (f.Parameters.Length < 1 || f.Parameters.Length > 2) { throw new Syntax.InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                    var VersionedName = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");
                    var Name = VersionedName;
                    var Version = GetVersion(ref Name);

                    String Description = "";
                    if (f.Parameters.Length >= 2)
                    {
                        var DescriptionParameter = f.Parameters[1];
                        if (DescriptionParameter._Tag != Semantics.NodeTag.Leaf) { throw new Syntax.InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                        Description = DescriptionParameter.Leaf;
                    }

                    var ContentLines = new Syntax.FunctionCallTableLine[] { };
                    if (Functions.Contains(f.Name.Text) && f.Content.HasValue)
                    {
                        var ContentValue = f.Content.Value;
                        if (ContentValue._Tag != Syntax.FunctionCallContentTag.TableContent) { throw new Syntax.InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                        ContentLines = ContentValue.TableContent;
                    }

                    switch (f.Name.Text)
                    {
                        case "Primitive":
                            {
                                if (Version != "") { throw new Syntax.InvalidEvaluationException("InvalidName", nm.GetFileRange(f.Parameters[0]), f.Parameters[0]); }

                                var GenericParameters = new List<Semantics.Node>();

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Semantics.Node cType = null;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 2)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                        cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                        cDescription = "";
                                    }
                                    else if (Line.Nodes.Length == 3)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                        cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                        cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                    }
                                    else if (Line.Nodes.Length == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new Syntax.InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }

                                    if (cName.StartsWith("'"))
                                    {
                                        cName = new String(cName.Skip(1).ToArray());
                                        GenericParameters.Add(MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        ));
                                    }
                                    else
                                    {
                                        throw new Syntax.InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                    }
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("Primitive",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("GenericParameters", GenericParameters.ToArray()),
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
                            }
                        case "Entity":
                            {
                                var GenericParameters = new List<Semantics.Node>();
                                var Fields = new List<Semantics.Node>();

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Semantics.Node cType = null;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 2)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                        cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                        cDescription = "";
                                    }
                                    else if (Line.Nodes.Length == 3)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                        cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                        cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                    }
                                    else if (Line.Nodes.Length == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new Syntax.InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }

                                    if (cName.StartsWith("'"))
                                    {
                                        cName = new String(cName.Skip(1).ToArray());
                                        GenericParameters.Add(MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        ));
                                    }
                                    else
                                    {
                                        Fields.Add(MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        ));
                                    }
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("Record",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("GenericParameters", GenericParameters.ToArray()),
                                        MakeStemNode("Fields", Fields.ToArray()),
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
                            }
                        case "Enum":
                            {
                                var Literals = new List<Semantics.Node>();

                                Int64 NextValue = 0;
                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Int64 cValue = NextValue;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 1)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                        cValue = NextValue;
                                        cDescription = "";
                                    }
                                    else if (Line.Nodes.Length == 2)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                        cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                        cDescription = "";
                                    }
                                    else if (Line.Nodes.Length == 3)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                        cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                        cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                    }
                                    else if (Line.Nodes.Length == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new Syntax.InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }
                                    NextValue = cValue + 1;

                                    Literals.Add(MakeStemNode("Literal",
                                        MakeStemNode("Name", MakeLeafNode(cName)),
                                        MakeStemNode("Value", MakeLeafNode(cValue.ToInvariantString())),
                                        MakeStemNode("Description", MakeLeafNode(cDescription))
                                    ));
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("Enum",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("UnderlyingType", BuildVirtualTypeSpec("Int")),
                                        MakeStemNode("Literals", Literals.ToArray()),
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
                            }
                        case "Query":
                            {
                                //TODO
                                return new Semantics.Node[] { };
                            }
                        default:
                            {
                                if (tfeo != null)
                                {
                                    return tfeo.FunctionCallEvaluator(f, nm);
                                }
                                throw new Syntax.InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                            }
                    }
                },
                TokenParameterEvaluator = tfeo != null ? tfeo.TokenParameterEvaluator : null
            };

            var t = TreeFile.ReadDirect(Reader, TreePath, ps, es);
            Types.AddRange(t.Value.Nodes);
            foreach (var p in t.Positions)
            {
                Positions.Add(p.Key, p.Value);
            }
            foreach (var n in t.Value.Nodes)
            {
                var Type = n.Stem.Name;
                var Name = n.Stem.Children.Single(c => c.Stem.Name == "Name").Stem.Children.Single().Leaf;
                var Version = "";
                if (n.Stem.Children.Where(c => c.Stem.Name == "Version").Any())
                {
                    Version = n.Stem.Children.Single(c => c.Stem.Name == "Version").Stem.Children.Single().Leaf;
                }
                var VersionedName = Name;
                if (Version != "")
                {
                    VersionedName = Name + "[" + Version + "]";
                }
                TypePaths.Add
                (
                    MakeStemNode("TypePath",
                        MakeStemNode("Name", MakeLeafNode(VersionedName)),
                        MakeStemNode("Path", MakeLeafNode(TreePath))
                    )
                );
            }
        }

        private String GetLeafNodeValue(Semantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (n._Tag != Semantics.NodeTag.Leaf) { throw new Syntax.InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }

        private Semantics.Node VirtualParseTypeSpec(Semantics.Node TypeNode, ISemanticsNodeMaker nm)
        {
            var TypeSpec = GetLeafNodeValue(TypeNode, nm, "InvalidTypeSpec");
            return BuildVirtualTypeSpec(TypeSpec);
        }
        private Semantics.Node BuildVirtualTypeSpec(String TypeSpec)
        {
            return MakeStemNode("TypeRef", MakeStemNode("Name", MakeLeafNode(TypeSpec)), MakeStemNode("Version", MakeLeafNode("")));
        }

        private static String GetVersion(ref String Name)
        {
            return OS.ObjectSchemaLoaderFunctions.GetVersion(ref Name);
        }
        private static OS.TypeSpec ParseTypeSpec(String TypeString, String TypeDefName, Dictionary<String, OS.TypeDef> TypeMap, Dictionary<String, OS.TypePath> TypePaths)
        {
            return OS.ObjectSchemaLoaderFunctions.ParseTypeSpec(TypeString, TypeDefName, TypeMap, TypePaths);
        }
    }
}
