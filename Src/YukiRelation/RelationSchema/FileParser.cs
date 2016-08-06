//==========================================================================
//
//  File:        FileParser.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 文件解析器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema
{

    public class FileParserResult
    {
        public Schema Schema;
        public Text Text;
        public Dictionary<Object, TextRange> Positions;
    }

    public static class FileParser
    {
        public static FileParserResult ParseFile(Text Text)
        {
            var TypeFunctions = new HashSet<String>() { "Primitive", "Entity", "Enum" };
            var TableParameterFunctions = new HashSet<String>(TypeFunctions.Concat(new List<String> { "Query" }));
            var TableContentFunctions = TypeFunctions;

            var ps = new TreeFormatParseSetting()
            {
                IsTableParameterFunction = Name => TableParameterFunctions.Contains(Name),
                IsTableContentFunction = Name => TableContentFunctions.Contains(Name)
            };

            var sp = new TreeFormatSyntaxParser(ps, Text);
            var ParserResult = sp.Parse();
            var ts = new TreeSerializer();

            var Types = new List<TypeDef>();
            var TypeRefs = new List<TypeDef>();
            var Imports = new List<String>();

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

                            if (TypeFunctions.Contains(f.Name.Text))
                            {
                                if (f.Parameters.Count < 1 || f.Parameters.Count > 2) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var VersionedName = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");
                                var TypeRef = ParseTypeRef(VersionedName);
                                Mark(TypeRef, f.Parameters[0]);
                                var Name = (String)TypeRef;

                                var Attributes = new List<KeyValuePair<String, List<String>>>();
                                var Description = "";
                                if (f.Parameters.Count >= 2)
                                {
                                    var DescriptionParameter = f.Parameters[1];
                                    if (!DescriptionParameter.OnLeaf) { throw new InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                                    var c = OS.TokenParser.DecomposeDescription(DescriptionParameter.Leaf);
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
                                                    var c = OS.TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
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
                                                    var gp = new VariableDef { Name = cName, Type = cType, Attributes = cAttributes, Description = cDescription, Attribute = null };
                                                    Mark(gp, Line);
                                                    GenericParameters.Add(gp);
                                                }
                                                else
                                                {
                                                    throw new InvalidEvaluationException("InvalidLine", nm.GetFileRange(Line), Line);
                                                }
                                            }

                                            var p = new PrimitiveDef { Name = Name, Attributes = Attributes, Description = Description };
                                            Mark(p, f);
                                            var t = TypeDef.CreatePrimitive(p);
                                            Mark(t, f);
                                            Types.Add(t);
                                            return new List<TFSemantics.Node> { };
                                        }
                                    case "Entity":
                                        {
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
                                                    var c = OS.TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
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
                                                Fields.Add(p);
                                            }

                                            var ed = new EntityDef { Name = Name, Fields = Fields, Attributes = Attributes, Description = Description };
                                            Mark(ed, f);
                                            var t = TypeDef.CreateEntity(ed);
                                            Mark(t, f);
                                            Types.Add(t);
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
                                                    var c = OS.TokenParser.DecomposeDescription(GetLeafNodeValue(Line.Nodes[2], nm, "InvalidDescription"));
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

                                            var r = (TypeRef)("Int");
                                            Mark(r, f);
                                            var UnderlyingType = TypeSpec.CreateTypeRef(r);
                                            Mark(UnderlyingType, f);
                                            var ed = new EnumDef { Name = Name, UnderlyingType = UnderlyingType, Literals = Literals, Attributes = Attributes, Description = Description };
                                            Mark(ed, f);
                                            var t = TypeDef.CreateEnum(ed);
                                            Mark(t, f);
                                            Types.Add(t);
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
                                if (f.Parameters.Count != 0) { throw new InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                                var Verbs = new Dictionary<String, Func<Verb>>
                                {
                                    { "Select", () => Verb.CreateSelect() },
                                    { "Lock", () => Verb.CreateLock() },
                                    { "Insert", () => Verb.CreateInsert() },
                                    { "Update", () => Verb.CreateUpdate() },
                                    { "Upsert", () => Verb.CreateUpsert() },
                                    { "Delete", () => Verb.CreateDelete() }
                                };
                                var Numerals = new Dictionary<String, Func<Numeral>>
                                {
                                    { "Optional", () => Numeral.CreateOptional() },
                                    { "One", () => Numeral.CreateOne()},
                                    { "Many", () => Numeral.CreateMany() },
                                    { "All", () => Numeral.CreateAll() },
                                    { "Range", () => Numeral.CreateRange() },
                                    { "Count", () => Numeral.CreateCount() }
                                };
                                Func<int, TextLine, List<QueryDef>> ParseQueryDef = (IndentLevel, Line) =>
                                {
                                    var l = new List<TFSemantics.Node>();
                                    List<TFSemantics.Node> cl = null;
                                    TextPosition clStart = default(TextPosition);
                                    TextPosition clEnd = default(TextPosition);
                                    if (Line.Text.Length < IndentLevel * 4)
                                    {
                                        return new List<QueryDef> { };
                                    }
                                    var LineRange = new TextRange { Start = Text.Calc(Line.Range.Start, IndentLevel * 4), End = Line.Range.End };
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
                                                throw new InvalidTokenException("DoubleLeftParenthesis", new FileTextRange { Text = Text, Range = Range }, "(");
                                            }
                                            cl = new List<TFSemantics.Node>();
                                            clStart = Range.Start;
                                            clEnd = Range.End;
                                        }
                                        else if (v.OnRightParenthesis)
                                        {
                                            if (cl == null)
                                            {
                                                throw new InvalidTokenException("DismatchedRightParenthesis", new FileTextRange { Text = Text, Range = Range }, ")");
                                            }
                                            if (cl.Count == 0)
                                            {
                                                throw new InvalidTokenException("EmptyIndex", new FileTextRange { Text = Text, Range = Range }, ")");
                                            }
                                            if (tpr.Value.RemainingChars.OnHasValue)
                                            {
                                                clEnd = tpr.Value.RemainingChars.Value.End;
                                            }
                                            l.Add(nm.MakeStemNode("", cl, new TextRange { Start = clStart, End = clEnd }));
                                            cl = null;
                                            clStart = default(TextPosition);
                                            clEnd = default(TextPosition);
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
                                            throw new InvalidTokenException("UnknownToken", new FileTextRange { Text = Text, Range = Range }, Text.GetTextInLine(Range));
                                        }

                                        if (!tpr.Value.RemainingChars.OnHasValue)
                                        {
                                            break;
                                        }

                                        Range = tpr.Value.RemainingChars.Value;
                                    }
                                    if (cl != null)
                                    {
                                        throw new InvalidTokenException("DismatchedRightParentheses", new FileTextRange { Text = Text, Range = Range }, "");
                                    }

                                    if (l.Count == 0) { return new List<QueryDef> { }; }

                                    if (l.Count != 4 && l.Count != 6 && l.Count != 8)
                                    {
                                        throw new InvalidSyntaxException("InvalidQuery", new FileTextRange { Text = Text, Range = LineRange });
                                    }
                                    var From = GetLeafNodeValue(l[0], nm, "InvalidFrom");
                                    if (From != "From") { throw new InvalidTokenException("InvalidFrom", nm.GetFileRange(l[0]), From); }
                                    var EntityName = GetLeafNodeValue(l[1], nm, "InvalidEntityName");
                                    var VerbName = GetLeafNodeValue(l[2], nm, "InvalidVerb");
                                    if (!Verbs.ContainsKey(VerbName)) { throw new InvalidTokenException("InvalidVerb", nm.GetFileRange(l[2]), VerbName); }
                                    var Verb = Verbs[VerbName]();
                                    Mark(Verb, l[2]);
                                    var NumeralName = GetLeafNodeValue(l[3], nm, "InvalidNumeral");
                                    if (!Numerals.ContainsKey(NumeralName)) { throw new InvalidTokenException("InvalidNumeral", nm.GetFileRange(l[3]), NumeralName); }
                                    var Numeral = Numerals[NumeralName]();
                                    Mark(Numeral, l[3]);

                                    var By = new List<String> { };
                                    var OrderBy = new List<KeyColumn> { };

                                    if (l.Count >= 6)
                                    {
                                        var ByOrOrderByName = GetLeafNodeValue(l[4], nm, "InvalidByOrOrderBy");
                                        if (ByOrOrderByName == "By")
                                        {
                                            if (l[5].OnLeaf)
                                            {
                                                By = new List<String> { l[5].Leaf };
                                            }
                                            else if (l[5].OnStem)
                                            {
                                                By = l[5].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).ToList();
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidBy", nm.GetFileRange(l[5]));
                                            }
                                            Mark(By, l[5]);
                                        }
                                        else if (ByOrOrderByName == "OrderBy")
                                        {
                                            if (l[5].OnLeaf)
                                            {
                                                OrderBy = (new List<String> { l[5].Leaf }).Select(c => c.EndsWith("-") ? new KeyColumn { Name = c.Substring(0, c.Length - 1), IsDescending = true } : new KeyColumn { Name = c, IsDescending = false }).ToList();
                                            }
                                            else if (l[5].OnStem)
                                            {
                                                OrderBy = l[5].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).Select(c => c.EndsWith("-") ? new KeyColumn { Name = c.Substring(0, c.Length - 1), IsDescending = true } : new KeyColumn { Name = c, IsDescending = false }).ToList();
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[5]));
                                            }
                                            Mark(OrderBy, l[5]);
                                        }
                                        else
                                        {
                                            throw new InvalidSyntaxException("InvalidByOrOrderBy", nm.GetFileRange(l[5]));
                                        }
                                    }
                                    if (l.Count >= 8)
                                    {
                                        if (OrderBy.Count != 0)
                                        {
                                            throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[6]));
                                        }
                                        var OrderByName = GetLeafNodeValue(l[6], nm, "InvalidOrderBy");
                                        if (OrderByName == "OrderBy")
                                        {
                                            if (l[7].OnLeaf)
                                            {
                                                OrderBy = (new List<String> { l[7].Leaf }).Select(c => c.EndsWith("-") ? new KeyColumn { Name = c.Substring(0, c.Length - 1), IsDescending = true } : new KeyColumn { Name = c, IsDescending = false }).ToList();
                                            }
                                            else if (l[7].OnStem)
                                            {
                                                OrderBy = l[7].Stem.Children.Select(c => GetLeafNodeValue(c, nm, "InvalidKeyColumn")).Select(c => c.EndsWith("-") ? new KeyColumn { Name = c.Substring(0, c.Length - 1), IsDescending = true } : new KeyColumn { Name = c, IsDescending = false }).ToList();
                                            }
                                            else
                                            {
                                                throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[7]));
                                            }
                                            Mark(OrderBy, l[7]);
                                        }
                                        else
                                        {
                                            throw new InvalidSyntaxException("InvalidOrderBy", nm.GetFileRange(l[7]));
                                        }
                                    }

                                    var q = new QueryDef { EntityName = EntityName, Verb = Verb, Numeral = Numeral, By = By, OrderBy = OrderBy };
                                    Mark(q, Line);
                                    return new List<QueryDef> { q };
                                };

                                var Queries = f.Content.Value.LineContent.Lines.SelectMany(Line => ParseQueryDef(f.Content.Value.LineContent.IndentLevel, Line)).ToList();
                                Mark(Queries, f);
                                var ql = new QueryListDef { Queries = Queries };
                                Mark(ql, f);
                                var t = TypeDef.CreateQueryList(ql);
                                Mark(t, f);
                                Types.Add(t);
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

            var Schema = new Schema { Types = Types, TypeRefs = TypeRefs, Imports = Imports };
            return new FileParserResult { Schema = Schema, Text = Text, Positions = Positions };
        }

        private static String GetLeafNodeValue(TFSemantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!n.OnLeaf) { throw new InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }

        private static TypeRef ParseTypeRef(String TypeString)
        {
            return (TypeRef)(TypeString);
        }

        private static TypeSpec ParseTypeSpec(TFSemantics.Node TypeNode, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var TypeSpecString = GetLeafNodeValue(TypeNode, nm, "InvalidTypeSpec");
            var InnerPositions = new Dictionary<Object, TextRange>();
            var ts = OS.TypeParser.ParseTypeSpec
            (
                TypeSpecString,
                (o, Start, End) =>
                {
                    var oRange = nm.GetRange(TypeNode);
                    if (oRange.OnHasValue)
                    {
                        var Range = oRange.Value;
                        var TypeRange = new TextRange { Start = nm.Text.Calc(Range.Start, Start), End = nm.Text.Calc(Range.Start, End) };
                        InnerPositions.Add(o, TypeRange);
                    }
                },
                Index =>
                {
                    var FileRange = TreeFormat.Optional<FileTextRange>.Empty;
                    var oRange = nm.GetRange(TypeNode);
                    if (oRange.OnHasValue)
                    {
                        var Range = oRange.Value;
                        FileRange = new FileTextRange { Text = nm.Text, Range = new TextRange { Start = nm.Text.Calc(Range.Start, Index), End = nm.Text.Calc(Range.Start, Index + 1) } };
                    }
                    return new InvalidTokenException("InvalidChar", FileRange, TypeSpecString.Substring(Index, 1));
                }
            );
            return TranslateTypeSpec(ts, nm.Text, InnerPositions, Positions);
        }
        private static TypeSpec TranslateTypeSpec(OS.TypeSpec t, Text Text, Dictionary<Object, TextRange> InnerPositions, Dictionary<Object, TextRange> Positions)
        {
            if (t.OnTypeRef)
            {
                var r = TypeSpec.CreateTypeRef((TypeRef)(t.TypeRef.Name));
                if (InnerPositions.ContainsKey(t))
                {
                    Positions.Add(r, InnerPositions[t]);
                }
                return r;
            }
            else if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && t.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && t.GenericTypeSpec.ParameterValues.Count == 1)
            {
                var Parameter = t.GenericTypeSpec.ParameterValues.Single();
                var InnerType = TranslateTypeSpec(Parameter, Text, InnerPositions, Positions);
                if (InnerType.OnTypeRef)
                {
                    var r = TypeSpec.CreateOptional(InnerType.TypeRef);
                    if (InnerPositions.ContainsKey(t))
                    {
                        Positions.Add(r, InnerPositions[t]);
                    }
                    return r;
                }
            }
            else if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && t.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && t.GenericTypeSpec.ParameterValues.Count == 1)
            {
                var Parameter = t.GenericTypeSpec.ParameterValues.Single();
                if (Parameter.OnTypeRef && Parameter.TypeRef.Name == "Byte")
                {
                    var r = TypeSpec.CreateTypeRef((TypeRef)("Binary"));
                    if (InnerPositions.ContainsKey(t))
                    {
                        Positions.Add(r, InnerPositions[t]);
                    }
                    return r;
                }
                else
                {
                    var InnerType = TranslateTypeSpec(Parameter, Text, InnerPositions, Positions);
                    if (InnerType.OnTypeRef)
                    {
                        var r = TypeSpec.CreateList(InnerType.TypeRef);
                        if (InnerPositions.ContainsKey(t))
                        {
                            Positions.Add(r, InnerPositions[t]);
                        }
                        return r;
                    }
                }
            }
            var oRange = InnerPositions.ContainsKey(t) ? InnerPositions[t] : TreeFormat.Optional<TextRange>.Empty;
            var FileRange = new FileTextRange { Text = Text, Range = oRange };
            throw new InvalidEvaluationException("InvalidTypeSpec", FileRange, t);
        }
    }
}
