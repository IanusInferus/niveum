//==========================================================================
//
//  File:        ObjectSchemaLoader.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构加载器
//  Version:     2012.11.11.
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

namespace Yuki.ObjectSchema
{
    public sealed class ObjectSchemaLoader
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

        public ObjectSchemaLoader()
        {
            Types = new List<Semantics.Node>();
            TypeRefs = new List<Semantics.Node>();
            Imports = new List<Semantics.Node>();
            TypePaths = new List<Semantics.Node>();
            Positions = new Dictionary<Object, Syntax.FileTextRange>();
            Parsed = new HashSet<String>();
        }

        public ObjectSchemaLoader(TreeFormatParseSetting OuterParsingSetting, TreeFormatEvaluateSetting OuterEvaluateSetting)
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
            var s = xs.Read<Schema>(x);

            s.VerifyDuplicatedNames();
            var Map = s.GetMap().ToDictionary(t => t.Key, t => t.Value);
            var TypePathDict = s.TypePaths.ToDictionary(tp => tp.Name);

            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (Parsed.Contains(t.VersionedName()))
                {
                    continue;
                }
                switch (t._Tag)
                {
                    case TypeDefTag.Primitive:
                        foreach (var v in t.Primitive.GenericParameters)
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Primitive.Name, Map, TypePathDict);
                        }
                        break;
                    case TypeDefTag.Alias:
                        foreach (var v in t.Alias.GenericParameters)
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Alias.Name, Map, TypePathDict);
                        }
                        t.Alias.Type = ParseTypeSpec(t.Alias.Type.TypeRef.Name, t.Alias.Name, Map, TypePathDict);
                        break;
                    case TypeDefTag.Record:
                        foreach (var v in t.Record.GenericParameters.Concat(t.Record.Fields))
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Record.Name, Map, TypePathDict);
                        }
                        break;
                    case TypeDefTag.TaggedUnion:
                        foreach (var v in t.TaggedUnion.GenericParameters.Concat(t.TaggedUnion.Alternatives))
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.TaggedUnion.Name, Map, TypePathDict);
                        }
                        break;
                    case TypeDefTag.Enum:
                        t.Enum.UnderlyingType = ParseTypeSpec(t.Enum.UnderlyingType.TypeRef.Name, t.Enum.Name, Map, TypePathDict);
                        break;
                    case TypeDefTag.ClientCommand:
                        foreach (var v in t.ClientCommand.OutParameters.Concat(t.ClientCommand.InParameters))
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.ClientCommand.Name, Map, TypePathDict);
                        }
                        break;
                    case TypeDefTag.ServerCommand:
                        foreach (var v in t.ServerCommand.OutParameters)
                        {
                            v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.ServerCommand.Name, Map, TypePathDict);
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

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

        public void LoadSchema(String TreePath)
        {
            using (var Reader = Txt.CreateTextReader(TreePath))
            {
                if (Debugger.IsAttached)
                {
                    LoadSchema(TreePath, Reader);
                }
                else
                {
                    try
                    {
                        LoadSchema(TreePath, Reader);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new Syntax.TextLine[] { } }, Range = Opt<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void LoadSchema(String TreePath, StreamReader Reader)
        {
            var t = TreeFile.ReadDirect(Reader, TreePath, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            var SchemaContent = t.Value.Nodes.Single(n => n.OnStem && n.Stem.Name == "Schema").Stem.Children;
            var TypesNodes = SchemaContent.Single(n => n.OnStem && n.Stem.Name == "Types").Stem.Children.Where(n => n.OnStem).SelectMany(n => n.Stem.Children);
            var TypeRefsNodes = SchemaContent.Single(n => n.OnStem && n.Stem.Name == "TypeRefs").Stem.Children.Where(n => n.OnStem).SelectMany(n => n.Stem.Children);
            Types.AddRange(TypesNodes);
            TypeRefs.AddRange(TypeRefsNodes);
            foreach (var n in TypesNodes.Concat(TypeRefsNodes))
            {
                if (!n.OnStem)
                {
                    throw new InvalidOperationException();
                }
                var NameNode = n.Stem.Children.Single(nc => nc.Stem.Name == "Name").Stem.Children.Single();
                if (!NameNode.OnLeaf)
                {
                    throw new InvalidOperationException();
                }
                var Name = NameNode.Leaf;
                var Version = "";
                if (n.Stem.Children.Where(nc => nc.Stem.Name == "Version").Count() > 0)
                {
                    var VersionNode = n.Stem.Children.Single(nc => nc.Stem.Name == "Version").Stem.Children.Single();
                    if (!VersionNode.OnLeaf)
                    {
                        throw new InvalidOperationException();
                    }
                    Version = VersionNode.Leaf;
                }
                var VersionedName = (new TypeRef { Name = Name, Version = Version }).VersionedName();
                Parsed.Add(VersionedName);
            }
            Imports.AddRange(SchemaContent.Single(n => n.OnStem && n.Stem.Name == "Imports").Stem.Children);
            TypePaths.AddRange(SchemaContent.Single(n => n.OnStem && n.Stem.Name == "TypePaths").Stem.Children);
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
            var Functions = new HashSet<String>() { "Primitive", "Alias", "Record", "TaggedUnion", "Enum", "ClientCommand", "ServerCommand" };
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
                        case "Alias":
                            {
                                var GenericParameters = new List<Semantics.Node>();
                                Semantics.Node Type = null;

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Semantics.Node cType = null;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 1)
                                    {
                                        if (Type != null)
                                        {
                                            throw new Syntax.InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                        }
                                        Type = MakeStemNode("Type", VirtualParseTypeSpec(Line.Nodes[0], nm));
                                        continue;
                                    }
                                    else if (Line.Nodes.Length == 2)
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

                                if (Type == null)
                                {
                                    throw new Syntax.InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentLines), ContentLines);
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("Alias",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("GenericParameters", GenericParameters.ToArray()),
                                        Type,
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
                            }
                        case "Record":
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
                        case "TaggedUnion":
                            {
                                var GenericParameters = new List<Semantics.Node>();
                                var Alternatives = new List<Semantics.Node>();

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Semantics.Node cType = null;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 2)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
                                        cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                        cDescription = "";
                                    }
                                    else if (Line.Nodes.Length == 3)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
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
                                        Alternatives.Add(MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        ));
                                    }
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("TaggedUnion",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("GenericParameters", GenericParameters.ToArray()),
                                        MakeStemNode("Alternatives", Alternatives.ToArray()),
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
                        case "ClientCommand":
                            {
                                var OutParameters = new List<Semantics.Node>();
                                var InParameters = new List<Semantics.Node>();

                                Boolean IsIntParameter = false;
                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;
                                    Semantics.Node cType = null;
                                    String cDescription = null;

                                    if (Line.Nodes.Length == 1)
                                    {
                                        if (GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName") == ">")
                                        {
                                            IsIntParameter = true;
                                            continue;
                                        }
                                        else
                                        {
                                            throw new Syntax.InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                        }
                                    }
                                    else if (Line.Nodes.Length == 2)
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

                                    if (IsIntParameter)
                                    {
                                        var p = MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        );
                                        InParameters.Add(p);
                                    }
                                    else
                                    {
                                        var p = MakeStemNode("Variable",
                                            MakeStemNode("Name", MakeLeafNode(cName)),
                                            MakeStemNode("Type", cType),
                                            MakeStemNode("Description", MakeLeafNode(cDescription))
                                        );
                                        OutParameters.Add(p);
                                    }
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("ClientCommand",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("OutParameters", OutParameters.ToArray()),
                                        MakeStemNode("InParameters", InParameters.ToArray()),
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
                            }
                        case "ServerCommand":
                            {
                                var OutParameters = new List<Semantics.Node>();

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

                                    OutParameters.Add(MakeStemNode("Variable",
                                        MakeStemNode("Name", MakeLeafNode(cName)),
                                        MakeStemNode("Type", cType),
                                        MakeStemNode("Description", MakeLeafNode(cDescription))
                                    ));
                                }

                                return new Semantics.Node[] {
                                    MakeStemNode("ServerCommand",
                                        MakeStemNode("Name", MakeLeafNode(Name)),
                                        MakeStemNode("Version", MakeLeafNode(Version)),
                                        MakeStemNode("OutParameters", OutParameters.ToArray()),
                                        MakeStemNode("Description", MakeLeafNode(Description))
                                    )
                                };
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

        private static Regex rVersion = new Regex(@"^(?<Name>.*?)\[(?<Version>.*?)\]$", RegexOptions.ExplicitCapture);
        private static String GetVersion(ref String Name)
        {
            var m = rVersion.Match(Name);
            if (m.Success)
            {
                Name = m.Result("${Name}");
                return m.Result("${Version}");
            }
            return "";
        }

        public static String GetTypeFriendlyNameFromVersionedName(String VersionedName)
        {
            var Name = VersionedName;
            var Version = GetVersion(ref Name);
            return (new TypeRef { Name = Name, Version = Version }).TypeFriendlyName();
        }

        private static Regex rErrorChars = new Regex(@"^(\s|\>)$", RegexOptions.ExplicitCapture);
        private Semantics.Node VirtualParseTypeSpec(Semantics.Node TypeNode, ISemanticsNodeMaker nm)
        {
            var TypeSpec = GetLeafNodeValue(TypeNode, nm, "InvalidTypeSpec");
            return BuildVirtualTypeSpec(TypeSpec);
        }
        private Semantics.Node BuildVirtualTypeSpec(String TypeSpec)
        {
            return MakeStemNode("TypeRef", MakeStemNode("Name", MakeLeafNode(TypeSpec)), MakeStemNode("Version", MakeLeafNode("")));
        }

        private TypeSpec ParseTypeSpec(String TypeString, String TypeDefName, Dictionary<String, TypeDef> TypeMap, Dictionary<String, TypePath> TypePaths)
        {
            var tsl = ParseTypeSpecLiteral(TypeString, c => new Syntax.InvalidTokenException(String.Format("InvalidChar: '{0}' in {1} at {2}", c, TypeDefName, TypePaths[TypeDefName].Path)));
            var TypeName = tsl.TypeName;
            var Parameters = tsl.Parameters;

            if (Parameters.Length == 0)
            {
                if (TypeName.StartsWith("'"))
                {
                    TypeName = new String(TypeName.Skip(1).ToArray());
                    return TypeSpec.CreateGenericParameterRef(TypeName);
                }
                else
                {
                    var Version = GetVersion(ref TypeName);
                    return TypeSpec.CreateTypeRef(new TypeRef { Name = TypeName, Version = Version });
                }
            }

            if (String.Equals(TypeName, "Tuple", StringComparison.OrdinalIgnoreCase))
            {
                return TypeSpec.CreateTuple(new TupleDef { Types = Parameters.Select(p => ParseTypeSpec(p, TypeDefName, TypeMap, TypePaths)).ToArray() });
            }

            var ts = ParseTypeSpec(TypeName, TypeDefName, TypeMap, TypePaths);
            if (!ts.OnTypeRef)
            {
                throw new Syntax.InvalidEvaluationException(String.Format("InvalidGenericType: {0} at {1}", TypeName, TypePaths[TypeName].Path));
            }

            var t = TypeMap[TypeName];

            VariableDef[] GenericParameters = null;

            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    GenericParameters = t.Primitive.GenericParameters;
                    break;
                case TypeDefTag.Alias:
                    GenericParameters = t.Alias.GenericParameters;
                    break;
                case TypeDefTag.Record:
                    GenericParameters = t.Record.GenericParameters;
                    break;
                case TypeDefTag.TaggedUnion:
                    GenericParameters = t.TaggedUnion.GenericParameters;
                    break;
                default:
                    throw new Syntax.InvalidEvaluationException(String.Format("InvalidGenericParameters: {0} at {1}", TypeDefName, TypePaths[TypeDefName].Path));
            }

            return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = ts, GenericParameterValues = Parameters.ZipStrict(GenericParameters, (v, p) => ParseGenericParameterValue(v, p, TypeDefName, TypeMap, TypePaths)).ToArray() });
        }
        private GenericParameterValue ParseGenericParameterValue(String GenericParameterValueString, VariableDef GenericParameter, String TypeDefName, Dictionary<String, TypeDef> TypeMap, Dictionary<String, TypePath> TypePaths)
        {
            if (!GenericParameter.Type.OnTypeRef)
            {
                return GenericParameterValue.CreateLiteral(GenericParameterValueString);
            }

            if (!String.Equals(GenericParameter.Type.TypeRef.Name, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return GenericParameterValue.CreateLiteral(GenericParameterValueString);
            }

            var TypeSpec = ParseTypeSpec(GenericParameterValueString, TypeDefName, TypeMap, TypePaths);
            return GenericParameterValue.CreateTypeSpec(TypeSpec);
        }

        private class TypeSpecLiteral
        {
            public String TypeName;
            public String[] Parameters;
        }
        private TypeSpecLiteral ParseTypeSpecLiteral(String TypeString, Func<String, Exception> InvalidCharExceptionGenerator)
        {
            var TypeLine = TypeString.Trim(' ');

            //TODO:词法分析并不支持更加复杂的类型表达式，如A<"12>3">

            //State 0       开始
            //State 1+n     内部
            //Level

            //State 0
            //    EndOfString -> 结束
            //    < -> Level += 1, State 1
            //    > -> 错误
            //    \s -> 错误
            //    _ -> 加入到TypeNameChars
            //
            //State 1
            //    EndOfString -> 错误
            //    < -> Level += 1, 加入到ParamChars
            //    > -> Level -= 1, 如果Level > 0，则加入到ParamChars，否则检查是否字符串结束，若未结束则错误
            //    , -> 如果Level = 1, 则提交参数到Parameters，清空ParamChars
            //    _ -> 加入到ParamChars

            var TypeNameChars = new List<Char>();
            var ParamChars = new List<Char>();
            var ParameterStrings = new List<String>();

            var Index = 0;
            Action Proceed = () => Index += 1;
            Func<Boolean> EndOfString = () => Index >= TypeLine.Length;
            Func<String> PeekChar = () => TypeLine.Substring(Index, 1);

            var State = 0;
            var Level = 0;

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == "<")
                    {
                        Level += 1;
                        State = 1;
                    }
                    else if (rErrorChars.Match(c).Success)
                    {
                        throw InvalidCharExceptionGenerator(c);
                    }
                    else
                    {
                        TypeNameChars.AddRange(c);
                    }
                    Proceed();
                }
                else if (State == 1)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == "<")
                    {
                        Level += 1;
                        ParamChars.AddRange(c);
                    }
                    else if (c == ">")
                    {
                        Level -= 1;
                        if (Level == 0)
                        {
                            Proceed();
                            break;
                        }
                        ParamChars.AddRange(c);
                    }
                    else if (c == ",")
                    {
                        if (Level == 1)
                        {
                            ParameterStrings.Add(new String(ParamChars.ToArray()));
                            ParamChars.Clear();
                        }
                        else
                        {
                            ParamChars.AddRange(c);
                        }
                    }
                    else
                    {
                        ParamChars.AddRange(c);
                    }
                    Proceed();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            if (ParamChars.Count > 0)
            {
                ParameterStrings.Add(new String(ParamChars.ToArray()));
                ParamChars.Clear();
            }

            var TypeName = new String(TypeNameChars.ToArray());

            return new TypeSpecLiteral { TypeName = TypeName.Trim(' '), Parameters = ParameterStrings.Select(p => p.Trim(' ')).ToArray() };
        }
    }
}
