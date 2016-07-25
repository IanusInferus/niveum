﻿//==========================================================================
//
//  File:        RelationSchemaLoader.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构加载器
//  Version:     2016.07.26.
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
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Yuki.RelationSchema
{
    public sealed class RelationSchemaLoader
    {
        private List<Semantics.Node> RTypes;
        private List<Semantics.Node> RTypeRefs;
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
            RTypes = new List<Semantics.Node>();
            RTypeRefs = new List<Semantics.Node>();
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
            var TypesQueryListsNode = MakeStemNode("Types", RTypes.Concat(Types.Where(n => n.OnStem && n.Stem.Name == "QueryList")).Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var TypeRefsQueryListsNode = MakeStemNode("TypeRefs", RTypeRefs.Concat(Types.Where(n => n.OnStem && n.Stem.Name == "QueryList")).Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var RelationSchema = MakeStemNode("Schema", TypesQueryListsNode, TypeRefsQueryListsNode, MakeStemNode("Imports"), MakeStemNode("TypePaths"));
            var rtfr = new TreeFormatResult { Value = new Semantics.Forest { Nodes = new List<Semantics.Node> { RelationSchema } }, Positions = Positions };
            var rx = XmlInterop.TreeToXml(rtfr);
            var rs = xs.Read<Schema>(rx);

            var TypesNode = MakeStemNode("Types", Types.Where(n => n.OnStem && n.Stem.Name != "QueryList").Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var TypeRefsNode = MakeStemNode("TypeRefs", TypeRefs.Where(n => n.OnStem && n.Stem.Name != "QueryList").Select(n => MakeStemNode("TypeDef", n)).ToArray());
            var ImportsNode = MakeStemNode("Imports", Imports.ToArray());
            var TypePathsNode = MakeStemNode("TypePaths", TypePaths.ToArray());
            var ObjectSchema = MakeStemNode("Schema", TypesNode, TypeRefsNode, ImportsNode, TypePathsNode);
            var tfr = new TreeFormatResult { Value = new Semantics.Forest { Nodes = new List<Semantics.Node> { ObjectSchema } }, Positions = Positions };

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
                if (t.OnPrimitive)
                {
                    foreach (var v in t.Primitive.GenericParameters)
                    {
                        v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Primitive.Name, Map, TypePathDict);
                    }
                }
                else if (t.OnRecord)
                {
                    foreach (var v in t.Record.GenericParameters.Concat(t.Record.Fields))
                    {
                        v.Type = ParseTypeSpec(v.Type.TypeRef.Name, t.Record.Name, Map, TypePathDict);
                    }
                }
                else if (t.OnEnum)
                {
                    t.Enum.UnderlyingType = ParseTypeSpec(t.Enum.UnderlyingType.TypeRef.Name, t.Enum.Name, Map, TypePathDict);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            var s = RelationSchemaTranslator.Translate(os);
            s.Types = s.Types.Concat(rs.Types).ToList();
            s.TypeRefs = s.TypeRefs.Concat(rs.TypeRefs).ToList();

            s.Verify();

            return s;
        }
        private Semantics.Node MakeLeafNode(String Value)
        {
            var n = Semantics.Node.CreateLeaf(Value);
            return n;
        }
        private Semantics.Node MakeStemNode(String Name, params Semantics.Node[] Children)
        {
            var s = new Semantics.Stem { Name = Name, Children = Children.ToList() };
            var n = Semantics.Node.CreateStem(s);
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
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new List<Syntax.TextLine> { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        public void LoadSchema(String TreePath, StreamReader Reader)
        {
            var t = TreeFile.ReadDirect(Reader, TreePath, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            LoadSchema(t);
        }
        public void LoadSchema(TreeFormatResult t)
        {
            var SchemaContent = t.Value.Nodes.Single(n => n.OnStem && n.Stem.Name == "Schema").Stem.Children;
            var TypesNodes = SchemaContent.Single(n => n.OnStem && n.Stem.Name == "Types").Stem.Children.Where(n => n.OnStem).SelectMany(n => n.Stem.Children);
            var TypeRefsNodes = SchemaContent.Single(n => n.OnStem && n.Stem.Name == "TypeRefs").Stem.Children.Where(n => n.OnStem).SelectMany(n => n.Stem.Children);
            RTypes.AddRange(TypesNodes);
            RTypeRefs.AddRange(TypeRefsNodes);
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
                Parsed.Add(Name);
            }
            Imports.AddRange(SchemaContent.Single(n => n.OnStem && n.Stem.Name == "Imports").Stem.Children.Where(n => n.OnStem));
            TypePaths.AddRange(SchemaContent.Single(n => n.OnStem && n.Stem.Name == "TypePaths").Stem.Children.Where(n => n.OnStem));
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
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new List<Syntax.TextLine> { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void Load(String TreePath, StreamReader Reader, List<Semantics.Node> Types)
        {
            var Functions = new HashSet<String>() { "Primitive", "Entity", "Enum", "Query" };
            var TableParameterFunctions = Functions;
            var TableContentFunctions = new HashSet<String>(Functions.Except(new List<String> { "Query" }));
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
            var pr = TreeFile.ReadRaw(Reader, TreePath, ps);
            var Text = pr.Text;

            var Verbs = new HashSet<String> { "Select", "Lock", "Insert", "Update", "Upsert", "Delete" };
            var Numerals = new HashSet<String> { "Optional", "One", "Many", "All", "Range", "Count" };
            Func<int, Syntax.TextLine, ISemanticsNodeMaker, List<Semantics.Node>> ParseQueryDefAsSemanticsNodes = (IndentLevel, Line, nm) =>
            {
                var l = new List<Semantics.Node>();
                List<Semantics.Node> cl = null;
                Syntax.TextPosition clStart = default(Syntax.TextPosition);
                Syntax.TextPosition clEnd = default(Syntax.TextPosition);
                if (Line.Text.Length < IndentLevel * 4)
                {
                    return new List<Semantics.Node> { };
                }
                var LineRange = new Syntax.TextRange { Start = Text.Calc(Line.Range.Start, IndentLevel * 4), End = Line.Range.End };
                var Range = LineRange;
                while (true)
                {
                    var tpr = TreeFormatTokenParser.ReadToken(Text, pr.Positions, Range);
                    if (!tpr.OnHasValue)
                    {
                        break;
                    }

                    var v = tpr.Value.Token;
                    if (v.OnSingleLineComment) { break; }
                    if (v.OnLeftParenthesis)
                    {
                        if (cl != null)
                        {
                            throw new Syntax.InvalidTokenException("DoubleLeftParenthesis", new Syntax.FileTextRange { Text = Text, Range = Range }, "(");
                        }
                        cl = new List<Semantics.Node>();
                        clStart = Range.Start;
                        clEnd = Range.End;
                    }
                    else if (v.OnRightParenthesis)
                    {
                        if (cl == null)
                        {
                            throw new Syntax.InvalidTokenException("DismatchedRightParenthesis", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (cl.Count == 0)
                        {
                            throw new Syntax.InvalidTokenException("EmptyIndex", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (tpr.Value.RemainingChars.OnHasValue)
                        {
                            clEnd = tpr.Value.RemainingChars.Value.End;
                        }
                        l.Add(nm.MakeStemNode("", cl, new Syntax.TextRange { Start = clStart, End = clEnd }));
                        cl = null;
                        clStart = default(Syntax.TextPosition);
                        clEnd = default(Syntax.TextPosition);
                    }
                    else if (v.OnSingleLineLiteral)
                    {
                        if (cl != null)
                        {
                            cl.Add(nm.MakeLeafNode(v.SingleLineLiteral, pr.Positions[v]));
                        }
                        else
                        {
                            l.Add(nm.MakeLeafNode(v.SingleLineLiteral, pr.Positions[v]));
                        }
                    }
                    else
                    {
                        throw new Syntax.InvalidTokenException("UnknownToken", new Syntax.FileTextRange { Text = Text, Range = Range }, Text.GetTextInLine(Range));
                    }

                    if (!tpr.Value.RemainingChars.OnHasValue)
                    {
                        break;
                    }

                    Range = tpr.Value.RemainingChars.Value;
                }
                if (cl != null)
                {
                    throw new Syntax.InvalidTokenException("DismatchedRightParentheses", new Syntax.FileTextRange { Text = Text, Range = Range }, "");
                }

                if (l.Count == 0) { return new List<Semantics.Node> { }; }

                if (l.Count != 4 && l.Count != 6 && l.Count != 8)
                {
                    throw new Syntax.InvalidSyntaxException("InvalidQuery", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
                var From = GetLeafNodeValue(l[0], nm, "InvalidFrom");
                if (From != "From") { throw new Syntax.InvalidTokenException("InvalidFrom", nm.GetFileRange(l[0]), From); }
                var EntityName = GetLeafNodeValue(l[1], nm, "InvalidEntityName");
                var VerbName = GetLeafNodeValue(l[2], nm, "InvalidVerb");
                if (!Verbs.Contains(VerbName)) { throw new Syntax.InvalidTokenException("InvalidVerb", nm.GetFileRange(l[2]), VerbName); }
                var NumeralName = GetLeafNodeValue(l[3], nm, "InvalidNumeral");
                if (!Numerals.Contains(NumeralName)) { throw new Syntax.InvalidTokenException("InvalidNumeral", nm.GetFileRange(l[3]), NumeralName); }

                var ByIndex = new List<String> { };
                var OrderByIndex = new List<String> { };

                if (l.Count >= 6)
                {
                    var ByOrOrderByName = GetLeafNodeValue(l[4], nm, "InvalidByOrOrderBy");
                    if (ByOrOrderByName == "By")
                    {
                        if (l[5].OnLeaf)
                        {
                            ByIndex = new List<String> { l[5].Leaf };
                        }
                        else if (l[5].OnStem)
                        {
                            ByIndex = l[5].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).ToList();
                        }
                        else
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidBy", nm.GetFileRange(l[5]));
                        }
                    }
                    else if (ByOrOrderByName == "OrderBy")
                    {
                        if (l[5].OnLeaf)
                        {
                            OrderByIndex = new List<String> { l[5].Leaf };
                        }
                        else if (l[5].OnStem)
                        {
                            OrderByIndex = l[5].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).ToList();
                        }
                        else
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[5]));
                        }
                    }
                    else
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidByOrOrderBy", nm.GetFileRange(l[5]));
                    }
                }
                if (l.Count >= 8)
                {
                    if (OrderByIndex.Count != 0)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[6]));
                    }
                    var OrderByName = GetLeafNodeValue(l[6], nm, "InvalidOrderBy");
                    if (OrderByName == "OrderBy")
                    {
                        if (l[7].OnLeaf)
                        {
                            OrderByIndex = new List<String> { l[7].Leaf };
                        }
                        else if (l[7].OnStem)
                        {
                            OrderByIndex = l[7].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).ToList();
                        }
                        else
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[7]));
                        }
                    }
                    else
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[7]));
                    }
                }

                var OrderByIndexColumns = OrderByIndex.Select(c => c.EndsWith("-") ? MakeStemNode("KeyColumn", MakeStemNode("Name", MakeLeafNode(c.Substring(0, c.Length - 1))), MakeStemNode("IsDescending", MakeLeafNode("True"))) : MakeStemNode("KeyColumn", MakeStemNode("Name", MakeLeafNode(c)), MakeStemNode("IsDescending", MakeLeafNode("False")))).ToList();

                return new List<Semantics.Node>
                {
                    MakeStemNode("QueryDef",
                        MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                        MakeStemNode("Verb", MakeStemNode(VerbName)),
                        MakeStemNode("Numeral", MakeStemNode(NumeralName)),
                        MakeStemNode("By", ByIndex.Select(c => MakeStemNode("StringLiteral", MakeLeafNode(c))).ToArray()),
                        MakeStemNode("OrderBy", OrderByIndexColumns.ToArray())
                    )
                };
            };

            var es = new TreeFormatEvaluateSetting()
            {
                FunctionCallEvaluator = (f, nm) =>
                {
                    if (f.Parameters.Count == 0)
                    {
                        if (f.Name.Text == "Query")
                        {
                            var Nodes = f.Content.Value.LineContent.Lines.SelectMany(Line => ParseQueryDefAsSemanticsNodes(f.Content.Value.LineContent.IndentLevel, Line, nm)).ToList();
                            return new List<Semantics.Node>
                            {
                                MakeStemNode("QueryList",
                                    MakeStemNode("Queries", Nodes.ToArray())
                                )
                            };
                        }
                        else
                        {
                            if (tfeo != null)
                            {
                                return tfeo.FunctionCallEvaluator(f, nm);
                            }
                            throw new Syntax.InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                        }
                    }
                    else if (f.Parameters.Count == 1 || f.Parameters.Count == 2)
                    {
                        var VersionedName = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");
                        var Name = VersionedName;
                        var Version = GetVersion(ref Name);

                        String Description = "";
                        if (f.Parameters.Count >= 2)
                        {
                            var DescriptionParameter = f.Parameters[1];
                            if (!DescriptionParameter.OnLeaf) { throw new Syntax.InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                            Description = DescriptionParameter.Leaf;
                        }

                        var ContentLines = new List<Syntax.FunctionCallTableLine> { };
                        if (Functions.Contains(f.Name.Text) && f.Content.OnHasValue)
                        {
                            var ContentValue = f.Content.Value;
                            if (!ContentValue.OnTableContent) { throw new Syntax.InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
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

                                        if (Line.Nodes.Count == 2)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                            cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                            cDescription = "";
                                        }
                                        else if (Line.Nodes.Count == 3)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                            cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                            cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                        }
                                        else if (Line.Nodes.Count == 0)
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

                                    return new List<Semantics.Node>
                                    {
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

                                        if (Line.Nodes.Count == 2)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                            cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                            cDescription = "";
                                        }
                                        else if (Line.Nodes.Count == 3)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                            cType = VirtualParseTypeSpec(Line.Nodes[1], nm);
                                            cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                        }
                                        else if (Line.Nodes.Count == 0)
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

                                    return new List<Semantics.Node>
                                    {
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

                                        if (Line.Nodes.Count == 1)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                            cValue = NextValue;
                                            cDescription = "";
                                        }
                                        else if (Line.Nodes.Count == 2)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                            cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                            cDescription = "";
                                        }
                                        else if (Line.Nodes.Count == 3)
                                        {
                                            cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                            cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                            cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                        }
                                        else if (Line.Nodes.Count == 0)
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

                                    return new List<Semantics.Node>
                                    {
                                        MakeStemNode("Enum",
                                            MakeStemNode("Name", MakeLeafNode(Name)),
                                            MakeStemNode("Version", MakeLeafNode(Version)),
                                            MakeStemNode("UnderlyingType", BuildVirtualTypeSpec("Int")),
                                            MakeStemNode("Literals", Literals.ToArray()),
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
                    }
                    else
                    {
                        throw new Syntax.InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f);
                    }
                },
                TokenParameterEvaluator = tfeo != null ? tfeo.TokenParameterEvaluator : null
            };

            var tfe = new TreeFormatEvaluator(es, pr);
            var t = tfe.Evaluate();
            Types.AddRange(t.Value.Nodes);
            foreach (var p in t.Positions)
            {
                Positions.Add(p.Key, p.Value);
            }
            foreach (var n in t.Value.Nodes)
            {
                var Type = n.Stem.Name;
                var Names = n.Stem.Children.Where(c => c.Stem.Name == "Name").ToList();
                if (Names.Count == 0) { continue; }
                var Name = Names.Single().Stem.Children.Single().Leaf;
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
            if (!n.OnLeaf) { throw new Syntax.InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
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
