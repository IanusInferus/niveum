//==========================================================================
//
//  File:        FileParser.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 文件解析器
//  Version:     2022.01.01.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Niveum.ObjectSchema
{

    public sealed class FileParserResult
    {
        public Text Text { get; init; }
        public Dictionary<Object, TextRange> Positions { get; init; }
        public List<TypeDef> Types { get; init; }
        public List<TypeDef> TypeRefs { get; init; }
        public List<String> Imports { get; init; }
        public Dictionary<TypeDef, List<String>> TypeToNamespace { get; init; }
        public Dictionary<TypeDef, List<List<String>>> TypeToNamespaceImports { get; init; }
    }

    public static class FileParser
    {
        public static FileParserResult ParseFile(Text Text)
        {
            var NormalTypeFunctions = new HashSet<String>() { "Primitive", "Alias", "Record", "TaggedUnion", "Enum", "ClientCommand", "ServerCommand" };
            var TableContentFunctions = new HashSet<String>(NormalTypeFunctions.Concat(new List<String>() { "Namespace", "Import" }));

            var ps = new TreeFormatParseSetting()
            {
                IsTableParameterFunction = Name => TableContentFunctions.Contains(Name) || Name == "Query",
                IsTableContentFunction = Name => TableContentFunctions.Contains(Name),
                IsTreeParameterFunction = Name => false,
                IsTreeContentFunction = Name => false
            };

            var sp = new TreeFormatSyntaxParser(ps, Text);
            var ParserResult = sp.Parse();
            var ts = new TreeSerializer();

            var Types = new List<TypeDef>();
            var TypeRefs = new List<TypeDef>();
            var Imports = new List<String>();
            var TypeToNamespace = new Dictionary<TypeDef, List<String>>();
            var TypeToNamespaceImports = new Dictionary<TypeDef, List<List<String>>>();
            var CurrentNamespace = new List<String>();
            var CurrentNamespaceImports = new List<List<String>>();

            var Positions = new Dictionary<Object, TextRange>();

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
                            Action<Object, Object> MarkSyntax = (SemanticsObj, SyntaxObj) =>
                            {
                                var Range = nm.GetRange(SyntaxObj);
                                if (Range.OnSome)
                                {
                                    ParserResult.Positions.Add(SemanticsObj, Range.Value);
                                }
                            };
                            Action<Object, Object, Object> MarkSyntax2 = (SemanticsObj, SyntaxObjStart, SyntaxObjEnd) =>
                            {
                                var RangeStart = nm.GetRange(SyntaxObjStart);
                                var RangeEnd = nm.GetRange(SyntaxObjEnd);
                                if (RangeStart.OnSome && RangeEnd.OnSome)
                                {
                                    ParserResult.Positions.Add(SemanticsObj, new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End });
                                }
                            };
                            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
                            {
                                var Range = nm.GetRange(SyntaxObj);
                                if (Range.OnSome)
                                {
                                    Positions.Add(SemanticsObj, Range.Value);
                                }
                            };
                            Action<Object, Object, Object> Mark2 = (SemanticsObj, SyntaxObjStart, SyntaxObjEnd) =>
                            {
                                var RangeStart = nm.GetRange(SyntaxObjStart);
                                var RangeEnd = nm.GetRange(SyntaxObjEnd);
                                if (RangeStart.OnSome && RangeEnd.OnSome)
                                {
                                    Positions.Add(SemanticsObj, new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End });
                                }
                            };

                            Func<TFSemantics.Node, List<String>> ExtractNamespaceParts = Node =>
                            {
                                var Namespace = GetLeafNodeValue(Node, nm, "InvalidName");

                                var NamespaceParts = new List<String>();
                                int InvalidCharIndex;
                                var osml = TokenParser.TrySplitSymbolMemberChain(Namespace, out InvalidCharIndex);
                                if (osml.OnNone)
                                {
                                    var Range = nm.GetRange(Node);
                                    var InvalidChar = Namespace.Substring(InvalidCharIndex, 1);
                                    if (Range.OnSome)
                                    {
                                        Range = new TextRange { Start = nm.Text.Calc(Range.Value.Start, InvalidCharIndex), End = nm.Text.Calc(Range.Value.Start, InvalidCharIndex + 1) };
                                    }
                                    throw new InvalidTokenException("InvalidChar", new FileTextRange { Text = nm.Text, Range = Range }, InvalidChar);
                                }
                                foreach (var p in osml.Value)
                                {
                                    if (p.Parameters.Count > 0)
                                    {
                                        var Range = nm.GetRange(Node);
                                        var Part = Namespace.Substring(p.SymbolStartIndex, p.SymbolEndIndex);
                                        if (Range.OnSome)
                                        {
                                            Range = new TextRange { Start = nm.Text.Calc(Range.Value.Start, p.SymbolStartIndex), End = nm.Text.Calc(Range.Value.Start, p.SymbolEndIndex) };
                                        }
                                        throw new InvalidTokenException("InvalidNamespacePart", new FileTextRange { Text = nm.Text, Range = Range }, Part);
                                    }
                                    int LocalInvalidCharIndex;
                                    var oName = TokenParser.TryUnescapeSymbolName(p.Name, out LocalInvalidCharIndex);
                                    if (oName.OnNone)
                                    {
                                        InvalidCharIndex = p.NameStartIndex + LocalInvalidCharIndex;
                                        var Range = nm.GetRange(Node);
                                        var InvalidChar = Namespace.Substring(InvalidCharIndex, 1);
                                        if (Range.OnSome)
                                        {
                                            Range = new TextRange { Start = nm.Text.Calc(Range.Value.Start, InvalidCharIndex), End = nm.Text.Calc(Range.Value.Start, InvalidCharIndex + 1) };
                                        }
                                        throw new InvalidTokenException("InvalidChar", new FileTextRange { Text = nm.Text, Range = Range }, InvalidChar);
                                    }
                                    NamespaceParts.Add(p.Name);
                                }

                                return NamespaceParts;
                            };

                            if (NormalTypeFunctions.Contains(f.Name.Text))
                            {
                                if (f.Parameters.Count < 1 || f.Parameters.Count > 2) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var TypeRef = ParseTypeRef(f.Parameters[0], nm, Positions);
                                var Name = TypeRef.Name;
                                var Version = TypeRef.Version;

                                var Attributes = new List<KeyValuePair<String, List<String>>>();
                                var Description = "";
                                if (f.Parameters.Count >= 2)
                                {
                                    var DescriptionParameter = f.Parameters[1];
                                    if (!DescriptionParameter.OnLeaf) { throw new InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                                    var c = TokenParser.DecomposeDescription(DescriptionParameter.Leaf);
                                    Attributes = c.Attributes;
                                    Mark(Attributes, f.Parameters[1]);
                                    Description = c.Description;
                                }

                                var ContentLines = new List<FunctionCallTableLine> { };
                                if (f.Content.OnSome)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (!ContentValue.OnTableContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
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
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
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
                                                    var gp = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                }
                                            }

                                            var p = new PrimitiveDef { Name = Name, GenericParameters = GenericParameters, Attributes = Attributes, Description = Description };
                                            Mark(p, f);
                                            var t = TypeDef.CreatePrimitive(p);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Alias":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            TypeSpec? Type = null;

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

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
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
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
                                                    var gp = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
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

                                            var a = new AliasDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Type = Type, Attributes = Attributes, Description = Description };
                                            Mark(a, f);
                                            var t = TypeDef.CreateAlias(a);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Record":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            var Fields = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
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
                                                    var gp = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    var p = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                    Mark(p, Line);
                                                    Fields.Add(p);
                                                }
                                            }

                                            var r = new RecordDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Fields = Fields, Attributes = Attributes, Description = Description };
                                            Mark(r, f);
                                            var t = TypeDef.CreateRecord(r);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "TaggedUnion":
                                        {
                                            var GenericParameters = new List<VariableDef>();
                                            var Alternatives = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidAlternativeName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
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
                                                    var gp = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    var p = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                    Mark(p, Line);
                                                    Alternatives.Add(p);
                                                }
                                            }

                                            var tu = new TaggedUnionDef { Name = Name, Version = Version, GenericParameters = GenericParameters, Alternatives = Alternatives, Attributes = Attributes, Description = Description };
                                            Mark(tu, f);
                                            var t = TypeDef.CreateTaggedUnion(tu);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Enum":
                                        {
                                            var Literals = new List<LiteralDef>();

                                            Int64 NextValue = 0;
                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                Int64 cValue = NextValue;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 1)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                                    cValue = NextValue;
                                                }
                                                else if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                                    cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidLiteralName");
                                                    cValue = NumericStrings.InvariantParseInt64(GetLeafNodeValue(Line.Nodes[1], nm, "InvalidLiteralValue"));
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
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

                                                var ltl = new LiteralDef { Name = cName, Value = cValue, Attributes = cAttributes, Description = cDescription };
                                                Mark(ltl, Line);
                                                Literals.Add(ltl);
                                            }

                                            var IntTypeName = new List<String> { "Int" };
                                            Mark(IntTypeName, f);
                                            var r = new TypeRef { Name = IntTypeName, Version = "" };
                                            Mark(r, f);
                                            var UnderlyingType = TypeSpec.CreateTypeRef(r);
                                            Mark(UnderlyingType, f);
                                            var ed = new EnumDef { Name = Name, Version = Version, UnderlyingType = UnderlyingType, Literals = Literals, Attributes = Attributes, Description = Description };
                                            Mark(ed, f);
                                            var t = TypeDef.CreateEnum(ed);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "ClientCommand":
                                        {
                                            var OutParameters = new List<VariableDef>();
                                            var InParameters = new List<VariableDef>();

                                            Boolean IsInParameter = false;
                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 1)
                                                {
                                                    if (GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName") == ">")
                                                    {
                                                        IsInParameter = true;
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                    }
                                                }
                                                else if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                var p = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                Mark(p, Line);
                                                if (IsInParameter)
                                                {
                                                    InParameters.Add(p);
                                                }
                                                else
                                                {
                                                    OutParameters.Add(p);
                                                }
                                            }

                                            var cc = new ClientCommandDef { Name = Name, Version = Version, OutParameters = OutParameters, InParameters = InParameters, Attributes = Attributes, Description = Description };
                                            Mark(cc, f);
                                            var t = TypeDef.CreateClientCommand(cc);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "ServerCommand":
                                        {
                                            var OutParameters = new List<VariableDef>();

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName;
                                                TypeSpec cType;
                                                var cAttributes = new List<KeyValuePair<String, List<String>>>();
                                                var cDescription = "";

                                                if (Line.Nodes.Count == 2)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                }
                                                else if (Line.Nodes.Count == 3)
                                                {
                                                    cName = GetLeafNodeValue(Line.Nodes[0], nm, "InvalidFieldName");
                                                    cType = ParseTypeSpec(Line.Nodes[1], nm, Positions);
                                                    var c = TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
                                                    cAttributes = c.Attributes;
                                                    Mark(cAttributes, Line.Nodes[2]);
                                                    cDescription = c.Description;
                                                }
                                                else if (Line.Nodes.Count == 0)
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                                }

                                                var p = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription };
                                                Mark(p, Line);
                                                OutParameters.Add(p);
                                            }

                                            var sc = new ServerCommandDef { Name = Name, Version = Version, OutParameters = OutParameters, Attributes = Attributes, Description = Description };
                                            Mark(sc, f);
                                            var t = TypeDef.CreateServerCommand(sc);
                                            Mark(t, f);
                                            Types.Add(t);
                                            TypeToNamespace.Add(t, CurrentNamespace);
                                            TypeToNamespaceImports.Add(t, CurrentNamespaceImports);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    default:
                                        {
                                            throw new InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                                        }
                                }
                            }
                            else if (f.Name.Text == "Query")
                            {
                                if (f.Parameters.Count < 2) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var TypeRef = ParseTypeRef(f.Parameters[0], nm, Positions);
                                var Name = TypeRef.Name;
                                var Version = TypeRef.Version;
                                if (Version != "") { throw new InvalidEvaluationException("InvalidQueryName", nm.GetFileRange(f), f); }

                                var RootType = ParseTypeSpec(f.Parameters[1], nm, Positions);

                                var Parameters = new List<VariableDef>();
                                foreach (var p in f.Parameters.Skip(2))
                                {
                                    var ParameterString = GetLeafNodeValue(p, nm, "InvalidParameter");
                                    var ParameterParts = ParameterString.Split(new Char[] { ':' }, 2);
                                    if (ParameterParts.Length != 2) { throw new InvalidEvaluationException("InvalidParameter", nm.GetFileRange(p), p); }
                                    var ParameterName = ParameterParts[0];
                                    var oRange = nm.GetRange(p);
                                    if (oRange.OnSome)
                                    {
                                        var Range = oRange.Value;
                                        var Start = nm.Text.Calc(Range.Start, ParameterName.Length + 1);
                                        oRange = new TextRange { Start = Start, End = Range.End };
                                    }
                                    var Type = ParseTypeSpec(ParameterParts[1], oRange, nm, Positions);
                                    var v = new VariableDef { Name = ParameterName, Type = Type, Description = "" };
                                    Mark(v, p);
                                    Parameters.Add(v);
                                }
                                Mark(Parameters, f.Parameters);

                                var Lines = new List<(int IndentLevel, List<Token> Tokens)>();
                                if (f.Content.OnSome)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (!ContentValue.OnLineContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    foreach (var Line in ContentValue.LineContent.Lines)
                                    {
                                        if (TreeFormatTokenParser.IsBlankLine(Line)) { continue; }
                                        var StartIndex = Line.Text.TakeWhile(c => c == ' ').Count();
                                        if (StartIndex % 4 != 0) { throw new InvalidEvaluationException("InvalidLineIndentation", nm.GetFileRange(Line), Line); }
                                        var IndentLevel =  StartIndex / 4 - ContentValue.LineContent.IndentLevel;

                                        var Tokens = new List<Token>();

                                        var Range = Line.Range;

                                        while (true)
                                        {
                                            var otpr = TreeFormatTokenParser.ReadToken(Text, ParserResult.Positions, Range);
                                            if (otpr.OnNone) { break; }
                                            var tpr = otpr.Value;
                                            if (!tpr.Token.OnSingleLineComment)
                                            {
                                                Tokens.Add(tpr.Token);
                                            }
                                            if (tpr.RemainingChars.OnNone) { break; }
                                            Range = tpr.RemainingChars.Value;
                                        }

                                        if (Tokens.Count > 0)
                                        {
                                            MarkSyntax2(Tokens, Tokens.First(), Tokens.Last());
                                        }
                                        else
                                        {
                                            MarkSyntax(Tokens, Line);
                                        }

                                        Lines.Add((IndentLevel, Tokens));
                                    }
                                }
                                QueryPath ParseQueryPath(Token Token)
                                {
                                    var v = GetStringValue(Token, nm, "InvalidQueryPath");
                                    if (v == "..")
                                    {
                                        var p = QueryPath.CreateParent();
                                        Mark(p, Token);
                                        return p;
                                    }
                                    else
                                    {
                                        var p = QueryPath.CreateLocalName(v);
                                        Mark(p, Token);
                                        return p;
                                    }
                                }

                                var Numerals = new Dictionary<String, Func<Numeral>>
                                {
                                    { "Optional", () => Numeral.CreateOptional() },
                                    { "One", () => Numeral.CreateOne()},
                                    { "Many", () => Numeral.CreateMany() },
                                    { "All", () => Numeral.CreateAll() },
                                    { "Range", () => Numeral.CreateRange() },
                                    { "Count", () => Numeral.CreateCount() }
                                };

                                OrderedField ParseOrderedField(Token t)
                                {
                                    var Value = GetStringValue(t, nm, "InvalidOrderedField");
                                    OrderedField f;
                                    if (Value.EndsWith("-"))
                                    {
                                        f = new OrderedField { Name = Value.Substring(0, Value.Length - 1), IsDescending = true };
                                    }
                                    else
                                    {
                                        f = new OrderedField { Name = Value, IsDescending = false };
                                    }
                                    Mark(f, t);
                                    return f;
                                }

                                QuerySelect ParseQuerySelect(List<Token> Tokens, List<QueryParameter> Arguments)
                                {
                                    if ((Tokens.Count < 2) || !(Tokens[0].OnLeftParenthesis && Tokens[Tokens.Count - 1].OnRightParenthesis)) { throw new InvalidEvaluationException("InvalidQueryFilter", nm.GetFileRange(Tokens), Tokens); }

                                    var NumeralName = GetStringValue(Tokens[1], nm, "InvalidNumeral");
                                    if (!Numerals.ContainsKey(NumeralName)) { throw new InvalidTokenException("InvalidNumeral", nm.GetFileRange(Tokens[2]), NumeralName); }
                                    var Numeral = Numerals[NumeralName]();
                                    Mark(Numeral, Tokens[2]);

                                    var By = new List<String>();
                                    var OrderBy = new List<OrderedField>();

                                    int NextPosition;
                                    if (Tokens.Count >= 5)
                                    {
                                        var ByOrOrderByName = GetStringValue(Tokens[2], nm, "InvalidByOrOrderBy");
                                        if (ByOrOrderByName == "By")
                                        {
                                            if (Tokens[3].OnSingleLineLiteral)
                                            {
                                                By = new List<String> { Tokens[3].SingleLineLiteral };
                                                NextPosition = 4;
                                            }
                                            else if (Tokens[3].OnLeftParenthesis)
                                            {
                                                By = Tokens.Skip(4).TakeWhile(t => !t.OnRightParenthesis).Select(t => GetStringValue(t, nm, "InvalidBySpec")).ToList();
                                                NextPosition = 5 + By.Count;
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidBy", nm.GetFileRange(Tokens[2]));
                                            }
                                            Mark(By, Tokens[2]);
                                        }
                                        else if (ByOrOrderByName == "OrderBy")
                                        {
                                            if (Tokens[3].OnSingleLineLiteral)
                                            {
                                                OrderBy = new List<OrderedField> { ParseOrderedField(Tokens[3]) };
                                                NextPosition = 4;
                                            }
                                            else if (Tokens[3].OnLeftParenthesis)
                                            {
                                                OrderBy = Tokens.Skip(4).TakeWhile(t => !t.OnRightParenthesis).Select(t => ParseOrderedField(t)).ToList();
                                                NextPosition = 5 + By.Count;
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(Tokens[2]));
                                            }
                                            Mark(OrderBy, Tokens[2]);
                                        }
                                        else
                                        {
                                            throw new InvalidSyntaxException("InvalidByOrOrderBy", nm.GetFileRange(Tokens[2]));
                                        }
                                        if (Tokens.Count >= NextPosition + 3)
                                        {
                                            if (OrderBy.Count != 0)
                                            {
                                                throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(Tokens[NextPosition]));
                                            }
                                            var OrderByName = GetStringValue(Tokens[NextPosition], nm, "InvalidOrderBy");
                                            if (OrderByName == "OrderBy")
                                            {
                                                if (Tokens[NextPosition + 1].OnSingleLineLiteral)
                                                {
                                                    OrderBy = new List<OrderedField> { ParseOrderedField(Tokens[NextPosition + 1]) };
                                                }
                                                else if (Tokens[NextPosition + 1].OnLeftParenthesis)
                                                {
                                                    OrderBy = Tokens.Skip(NextPosition + 2).TakeWhile(t => !t.OnRightParenthesis).Select(t => ParseOrderedField(t)).ToList();
                                                }
                                                else
                                                {
                                                    throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(Tokens[2]));
                                                }
                                                Mark(OrderBy, Tokens[NextPosition]);
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(Tokens[NextPosition]));
                                            }
                                        }
                                    }

                                    var s = new QuerySelect { Numeral = Numeral, By = By, OrderBy = OrderBy, Arguments = Arguments };
                                    Mark(s, Tokens);
                                    return s;
                                }

                                QueryMappingExpr ParseQueryMappingExpr(List<Token> Tokens, String mName, Optional<List<QueryMappingSpec>> SubMappings)
                                {
                                    QueryMappingExpr e;
                                    if (Tokens.Count == 1)
                                    {
                                        e = new QueryMappingExpr { Variable = QueryPath.CreateLocalName(mName), Function = Optional<QueryFunction>.Empty, SubMappings = SubMappings };
                                    }
                                    else if (Tokens.Count == 2)
                                    {
                                        var Variable = ParseQueryPath(Tokens[1]);
                                        e = new QueryMappingExpr { Variable = Variable, Function = Optional<QueryFunction>.Empty, SubMappings = SubMappings };
                                    }
                                    else if ((Tokens.Count == 5) && (Tokens[1].OnSingleLineLiteral && Tokens[1].SingleLineLiteral == "Count") && Tokens[2].OnLeftParenthesis && Tokens[4].OnRightParenthesis)
                                    {
                                        var Variable = ParseQueryPath(Tokens[3]);
                                        var Function = QueryFunction.CreateCount();
                                        Mark2(Function, Tokens[1], Tokens[4]);
                                        e = new QueryMappingExpr { Variable = Variable, Function = Function, SubMappings = SubMappings };
                                    }
                                    else if ((Tokens.Count >= 5) && (Tokens[1].OnSingleLineLiteral && Tokens[1].SingleLineLiteral == "Select") && Tokens[2].OnLeftParenthesis && Tokens[Tokens.Count - 1].OnRightParenthesis)
                                    {
                                        var Variable = ParseQueryPath(Tokens[3]);

                                        var ArgumentTokens = Tokens.AsEnumerable().Reverse().Skip(1).TakeWhile(t => !t.OnRightParenthesis).Reverse().ToList();
                                        var Arguments = new List<QueryParameter>();
                                        foreach (var t in ArgumentTokens)
                                        {
                                            var ArgumentName = GetStringValue(t, nm, "InvalidArgumentName");
                                            var Argument = new QueryParameter { Name = ArgumentName };
                                            Mark(Argument, t);
                                            Arguments.Add(Argument);
                                        }
                                        if (ArgumentTokens.Count > 0)
                                        {
                                            Mark2(Arguments, ArgumentTokens.First(), ArgumentTokens.Last());
                                        }
                                        else
                                        {
                                            Mark(Arguments, Tokens.Last());
                                        }

                                        var QueryFilterNodes = Tokens.Skip(4).Take(Tokens.Count - 5 - ArgumentTokens.Count).ToList();
                                        Mark2(QueryFilterNodes, Tokens[4], Tokens.Last());
                                        var Select = ParseQuerySelect(QueryFilterNodes, Arguments);

                                        var Function = QueryFunction.CreateSelect(Select);
                                        Mark2(Function, Tokens[1], Tokens[4]);
                                        e = new QueryMappingExpr { Variable = Variable, Function = Function, SubMappings = SubMappings };
                                    }
                                    else
                                    {
                                        throw new InvalidEvaluationException("InvalidMapping", nm.GetFileRange(Tokens), Tokens);
                                    }
                                    Mark(e, Tokens);
                                    return e;
                                }

                                List<QueryMappingSpec> ParseQueryMappingSpecs(int StartLineIndex, int EndLineIndex, int IndentLevel)
                                {
                                    var l = new List<QueryMappingSpec>();
                                    for (int k = StartLineIndex; k < EndLineIndex; k += 1)
                                    {
                                        var (CurrentIndentLevel, Tokens) = Lines[k];
                                        if (CurrentIndentLevel != IndentLevel) { throw new InvalidEvaluationException("InvalidLineIndentation", nm.GetFileRange(Tokens), Tokens); }
                                        var ChildStartLineIndex = k + 1;
                                        var ChildEndLineIndex = Enumerable.Range(ChildStartLineIndex, EndLineIndex - ChildStartLineIndex).TakeWhile(j => Lines[j].IndentLevel > CurrentIndentLevel).Select(j => j + 1).Cast<int?>().LastOrDefault() ?? ChildStartLineIndex;
                                        var Children = ParseQueryMappingSpecs(ChildStartLineIndex, ChildEndLineIndex, IndentLevel + 1);
                                        var SubMappings = Children.Count == 0 ? Optional<List<QueryMappingSpec>>.Empty : Children;

                                        var mName = GetStringValue(Tokens[0], nm, "InvalidMappingName");
                                        var e = ParseQueryMappingExpr(Tokens, mName, SubMappings);
                                        var s = new QueryMappingSpec { Name = mName, Expr = e };

                                        l.Add(s);
                                        k = ChildEndLineIndex - 1;
                                    }
                                    return l;
                                }

                                var MappingSpecs = ParseQueryMappingSpecs(0, Lines.Count, 0);

                                var q = new QueryDef { Name = Name, RootType = RootType, MappingSpecs = MappingSpecs };
                                Mark(q, f);
                                var t = TypeDef.CreateQuery(q);
                                Mark(t, f);
                                Types.Add(t);
                                TypeToNamespace.Add(t, CurrentNamespace);
                                TypeToNamespaceImports.Add(t, CurrentNamespaceImports);

                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Namespace")
                            {
                                if (f.Parameters.Count != 1) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }
                                var NamespaceParts = ExtractNamespaceParts(f.Parameters[0]);

                                Mark(NamespaceParts, f);
                                CurrentNamespace = NamespaceParts;
                                return new List<TFSemantics.Node> { };
                            }
                            else if (f.Name.Text == "Import")
                            {
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var ContentLines = new List<FunctionCallTableLine> { };
                                if (f.Content.OnSome)
                                {
                                    var ContentValue = f.Content.Value;
                                    if (!ContentValue.OnTableContent) { throw new InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                                    ContentLines = ContentValue.TableContent;
                                }

                                var NamespaceImports = new List<List<String>>();

                                foreach (var Line in ContentLines)
                                {
                                    if (Line.Nodes.Count == 1)
                                    {
                                        var NamespaceParts = ExtractNamespaceParts(Line.Nodes[0]);
                                        Mark(NamespaceParts, Line.Nodes[0]);
                                        NamespaceImports.Add(NamespaceParts);
                                    }
                                    else if (Line.Nodes.Count == 0)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        throw new InvalidEvaluationException("InvalidLineNodeCount", nm.GetFileRange(Line), Line);
                                    }
                                }

                                CurrentNamespaceImports = CurrentNamespaceImports.Concat(NamespaceImports).ToList();

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
                    if (er.Value.Nodes.Count > 0)
                    {
                        var ReadResult = ts.Read<Schema>(CollectionOperations.CreatePair(er.Value, er.Positions));
                        Types.AddRange(ReadResult.Key.Types);
                        TypeRefs.AddRange(ReadResult.Key.TypeRefs);
                        Imports.AddRange(ReadResult.Key.Imports);
                        foreach (var p in ReadResult.Value)
                        {
                            if (p.Value.Range.OnSome)
                            {
                                Positions.Add(p.Key, p.Value.Range.Value);
                            }
                        }
                    }
                }
            }

            return new FileParserResult { Text = Text, Positions = Positions, Types = Types, TypeRefs = TypeRefs, Imports = Imports, TypeToNamespace = TypeToNamespace, TypeToNamespaceImports = TypeToNamespaceImports };
        }

        private static String GetLeafNodeValue(TFSemantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!n.OnLeaf) { throw new InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }

        private static String GetStringValue(Token t, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!t.OnSingleLineLiteral) { throw new InvalidEvaluationException(ErrorCause, nm.GetFileRange(t), t); }
            return t.SingleLineLiteral;
        }

        private static TypeRef ParseTypeRef(TFSemantics.Node TypeNode, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var ts = ParseTypeSpec(TypeNode, nm, Positions);
            if (!ts.OnTypeRef)
            {
                throw new InvalidEvaluationException("ExpectedTypeRef", nm.GetFileRange(ts), ts);
            }
            return ts.TypeRef;
        }

        private static TypeSpec ParseTypeSpec(TFSemantics.Node TypeNode, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var TypeSpecString = GetLeafNodeValue(TypeNode, nm, "InvalidTypeSpec");
            var oRange = nm.GetRange(TypeNode);
            return ParseTypeSpec(TypeSpecString, oRange, nm, Positions);
        }
        private static TypeSpec ParseTypeSpec(String TypeSpecString, TreeFormat.Optional<TextRange> oRange, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var ts = TypeParser.ParseTypeSpec
            (
                TypeSpecString,
                (o, Start, End) =>
                {
                    if (oRange.OnSome)
                    {
                        var Range = oRange.Value;
                        var TypeRange = new TextRange { Start = nm.Text.Calc(Range.Start, Start), End = nm.Text.Calc(Range.Start, End) };
                        Positions.Add(o, TypeRange);
                    }
                },
                Index =>
                {
                    var FileRange = TreeFormat.Optional<FileTextRange>.Empty;
                    if (oRange.OnSome)
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
