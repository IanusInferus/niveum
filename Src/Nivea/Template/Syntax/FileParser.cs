//==========================================================================
//
//  File:        FileParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 文件解析器
//  Version:     2016.05.26.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;
using Nivea.Template.Semantics;

namespace Nivea.Template.Syntax
{
    public class FileParserResult
    {
        public Semantics.File File;
        public Dictionary<Object, TextRange> Positions;
    }

    public static class FileParser
    {
        public static FileParserResult ParseFile(Text Text)
        {
            var TypeFunctions = new HashSet<String>() { "Primitive", "Alias", "Record", "TaggedUnion", "Enum" };
            var Functions = new HashSet<String>(TypeFunctions.Concat(new List<String>() { "Option", "Namespace", "Assembly", "Import", "Template", "Global" }));
            var TreeContentFunctions = new HashSet<String> { "Option" };
            var FreeContentFunctions = new HashSet<String> { "Template", "Global" };

            var ps = new TreeFormatParseSetting()
            {
                IsTableParameterFunction = Name => Functions.Contains(Name),
                IsTableContentFunction = Name => Functions.Contains(Name) && !TreeContentFunctions.Contains(Name) && !FreeContentFunctions.Contains(Name),
                IsTreeContentFunction = Name => TreeContentFunctions.Contains(Name)
            };

            var sp = new TreeFormatSyntaxParser(ps, Text);
            var ParserResult = sp.Parse();
            var ts = new TreeSerializer();

            var Sections = new List<Semantics.SectionDef>();
            var Positions = new Dictionary<Object, TextRange>();
            var InlineExpressionRegex = new Regex(@"\${(?<Expr>.*?)}", RegexOptions.ExplicitCapture);

            foreach (var TopNode in ParserResult.Value.MultiNodesList)
            {
                if (TopNode.OnFunctionNodes)
                {
                    var pr = new TreeFormatParseResult
                    {
                        Value = new Forest { MultiNodesList = new List<MultiNodes> { TopNode } },
                        Text = Text,
                        Positions = ParserResult.Positions,
                        RawFunctionCalls = ParserResult.RawFunctionCalls
                    };
                    var es = new TreeFormatEvaluateSetting
                    {
                        FunctionCallEvaluator = (f, nm) =>
                        {
                            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
                            {
                                var Range = nm.GetRange(SyntaxObj);
                                if (Range.OnHasValue)
                                {
                                    Positions.Add(SemanticsObj, Range.Value);
                                }
                            };

                            if (TypeFunctions.Contains(f.Name.Text))
                            {
                                if (f.Parameters.Count < 1 || f.Parameters.Count > 2) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var VersionedName = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");
                                var TypeRef = ParseTypeRef(VersionedName);
                                var Name = TypeRef.Name;
                                var Version = TypeRef.Version;

                                String Description = "";
                                if (f.Parameters.Count >= 2)
                                {
                                    var DescriptionParameter = f.Parameters[1];
                                    if (!DescriptionParameter.OnLeaf) { throw new InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                                    Description = DescriptionParameter.Leaf;
                                }

                                var ContentLines = new List<FunctionCallTableLine> { };
                                if (Functions.Contains(f.Name.Text) && f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.TableContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    ContentLines = ContentValue.TableContent;
                                }

                                switch (f.Name.Text)
                                {
                                    case "Primitive":
                                        {
                                            if (Version != "") { throw new InvalidEvaluationException("InvalidName", nm.GetFileRange(f.Parameters[0]), f.Parameters[0]); }

                                            var GenericParameters = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName = null;
                                                TypeSpec cType = null;
                                                String cDescription = null;

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = "";
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                if (cName.StartsWith("'"))
                                                {
                                                    cName = new String(cName.Skip(1).ToArray());
                                                    var gp = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                }
                                            }

                                            var p = new PrimitiveDef { Name = Name, GenericParameters = GenericParameters, Description = Description };
                                            Mark(p, f);
                                            var t = TypeDef.CreatePrimitive(p);
                                            Mark(t, f);
                                            var s = SectionDef.CreateType(t);
                                            Mark(s, f);
                                            Sections.Add(s);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Alias":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            TypeSpec Type = null;

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName = null;
                                                TypeSpec cType = null;
                                                String cDescription = null;

                                                if (Line.Nodes.Count == 1)
                                                {
                                                    if (Type != null)
                                                    {
                                                        throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                    }
                                                    Type = ParseTypeSpec(Line.Nodes[0], nm, Positions);
                                                    continue;
                                                }
                                                else if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = "";
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                if (cName.StartsWith("'"))
                                                {
                                                    cName = new String(cName.Skip(1).ToArray());
                                                    var gp = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                }
                                            }

                                            if (Type == null)
                                            {
                                                throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentLines), ContentLines);
                                            }

                                            var a = new AliasDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Type = Type, Description = Description };
                                            Mark(a, f);
                                            var t = TypeDef.CreateAlias(a);
                                            Mark(t, f);
                                            var s = SectionDef.CreateType(t);
                                            Mark(s, f);
                                            Sections.Add(s);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Record":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            var Fields = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName = null;
                                                TypeSpec cType = null;
                                                String cDescription = null;

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = "";
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                if (cName.StartsWith("'"))
                                                {
                                                    cName = new String(cName.Skip(1).ToArray());
                                                    var gp = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    var p = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(p, Line);
                                                    Fields.Add(p);
                                                }
                                            }

                                            var r = new RecordDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Fields = Fields, Description = Description };
                                            Mark(r, f);
                                            var t = TypeDef.CreateRecord(r);
                                            Mark(t, f);
                                            var s = SectionDef.CreateType(t);
                                            Mark(s, f);
                                            Sections.Add(s);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "TaggedUnion":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            var Alternatives = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName = null;
                                                TypeSpec cType = null;
                                                String cDescription = null;

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = "";
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    cDescription = GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription");
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                if (cName.StartsWith("'"))
                                                {
                                                    cName = new String(cName.Skip(1).ToArray());
                                                    var gp = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    var p = new VariableDef { Name = cName, Type = cType, Description = cDescription };
                                                    Mark(p, Line);
                                                    Alternatives.Add(p);
                                                }
                                            }

                                            var tu = new TaggedUnionDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Alternatives = Alternatives, Description = Description };
                                            Mark(tu, f);
                                            var t = TypeDef.CreateTaggedUnion(tu);
                                            Mark(t, f);
                                            var s = SectionDef.CreateType(t);
                                            Mark(s, f);
                                            Sections.Add(s);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Enum":
                                        {
                                            var Literals = new List<LiteralDef>();

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
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }
                                                NextValue = cValue + 1;

