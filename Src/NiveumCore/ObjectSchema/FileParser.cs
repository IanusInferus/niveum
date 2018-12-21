//==========================================================================
//
//  File:        FileParser.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 文件解析器
//  Version:     2018.12.01.
//  Copyright(C) F.R.C.
//
//==========================================================================

using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Niveum.ObjectSchema
{

    public class FileParserResult
    {
        public Text Text;
        public Dictionary<Object, TextRange> Positions;
        public List<TypeDef> Types;
        public List<TypeDef> TypeRefs;
        public List<String> Imports;
        public Dictionary<TypeDef, List<String>> TypeToNamespace;
        public Dictionary<TypeDef, List<List<String>>> TypeToNamespaceImports;
    }

    public static class FileParser
    {
        public static FileParserResult ParseFile(Text Text)
        {
            var TypeFunctions = new HashSet<String>() { "Primitive", "Alias", "Record", "TaggedUnion", "Enum", "ClientCommand", "ServerCommand" };
            var Functions = new HashSet<String>(TypeFunctions.Concat(new List<String>() { "Namespace", "Import" }));

            var ps = new TreeFormatParseSetting()
            {
                IsTableParameterFunction = Name => Functions.Contains(Name),
                IsTableContentFunction = Name => Functions.Contains(Name),
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
                            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
                            {
                                var Range = nm.GetRange(SyntaxObj);
                                if (Range.OnHasValue)
                                {
                                    Positions.Add(SemanticsObj, Range.Value);
                                }
                            };

                            Func<TFSemantics.Node, List<String>> ExtractNamespaceParts = Node =>
                            {
                                var Namespace = GetLeafNodeValue(Node, nm, "InvalidName");

                                var NamespaceParts = new List<String>();
                                int InvalidCharIndex;
                                var osml = TokenParser.TrySplitSymbolMemberChain(Namespace, out InvalidCharIndex);
                                if (osml.OnNotHasValue)
                                {
                                    var Range = nm.GetRange(Node);
                                    var InvalidChar = Namespace.Substring(InvalidCharIndex, 1);
                                    if (Range.OnHasValue)
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
                                        if (Range.OnHasValue)
                                        {
                                            Range = new TextRange { Start = nm.Text.Calc(Range.Value.Start, p.SymbolStartIndex), End = nm.Text.Calc(Range.Value.Start, p.SymbolEndIndex) };
                                        }
                                        throw new InvalidTokenException("InvalidNamespacePart", new FileTextRange { Text = nm.Text, Range = Range }, Part);
                                    }
                                    int LocalInvalidCharIndex;
                                    var oName = TokenParser.TryUnescapeSymbolName(p.Name, out LocalInvalidCharIndex);
                                    if (oName.OnNotHasValue)
                                    {
                                        InvalidCharIndex = p.NameStartIndex + LocalInvalidCharIndex;
                                        var Range = nm.GetRange(Node);
                                        var InvalidChar = Namespace.Substring(InvalidCharIndex, 1);
                                        if (Range.OnHasValue)
                                        {
                                            Range = new TextRange { Start = nm.Text.Calc(Range.Value.Start, InvalidCharIndex), End = nm.Text.Calc(Range.Value.Start, InvalidCharIndex + 1) };
                                        }
                                        throw new InvalidTokenException("InvalidChar", new FileTextRange { Text = nm.Text, Range = Range }, InvalidChar);
                                    }
                                    NamespaceParts.Add(p.Name);
                                }

                                return NamespaceParts;
                            };

                            if (TypeFunctions.Contains(f.Name.Text))
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
                                if (f.Content.OnHasValue)
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
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                            TypeSpec Type = null;

                                            foreach (var Line in ContentLines)
                                            {
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                                String cName = null;
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
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                                String cName = null;
                                                TypeSpec cType = null;
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
                                if (f.Content.OnHasValue)
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
                            if (p.Value.Range.OnHasValue)
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
            var ts = TypeParser.ParseTypeSpec
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
                    var FileRange = TreeFormat.Optional<FileTextRange>.Empty;
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