                                                var ltl = new LiteralDef { Name = cName, Value = cValue, Description = cDescription };
                                                Mark(ltl, Line);
                                                Literals.Add(ltl);
                                            }

                                            var UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" });
                                            var ed = new EnumDef { Name = Name, Version = Version, UnderlyingType = UnderlyingType, Literals = Literals, Description = Description };
                                            Mark(ed, f);
                                            var t = TypeDef.CreateEnum(ed);
                                            Mark(t, f);
                                            var s = SectionDef.CreateType(t);
                                            Mark(s, f);
                                            Sections.Add(s);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    default:
                                        {
                                            throw new InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                                        }
                                }
                            }
                            else if (f.Name.Text == "Option")
                            {
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var ContentNodes = new List<TFSemantics.Node> { };
                                if (f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.TreeContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    ContentNodes = ContentValue.TreeContent;
                                }

                                foreach (var Node in ContentNodes)
                                {
                                    if (Node.OnEmpty) { continue; }
                                    if (Node.OnLeaf) { throw new InvalidEvaluationException("InvalidOption", nm.GetFileRange(Node), Node); }
                                    var Name = Node.Stem.Name;
                                    if (Name == "InlineExpressionRegex")
                                    {
                                        if (Node.Stem.Children.Count != 1) { throw new InvalidEvaluationException("InvalidOption", nm.GetFileRange(Node), Node); }
                                        var ValueNode = Node.Stem.Children.Single();
                                        var InlineExpressionRegexValue = GetLeafNodeValue(ValueNode, nm, "InvalidOption");
                                        try
                                        {
                                            InlineExpressionRegex = new Regex(InlineExpressionRegexValue, RegexOptions.ExplicitCapture);
                                        }
                                        catch (ArgumentException ex)
                                        {
                                            throw new InvalidEvaluationException("InvalidInlineExpressionRegex", nm.GetFileRange(ValueNode), ValueNode, ex);
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidEvaluationException("UnknownOption", nm.GetFileRange(Node), Node);
                                    }
                                }

                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Namespace")
                            {
                                if (f.Parameters.Count != 1) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }
                                var Namespace = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");

                                var s = SectionDef.CreateNamepsace(Namespace);
                                Mark(s, f);
                                Sections.Add(s);
                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Assembly")
                            {
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var ContentLines = new List<FunctionCallTableLine> { };
                                if (f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.TableContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    ContentLines = ContentValue.TableContent;
                                }

                                var Assemblies = new List<String>();

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;

                                    if (Line.Nodes.Count == 1)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAssemblyName");
                                    }
                                    else if (Line.Nodes.Count == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }

                                    Assemblies.Add(cName);
                                }

                                Mark(Assemblies, f);
                                var s = SectionDef.CreateAssembly(Assemblies);
                                Mark(s, f);
                                Sections.Add(s);
                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Import")
                            {
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var ContentLines = new List<FunctionCallTableLine> { };
                                if (f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.TableContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    ContentLines = ContentValue.TableContent;
                                }

                                var Imports = new List<String>();

                                foreach (var Line in ContentLines)
                                {
                                    String cName = null;

                                    if (Line.Nodes.Count == 1)
                                    {
                                        cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidImport");
                                    }
                                    else if (Line.Nodes.Count == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }

                                    Imports.Add(cName);
                                }

                                Mark(Imports, f);
                                var s = SectionDef.CreateImport(Imports);
                                Mark(s, f);
                                Sections.Add(s);
                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Template")
                            {
                                if (f.Parameters.Count < 1) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var Name = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");

                                var Parameters = new List<VariableDef>();
                                foreach (var p in f.Parameters.Skip(1))
                                {
                                    var ParameterString = GetLeafNodeValue(p, nm, "InvalidParameter");
                                    var ParameterParts = ParameterString.Split(new Char[] { ':' }, 2);
                                    if (ParameterParts.Length != 2) { throw new InvalidEvaluationException("InvalidParameter", nm.GetFileRange(p), p); }
                                    var ParameterName = ParameterParts[0];
                                    var oRange = nm.GetRange(p);
                                    if (oRange.OnHasValue)
                                    {
                                        var Range = oRange.Value;
                                        var Start = nm.Text.Calc(Range.Start, ParameterName.Length + 1);
                                        oRange = new TextRange { Start = Start, End = Range.End };
                                    }
                                    var Type = ParseTypeSpec(ParameterParts[1], oRange, nm, Positions);
                                    Parameters.Add(new VariableDef { Name = ParameterName, Type = Type, Description = "" });
                                }

                                FunctionContent Content;
                                if (f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.LineContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    Content = ContentValue.LineContent;
                                }
                                else
                                {
                                    throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(f), f);
                                }

                                var Signature = new TemplateSignature { Name = Name, Parameters = Parameters };
                                var FirstRange = nm.GetRange(f.Parameters.First());
                                var EndRange = nm.GetRange(f.Parameters.Last());
                                if (FirstRange.OnHasValue && EndRange.OnHasValue)
                                {
                                    Positions.Add(Signature, new TextRange { Start = FirstRange.Value.Start, End = FirstRange.Value.End });
                                }

                                var Body = ExprParser.ParseTemplateBody(Content.Lines, Content.IndentLevel * 4, InlineExpressionRegex, nm, Positions);
                                Mark(Body, Content);
                                var t = new TemplateDef { Signature = Signature, Body = Body };
                                Mark(t, f);
                                var s = SectionDef.CreateTemplate(t);
                                Mark(s, f);
                                Sections.Add(s);
                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Global")
                            {
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                FunctionContent Content;
                                if (f.Content.OnHasValue)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (ContentValue._Tag != FunctionCallContentTag.LineContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    Content = ContentValue.LineContent;
                                }
                                else
                                {
                                    throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(f), f);
                                }

                                var Range = nm.GetRange(Content);
                                var Expr = ExprParser.ParseExprLines(Content.Lines, Range.OnHasValue ? Range.Value : Optional<TextRange>.Empty, Content.IndentLevel * 4, InlineExpressionRegex, nm, Positions);
                                var s = SectionDef.CreateGlobal(Expr);
                                Mark(s, f);
                                Sections.Add(s);
                                return new List<TFSemantics.Node> { };
                            }
                            else
                            {
                                throw new InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                            }
                        }
                    };
                    var e = new TreeFormatEvaluator(es, pr);
                    e.Evaluate();
                }
                else
                {
                    var pr = new TreeFormatParseResult
                    {
                        Value = new Forest { MultiNodesList = new List<MultiNodes> { TopNode } },
                        Text = Text,
                        Positions = ParserResult.Positions,
                        RawFunctionCalls = ParserResult.RawFunctionCalls
                    };
                    var es = new TreeFormatEvaluateSetting { };
                    var e = new TreeFormatEvaluator(es, pr);
                    var er = e.Evaluate();
                    var ReadResult = ts.Read<Semantics.File>(CollectionOperations.CreatePair(er.Value, er.Positions));
                    Sections.AddRange(ReadResult.Key.Sections);
                    foreach (var p in ReadResult.Value)
                    {
                        if (p.Value.Range.OnHasValue)
                        {
                            Positions.Add(p.Key, p.Value.Range.Value);
                        }
                    }
                }
            }

            var File = new Semantics.File { Sections = Sections };
            return new FileParserResult { File = File, Positions = Positions };
        }

        private static String GetLeafNodeValue(TFSemantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!n.OnLeaf) { throw new InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }

        private static TypeRef ParseTypeRef(String TypeString)
        {
            return Syntax.TypeParser.ParseTypeRef(TypeString);
        }

        private static TypeSpec ParseTypeSpec(TFSemantics.Node TypeNode, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var TypeSpecString = GetLeafNodeValue(TypeNode, nm, "InvalidTypeSpec");
            var oRange = nm.GetRange(TypeNode);
            return ParseTypeSpec(TypeSpecString, oRange, nm, Positions);
        }
        private static TypeSpec ParseTypeSpec(String TypeSpecString, Firefly.Texting.TreeFormat.Optional<TextRange> oRange, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var ts = Syntax.TypeParser.ParseTypeSpec
            (
                TypeSpecString,
                (o, Start, End) =>
                {
                    if (oRange.OnHasValue)
                    {
                        var Range = oRange.Value;
                        var TypeRange = new TextRange { Start = nm.Text.Calc(Range.Start, Start), End = nm.Text.Calc(Range.Start, End) };
                        Positions.Add(o, TypeRange);
                    }
                },
                Index =>
                {
                    var FileRange = Firefly.Texting.TreeFormat.Optional<FileTextRange>.Empty;
                    if (oRange.OnHasValue)
                    {
                        var Range = oRange.Value;
                        FileRange = new FileTextRange { Text = nm.Text, Range = new TextRange { Start = nm.Text.Calc(Range.Start, Index), End = nm.Text.Calc(Range.Start, Index + 1) } };
                    }
                    return new InvalidTokenException("InvalidChar", FileRange, TypeSpecString.Substring(Index, 1));
                }
            );
            return ts;
        }
    }
}
