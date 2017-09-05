//==========================================================================
//
//  File:        ExprParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式解析器
//  Version:     2017.09.05.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using Nivea.Template.Semantics;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;

namespace Nivea.Template.Syntax
{
    public static class ExprParser
    {
        public static Expr ParseConstantBody(TFSemantics.Node Value, TypeSpec Type, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            Action<Object, Object> Mark = (SemanticsObj, SyntaxObj) =>
            {
                if (Positions.ContainsKey(SyntaxObj))
                {
                    Positions.Add(SemanticsObj, Positions[SyntaxObj]);
                }
                var Range = nm.GetRange(SyntaxObj);
                if (Range.OnHasValue)
                {
                    Positions.Add(SemanticsObj, Range.Value);
                }
            };

            //TODO 支持复杂类型
            if (Type.OnTypeRef)
            {
                if (Type.TypeRef.Version != "") { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                var Name = Type.TypeRef.Name;
                if (!(Value.OnStem && (Value.Stem.Children.Count == 1))) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                var One = Value.Stem.Children.Single();
                if (One.OnEmpty)
                {
                    if (Name == "Unit")
                    {
                        var e = Expr.CreateDefault();
                        Mark(e, Value);
                        return e;
                    }
                    else
                    {
                        throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type);
                    }
                }
                else if (One.OnLeaf)
                {
                    if (Name == "Boolean")
                    {
                        if (One.Leaf == "False")
                        {
                            var pe = new PrimitiveLiteralExpr { Type = Type, Value = "False" };
                            Mark(pe, Value);
                            var e = Expr.CreatePrimitiveLiteral(pe);
                            Mark(e, Value);
                            return e;
                        }
                        else if (One.Leaf == "True")
                        {
                            var pe = new PrimitiveLiteralExpr { Type = Type, Value = "True" };
                            Mark(pe, Value);
                            var e = Expr.CreatePrimitiveLiteral(pe);
                            Mark(e, Value);
                            return e;
                        }
                        else
                        {
                            throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value);
                        }
                    }
                    else if (Name == "String")
                    {
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Int")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Real")
                    {
                        if (!TokenParser.IsFloatLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Byte")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseUInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < Byte.MinValue) || (i.Value > Byte.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "UInt8")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseUInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < Byte.MinValue) || (i.Value > Byte.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "UInt16")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseUInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < UInt16.MinValue) || (i.Value > UInt16.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "UInt32")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseUInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < UInt32.MinValue) || (i.Value > UInt32.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "UInt64")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseUInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Int8")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < SByte.MinValue) || (i.Value > SByte.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Int16")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < Int16.MinValue) || (i.Value > Int16.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Int32")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < Int32.MinValue) || (i.Value > Int32.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Int64")
                    {
                        if (!TokenParser.IsIntLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseInt64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Float32")
                    {
                        if (!TokenParser.IsFloatLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseFloat64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        if ((i.Value < Single.MinValue) || (i.Value > Single.MaxValue)) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Float64")
                    {
                        if (!TokenParser.IsFloatLiteral(One.Leaf)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                        var i = TokenParser.TryParseFloat64Literal(One.Leaf);
                        if (i.OnNotHasValue) { throw new InvalidEvaluationException("ValueExceedRange", nm.GetFileRange(Value), Value); }
                        var pe = new PrimitiveLiteralExpr { Type = Type, Value = One.Leaf };
                        Mark(pe, Value);
                        var e = Expr.CreatePrimitiveLiteral(pe);
                        Mark(e, Value);
                        return e;
                    }
                    else if (Name == "Type")
                    {
                        int InvalidCharIndex;
                        var ot = TypeParser.TryParseTypeSpec(One.Leaf, (o, Start, End) =>
                        {
                            var Range = nm.GetRange(One);
                            if (Range.OnHasValue)
                            {
                                if ((Range.Value.Start.Row == Range.Value.End.Row) && (nm.Text.GetTextInLine(Range.Value) == One.Leaf))
                                {
                                    Positions.Add(o, new TextRange { Start = nm.Text.Calc(Range.Value.Start, Start), End = nm.Text.Calc(Range.Value.Start, End) });
                                }
                                else
                                {
                                    Positions.Add(o, Range.Value);
                                }
                            }
                        }, out InvalidCharIndex);
                        if (ot.OnHasValue)
                        {
                            var e = Expr.CreateTypeLiteral(ot.Value);
                            Mark(e, Value);
                            return e;
                        }
                        else
                        {
                            var FileRange = nm.GetFileRange(One);
                            if (FileRange.OnHasValue && FileRange.Value.Range.OnHasValue)
                            {
                                var Range = FileRange.Value.Range.Value;
                                if ((Range.Start.Row == Range.End.Row) && (nm.Text.GetTextInLine(Range) == One.Leaf))
                                {
                                    FileRange = new FileTextRange { Text = nm.Text, Range = new TextRange { Start = nm.Text.Calc(Range.Start, InvalidCharIndex), End = nm.Text.Calc(Range.Start, InvalidCharIndex + 1) } };
                                }
                            }
                            throw new InvalidTokenException("InvalidChar", FileRange, One.Leaf.Substring(InvalidCharIndex, 1));
                        }
                    }
                    else
                    {
                        throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type);
                    }
                }
                else
                {
                    throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type);
                }
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (!Type.GenericTypeSpec.TypeSpec.OnTypeRef) { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                if (Type.GenericTypeSpec.TypeSpec.TypeRef.Version != "") { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                var Name = Type.GenericTypeSpec.TypeSpec.TypeRef.Name;
                if (!Value.OnStem) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                if (Name == "Optional")
                {
                    if (Type.GenericTypeSpec.ParameterValues.Count != 1) { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                    var ElementType = Type.GenericTypeSpec.ParameterValues.Single();
                    if (Value.Stem.Children.Count != 1) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value); }
                    var One = Value.Stem.Children.Single();
                    if (One.Stem.Name == "NotHasValue")
                    {
                        if (!One.OnStem || One.Stem.Children.Count != 1 || !One.Stem.Children.Single().OnEmpty) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(One), One); }
                        var tule = new TaggedUnionLiteralExpr { Type = Type, Alternative = "NotHasValue", Expr = Optional<Expr>.Empty };
                        Mark(tule, Value);
                        var e = Expr.CreateTaggedUnionLiteral(tule);
                        Mark(e, Value);
                        return e;
                    }
                    else if (One.Stem.Name == "HasValue")
                    {
                        var ve = ParseConstantBody(One, ElementType, nm, Positions);
                        var tule = new TaggedUnionLiteralExpr { Type = Type, Alternative = "HasValue", Expr = ve };
                        Mark(tule, Value);
                        var e = Expr.CreateTaggedUnionLiteral(tule);
                        Mark(e, Value);
                        return e;
                    }
                    else
                    {
                        throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value);
                    }
                }
                else if ((Name == "List") || (Name == "Set"))
                {
                    if (Type.GenericTypeSpec.ParameterValues.Count != 1) { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                    var ElementType = Type.GenericTypeSpec.ParameterValues.Single();
                    var l = new List<Expr> { };
                    foreach (var v in Value.Stem.Children)
                    {
                        if (!v.OnStem) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(v), v); }
                        l.Add(ParseConstantBody(v, ElementType, nm, Positions));
                    }
                    Mark(l, Value);
                    var lle = new ListLiteralExpr { Type = Type, Elements = l };
                    Mark(lle, Value);
                    var e = Expr.CreateListLiteral(lle);
                    Mark(e, Value);
                    return e;
                }
                else if (Name == "Map")
                {
                    if (Type.GenericTypeSpec.ParameterValues.Count != 2) { throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type); }
                    var KeyType = Type.GenericTypeSpec.ParameterValues[0];
                    var ValueType = Type.GenericTypeSpec.ParameterValues[1];
                    var l = new List<Expr> { };
                    foreach (var v in Value.Stem.Children)
                    {
                        if (!v.OnStem) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(v), v); }
                        if (v.Stem.Children.Count != 2) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(v), v); }
                        var Keys = v.Stem.Children.Where(c => c.OnStem && c.Stem.Name == "Key").ToList();
                        var Values = v.Stem.Children.Where(c => c.OnStem && c.Stem.Name == "Value").ToList();
                        if ((Keys.Count != 1) || (Values.Count != 1)) { throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(v), v); }
                        var KeyExpr = ParseConstantBody(Keys.Single(), KeyType, nm, Positions);
                        var ValueExpr = ParseConstantBody(Values.Single(), ValueType, nm, Positions);
                        var tt = TypeSpec.CreateTuple(Type.GenericTypeSpec.ParameterValues);
                        Mark(tt, Type);
                        var Elements = new List<Expr> { KeyExpr, ValueExpr };
                        Mark(Elements, v);
                        var tle = new TupleLiteralExpr { Type = tt, Elements = Elements };
                        Mark(tle, v);
                        var ve = Expr.CreateTupleLiteral(tle);
                        Mark(ve, v);
                        l.Add(ve);
                    }
                    Mark(l, Value);
                    var lle = new ListLiteralExpr { Type = Type, Elements = l };
                    Mark(lle, Value);
                    var e = Expr.CreateListLiteral(lle);
                    Mark(e, Value);
                    return e;
                }
                else
                {
                    throw new InvalidEvaluationException("ValueNotMatchType", nm.GetFileRange(Value), Value);
                }
            }
            else
            {
                throw new InvalidEvaluationException("TypeNotSupportedInConstant", nm.GetFileRange(Type), Type);
            }
        }

        public static List<TemplateExpr> ParseTemplateBody(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, List<KeyValuePair<String, Regex>> FilterNameAndRegex, Dictionary<String, List<String>> FilterNameToParameters, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var Body = new List<TemplateExpr>();
            var LineQueue = new LinkedList<TextLine>(Lines);
            while (LineQueue.Count > 0)
            {
                var Line = LineQueue.First.Value;
                LineQueue.RemoveFirst();
                var LineText = Line.Text.Substring(Math.Min(LinesIndentSpace, Line.Text.Length));
                var Trimmed = LineText.Trim(' ');
                if (Trimmed == "$$")
                {
                    var IndentSpace = LineText.TakeWhile(c => c == ' ').Count();
                    var HeadRange = Line.Range;
                    var IndentedExprLines = new LinkedList<TextLine>();
                    var HasEnd = false;
                    while (LineQueue.Count > 0)
                    {
                        var ChildLine = LineQueue.First.Value;
                        var ChildLineText = ChildLine.Text.Substring(Math.Min(LinesIndentSpace, ChildLine.Text.Length));
                        var SpaceCount = ChildLineText.TakeWhile(c => c == ' ').Count();
                        if (TokenParser.IsBlankLine(ChildLineText))
                        {
                            IndentedExprLines.AddLast(ChildLine);
                            LineQueue.RemoveFirst();
                        }
                        else if ((SpaceCount == IndentSpace) && (ChildLineText.Substring(SpaceCount) == "$End"))
                        {
                            LineQueue.RemoveFirst();
                            HasEnd = true;
                            break;
                        }
                        else if (SpaceCount <= IndentSpace)
                        {
                            break;
                        }
                        else if (SpaceCount >= IndentSpace + 4)
                        {
                            IndentedExprLines.AddLast(ChildLine);
                            LineQueue.RemoveFirst();
                        }
                        else
                        {
                            throw new InvalidSyntaxException("InvalidIndent", new FileTextRange { Text = nm.Text, Range = ChildLine.Range });
                        }
                    }
                    if (!HasEnd)
                    {
                        while (IndentedExprLines.Count > 0)
                        {
                            var ChildLine = IndentedExprLines.Last.Value;
                            var ChildLineText = ChildLine.Text.Substring(Math.Min(LinesIndentSpace, ChildLine.Text.Length));
                            var SpaceCount = ChildLineText.TakeWhile(c => c == ' ').Count();
                            if (TokenParser.IsBlankLine(ChildLineText))
                            {
                                LineQueue.AddFirst(ChildLine);
                                IndentedExprLines.RemoveLast();
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    var Range = new TextRange { Start = HeadRange.Start, End = HeadRange.End };
                    if (IndentedExprLines.Count > 0)
                    {
                        Range.End = IndentedExprLines.Last().Range.End;
                    }

                    var e = ParseExprLines(IndentedExprLines.ToList(), Range, LinesIndentSpace + IndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, EnableEmbeddedExpr, nm, Positions);
                    var ee = new IndentedExpr { IndentSpace = IndentSpace, Expr = e };

                    Positions.Add(ee, Range);
                    var te = TemplateExpr.CreateIndentedExpr(ee);
                    Positions.Add(te, Range);
                    Body.Add(te);
                }
                else
                {
                    var Range = new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + LineText.Length) };
                    var Spans = ParseTemplateSpans(LineText, Range, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, EnableEmbeddedExpr, nm, Positions);
                    var te = TemplateExpr.CreateLine(Spans);
                    Positions.Add(te, Range);
                    Body.Add(te);
                }
            }

            return Body;
        }

        private static List<TemplateSpan> ParseTemplateSpans(String s, TextRange sRange, Regex InlineExpressionRegex, List<KeyValuePair<String, Regex>> FilterNameAndRegex, Dictionary<String, List<String>> FilterNameToParameters, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<object, TextRange> Positions)
        {
            var PreviousEndIndex = 0;
            var Spans = new List<TemplateSpan>();
            Action<int, int> AddLiteral = (StartIndex, Length) =>
            {
                var Literal = s.Substring(StartIndex, Length);
                var ts = TemplateSpan.CreateLiteral(Literal);
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                Positions.Add(ts, Range);
                Spans.Add(ts);
            };
            Action<String, List<KeyValuePair<int, int>>, int, int> AddFilter = (FilterName, ParameterRanges, FilterStartIndex, FilterLength) =>
            {
                var FilterSpans = new List<List<TemplateSpan>>();
                foreach (var pr in ParameterRanges)
                {
                    var StartIndex = pr.Key;
                    var Length = pr.Value;
                    var sParameter = s.Substring(StartIndex, Length);
                    var pRange = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                    var ParameterSpans = ParseTemplateSpans(sParameter, pRange, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, EnableEmbeddedExpr, nm, Positions);
                    FilterSpans.Add(ParameterSpans);
                }
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, FilterStartIndex), End = nm.Text.Calc(sRange.Start, FilterStartIndex + FilterLength) };
                var fe = new FilterExpr { Name = FilterName, Spans = FilterSpans };
                Positions.Add(fe, Range);
                var ts = TemplateSpan.CreateFilter(fe);
                Positions.Add(ts, Range);
                Spans.Add(ts);
            };
            Action<int, int> AddExpr = (StartIndex, Length) =>
            {
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                var Expr = ParseExprInLine(Range, EnableEmbeddedExpr, nm, Positions);
                var ts = TemplateSpan.CreateExpr(Expr);
                Positions.Add(ts, Range);
                Spans.Add(ts);
            };
            while (true)
            {
                var FirstMatch = Optional<Match>.Empty;
                var FirstMatchStartIndex = s.Length;
                var FirstIsInlineExpr = false;
                var FirstFilterName = Optional<String>.Empty;

                {
                    var m = InlineExpressionRegex.Match(s, PreviousEndIndex);
                    if (m.Success)
                    {
                        FirstMatch = m;
                        FirstMatchStartIndex = m.Index;
                        FirstIsInlineExpr = true;
                    }
                }
                foreach (var p in FilterNameAndRegex)
                {
                    var m = p.Value.Match(s, PreviousEndIndex);
                    if (m.Success && (m.Index < FirstMatchStartIndex))
                    {
                        FirstMatch = m;
                        FirstMatchStartIndex = m.Index;
                        FirstIsInlineExpr = false;
                        FirstFilterName = p.Key;
                    }
                }

                if (FirstMatch.OnNotHasValue)
                {
                    break;
                }
                {
                    var m = FirstMatch.Value;
                    if (m.Index > PreviousEndIndex)
                    {
                        AddLiteral(PreviousEndIndex, m.Index - PreviousEndIndex);
                    }
                    if (FirstIsInlineExpr)
                    {
                        var g = m.Groups["Expr"];
                        if (!g.Success)
                        {
                            var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, m.Index), End = nm.Text.Calc(sRange.Start, m.Index + m.Length) };
                            throw new InvalidSyntaxException("InvalidInlineExpressionRegexLackExpr", new FileTextRange { Text = nm.Text, Range = Range });
                        }
                        AddExpr(g.Index, g.Length);
                    }
                    else
                    {
                        var Parameters = FilterNameToParameters[FirstFilterName.Value];
                        var ParameterRanges = new List<KeyValuePair<int, int>>();
                        foreach (var p in Parameters)
                        {
                            var g = m.Groups[p];
                            if (!g.Success)
                            {
                                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, m.Index), End = nm.Text.Calc(sRange.Start, m.Index + m.Length) };
                                throw new InvalidSyntaxException("InvalidFilterRegexLackParameter", new FileTextRange { Text = nm.Text, Range = Range });
                            }
                            ParameterRanges.Add(new KeyValuePair<int, int>(g.Index, g.Length));
                        }
                        AddFilter(FirstFilterName.Value, ParameterRanges, m.Index, m.Length);
                    }
                    PreviousEndIndex = m.Index + m.Length;
                    if (PreviousEndIndex >= s.Length)
                    {
                        break;
                    }
                }
            }
            if (s.Length > PreviousEndIndex)
            {
                AddLiteral(PreviousEndIndex, s.Length - PreviousEndIndex);
            }

            Positions.Add(Spans, sRange);
            return Spans;
        }

        public static Expr ParseExprInLine(TextRange Range, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            if (EnableEmbeddedExpr)
            {
                var ee = EmbeddedExpr.CreateSpan(nm.Text.GetTextInLine(Range));
                Positions.Add(ee, Range);
                var l = new List<EmbeddedExpr> { ee };
                Positions.Add(l, Range);
                var e = Expr.CreateEmbedded(l);
                Positions.Add(e, Range);
                return e;
            }
            else
            {
                var NodePositions = new Dictionary<Object, TextRange>();
                var l = ParseExprInLineToNodes(Range, nm, NodePositions, Positions);
                ExprNode OuterNode;
                if (l.Count == 1)
                {
                    OuterNode = l.Single();
                }
                else
                {
                    var OuterUndetermined = new ExprNodeUndetermined { Nodes = l };
                    OuterNode = ExprNode.CreateUndetermined(OuterUndetermined);
                    NodePositions.Add(OuterUndetermined, Range);
                    NodePositions.Add(OuterNode, Range);
                }
                var e = ExprTransformer.Transform(OuterNode, nm.Text, NodePositions, Positions);
                return e;
            }
        }

        public static Expr ParseExprLines(List<TextLine> Lines, Optional<TextRange> Range, int LinesIndentSpace, Regex InlineExpressionRegex, List<KeyValuePair<String, Regex>> FilterNameAndRegex, Dictionary<String, List<String>> FilterNameToParameters, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            if (EnableEmbeddedExpr)
            {
                var Body = new List<EmbeddedExpr>();
                var LineQueue = new LinkedList<TextLine>(Lines);
                while (LineQueue.Count > 0)
                {
                    var Line = LineQueue.First.Value;
                    LineQueue.RemoveFirst();
                    var LineText = Line.Text.Substring(Math.Min(LinesIndentSpace, Line.Text.Length));
                    var Trimmed = LineText.Trim(' ');
                    var HasEnd = false;
                    if (Trimmed == "##")
                    {
                        var IndentSpace = LineText.TakeWhile(c => c == ' ').Count();
                        var HeadRange = Line.Range;
                        var IndentedExprLines = new LinkedList<TextLine>();
                        while (LineQueue.Count > 0)
                        {
                            var ChildLine = LineQueue.First.Value;
                            var ChildLineText = ChildLine.Text.Substring(Math.Min(LinesIndentSpace, ChildLine.Text.Length));
                            var SpaceCount = ChildLineText.TakeWhile(c => c == ' ').Count();
                            if (TokenParser.IsBlankLine(ChildLineText))
                            {
                                IndentedExprLines.AddLast(ChildLine);
                                LineQueue.RemoveFirst();
                            }
                            else if ((SpaceCount == IndentSpace) && (ChildLineText.Substring(SpaceCount) == "$End"))
                            {
                                LineQueue.RemoveFirst();
                                HasEnd = true;
                                break;
                            }
                            else if (SpaceCount <= IndentSpace)
                            {
                                break;
                            }
                            else if (SpaceCount >= IndentSpace + 4)
                            {
                                IndentedExprLines.AddLast(ChildLine);
                                LineQueue.RemoveFirst();
                            }
                            else
                            {
                                throw new InvalidSyntaxException("InvalidIndent", new FileTextRange { Text = nm.Text, Range = ChildLine.Range });
                            }
                        }
                        if (!HasEnd)
                        {
                            while (IndentedExprLines.Count > 0)
                            {
                                var ChildLine = IndentedExprLines.Last.Value;
                                var ChildLineText = ChildLine.Text.Substring(Math.Min(LinesIndentSpace, ChildLine.Text.Length));
                                var SpaceCount = ChildLineText.TakeWhile(c => c == ' ').Count();
                                if (TokenParser.IsBlankLine(ChildLineText))
                                {
                                    LineQueue.AddFirst(ChildLine);
                                    IndentedExprLines.RemoveLast();
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        var Children = ParseTemplateBody(IndentedExprLines.ToList(), LinesIndentSpace + IndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, EnableEmbeddedExpr, nm, Positions);
                        var te = Expr.CreateYieldTemplate(Children);
                        var ChildrenRange = new TextRange { Start = HeadRange.Start, End = HeadRange.End };
                        if (IndentedExprLines.Count > 0)
                        {
                            ChildrenRange.End = IndentedExprLines.Last().Range.End;
                        }
                        Positions.Add(Children, ChildrenRange);
                        Positions.Add(te, ChildrenRange);
                        var ie = new IndentedExpr { IndentSpace = IndentSpace, Expr = te };
                        Positions.Add(ie, ChildrenRange);
                        var ee = EmbeddedExpr.CreateIndentedExpr(ie);
                        Positions.Add(ee, ChildrenRange);
                        Body.Add(ee);
                    }
                    else
                    {
                        var ee = EmbeddedExpr.CreateLine(LineText);
                        Positions.Add(ee, new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + LineText.Length) });
                        Body.Add(ee);
                    }
                }

                var e = Expr.CreateEmbedded(Body);
                if (Range.OnHasValue)
                {
                    Positions.Add(e, Range.Value);
                }
                return e;
            }
            else
            {
                var NodePositions = new Dictionary<Object, TextRange>();
                var l = ParseExprLinesToNodes(Lines, LinesIndentSpace, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, nm, NodePositions, Positions);
                ExprNode OuterNode;
                if (l.Count == 1)
                {
                    OuterNode = l.Single();
                }
                else
                {
                    var OuterStem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = l, CanMerge = false };
                    OuterNode = ExprNode.CreateStem(OuterStem);
                    if (Range.OnHasValue)
                    {
                        Positions.Add(l, Range.Value);
                        Positions.Add(OuterStem, Range.Value);
                        Positions.Add(OuterNode, Range.Value);
                    }
                }
                var e = ExprTransformer.Transform(OuterNode, nm.Text, NodePositions, Positions);
                return e;
            }
        }

        public static List<ExprNode> ParseExprInLineToNodes(TextRange Range, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<ExprNode>();

            var TokenPositions = new Dictionary<Object, TextRange>();
            var sb = new SequenceBuilder();
            var Tokens = TokenParser.ReadTokensInLine(nm.Text, TokenPositions, Range);
            foreach (var t in Tokens)
            {
                sb.PushToken(t, nm.Text, TokenPositions, NodePositions);
            }

            if (sb.IsInParenthesis)
            {
                throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = nm.Text, Range = Range });
            }

            return sb.GetResult(nm.Text, TokenPositions, NodePositions);
        }

        public static List<ExprNode> ParseExprLinesToNodes(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, List<KeyValuePair<String, Regex>> FilterNameAndRegex, Dictionary<String, List<String>> FilterNameToParameters, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<ExprNode>();

            var TokenPositions = new Dictionary<Object, TextRange>();
            Func<Object, Firefly.Texting.TreeFormat.Optional<FileTextRange>> GetFileRange = Obj =>
            {
                if (TokenPositions.ContainsKey(Obj))
                {
                    return new FileTextRange { Text = nm.Text, Range = TokenPositions[Obj] };
                }
                else
                {
                    return Firefly.Texting.TreeFormat.Optional<FileTextRange>.Empty;
                }
            };

            var sb = new SequenceBuilder();
            var RangeStart = Firefly.Texting.TreeFormat.Optional<TextRange>.Empty;
            var RangeEnd = Firefly.Texting.TreeFormat.Optional<TextRange>.Empty;

            var LineQueue = new Queue<TextLine>(Lines);
            while (LineQueue.Count > 0)
            {
                var Line = LineQueue.Dequeue();
                RangeStart = Line.Range;
                RangeEnd = Line.Range;
                if (TokenParser.IsBlankLine(Line.Text))
                {
                    continue;
                }
                if (!TokenParser.IsExactFitIndentCount(Line.Text, LinesIndentSpace))
                {
                    throw new InvalidSyntaxException("InvalidIndent", new FileTextRange { Text = nm.Text, Range = Line.Range });
                }
                var LineText = Line.Text.Substring(Math.Min(LinesIndentSpace, Line.Text.Length));
                var Tokens = TokenParser.ReadTokensInLine(nm.Text, TokenPositions, Line.Range);
                foreach (var t in Tokens)
                {
                    sb.PushToken(t, nm.Text, TokenPositions, NodePositions);
                }

                var ChildLines = new List<TextLine>();
                var HasEnd = false;
                while (LineQueue.Count > 0)
                {
                    var NextLine = LineQueue.Peek();
                    if (!TokenParser.IsBlankLine(NextLine.Text) && !TokenParser.IsFitIndentCount(NextLine.Text, LinesIndentSpace + 4))
                    {
                        if (TokenParser.IsExactFitIndentCount(NextLine.Text, LinesIndentSpace) && (NextLine.Text.Trim(' ') == "$End"))
                        {
                            LineQueue.Dequeue();
                            HasEnd = true;
                        }
                        break;
                    }
                    ChildLines.Add(NextLine);
                    LineQueue.Dequeue();
                }
                if (!HasEnd)
                {
                    ChildLines = ChildLines.AsEnumerable().Reverse().SkipWhile(ChildLine => TokenParser.IsBlankLine(ChildLine.Text)).Reverse().ToList();
                }
                var ChildRangeStart = new TextRange { Start = Line.Range.End, End = Line.Range.End };
                if (ChildLines.Count > 0)
                {
                    ChildRangeStart = ChildLines.First().Range;
                    RangeEnd = ChildLines.Last().Range;
                }
                var ChildRange = ChildRangeStart;
                if (RangeEnd.OnHasValue)
                {
                    ChildRange = new TextRange { Start = ChildRangeStart.Start, End = RangeEnd.Value.End };
                }
                var Range = Optional<TextRange>.Empty;
                if (RangeStart.OnHasValue && RangeEnd.OnHasValue)
                {
                    Range = new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End };
                }

                var PreprocessDirectiveReduced = sb.TryReducePreprocessDirective((PreprocessDirectiveToken, Parameters) =>
                {
                    if (!PreprocessDirectiveToken.Type.OnPreprocessDirective) { throw new InvalidOperationException(); }
                    var PreprocessDirectiveName = PreprocessDirectiveToken.Type.PreprocessDirective;
                    if (PreprocessDirectiveName == "$Comment")
                    {
                        if (Parameters.Count != 0) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        return new List<ExprNode> { };
                    }
                    else if (PreprocessDirectiveName == "$String")
                    {
                        if (Parameters.Count != 0) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        var s = String.Join("\r\n", ChildLines.Select(ChildLine => ChildLine.Text.Substring(Math.Min(LinesIndentSpace + 4, ChildLine.Text.Length))));
                        var n = ExprNode.CreateLiteral(s);
                        if (Range.OnHasValue)
                        {
                            NodePositions.Add(n, Range.Value);
                        }
                        return new List<ExprNode> { n };
                    }
                    else if (PreprocessDirectiveName == "$List")
                    {
                        if (Parameters.Count != 1) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        var Head = Parameters.Single();
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, nm, NodePositions, Positions);
                        var ln = new List<ExprNode>();
                        foreach (var c in Children)
                        {
                            var Nodes = new List<ExprNode> { c };
                            var Stem = new ExprNodeStem { Head = Head, Nodes = Nodes, CanMerge = false };
                            var n = ExprNode.CreateStem(Stem);
                            if (NodePositions.ContainsKey(c))
                            {
                                var cRange = NodePositions[c];
                                NodePositions.Add(Nodes, cRange);
                                NodePositions.Add(Stem, cRange);
                                NodePositions.Add(n, cRange);
                            }
                            ln.Add(n);
                        }
                        var OuterStem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = ln, CanMerge = true };
                        var OuterNode = ExprNode.CreateStem(OuterStem);
                        if (Range.OnHasValue)
                        {
                            NodePositions.Add(ln, Range.Value);
                            NodePositions.Add(OuterStem, Range.Value);
                            NodePositions.Add(OuterNode, Range.Value);
                        }
                        return new List<ExprNode> { OuterNode };
                    }
                    else if (PreprocessDirectiveName == "$Table")
                    {
                        if (Parameters.Count < 2) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        var Head = Parameters.First();
                        var Fields = Parameters.Skip(1).ToList();
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, nm, NodePositions, Positions);
                        var ln = new List<ExprNode>();
                        foreach (var c in Children)
                        {
                            var FieldNodes = new List<ExprNode> { };
                            if (c.OnUndetermined)
                            {
                                FieldNodes = c.Undetermined.Nodes;
                            }
                            else
                            {
                                FieldNodes.Add(c);
                            }

                            if (FieldNodes.Count != Fields.Count)
                            {
                                throw new InvalidSyntaxException("InvalidFieldCount", GetFileRange(c));
                            }

                            var Nodes = new List<ExprNode> { };
                            foreach (var p in Fields.Zip(FieldNodes, (Field, FieldNode) => new { Field = Field, FieldNode = FieldNode }))
                            {
                                var Field = p.Field;
                                var FieldNode = p.FieldNode;
                                var fNodes = new List<ExprNode> { FieldNode };
                                var fStem = new ExprNodeStem { Head = Field, Nodes = fNodes, CanMerge = false };
                                var fn = ExprNode.CreateStem(fStem);
                                if (NodePositions.ContainsKey(FieldNode))
                                {
                                    var fRange = NodePositions[FieldNode];
                                    NodePositions.Add(fNodes, fRange);
                                    NodePositions.Add(fStem, fRange);
                                    NodePositions.Add(fn, fRange);
                                }
                                Nodes.Add(fn);
                            }
                            var Stem = new ExprNodeStem { Head = Head, Nodes = Nodes, CanMerge = false };
                            var n = ExprNode.CreateStem(Stem);
                            if (NodePositions.ContainsKey(c))
                            {
                                var cRange = NodePositions[c];
                                NodePositions.Add(Nodes, cRange);
                                NodePositions.Add(Stem, cRange);
                                NodePositions.Add(n, cRange);
                            }
                            ln.Add(n);
                        }
                        var OuterStem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = ln, CanMerge = true };
                        var OuterNode = ExprNode.CreateStem(OuterStem);
                        if (Range.OnHasValue)
                        {
                            NodePositions.Add(ln, Range.Value);
                            NodePositions.Add(OuterStem, Range.Value);
                            NodePositions.Add(OuterNode, Range.Value);
                        }
                        return new List<ExprNode> { OuterNode };
                    }
                    else if (PreprocessDirectiveName == "#")
                    {
                        if (Parameters.Count != 0) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        var Children = ParseTemplateBody(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, false, nm, Positions);
                        var OuterNode = ExprNode.CreateTemplate(Children);
                        if (Range.OnHasValue)
                        {
                            Positions.Add(Children, Range.Value);
                            Positions.Add(OuterNode, Range.Value);
                        }
                        return new List<ExprNode> { OuterNode };
                    }
                    else if (PreprocessDirectiveName == "##")
                    {
                        if (Parameters.Count != 0) { throw new InvalidSyntaxException("InvalidParameterCount", GetFileRange(PreprocessDirectiveToken)); }
                        var Children = ParseTemplateBody(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, false, nm, Positions);
                        var OuterNode = ExprNode.CreateYieldTemplate(Children);
                        if (Range.OnHasValue)
                        {
                            Positions.Add(Children, Range.Value);
                            Positions.Add(OuterNode, Range.Value);
                        }
                        return new List<ExprNode> { OuterNode };
                    }
                    else
                    {
                        throw new InvalidSyntaxException("UnknownPreprocessDirective", GetFileRange(PreprocessDirectiveToken));
                    }
                }, nm.Text, TokenPositions, NodePositions);

                if (!PreprocessDirectiveReduced)
                {
                    if ((ChildLines.Count > 0) || HasEnd || sb.IsCurrentLeftParenthesis)
                    {
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, FilterNameAndRegex, FilterNameToParameters, nm, NodePositions, Positions);
                        if (Children.Count == 1)
                        {
                            sb.PushNode(Children.Single());
                        }
                        else
                        {
                            var Stem = new ExprNodeStem { Head = Optional<ExprNode>.Empty, Nodes = Children, CanMerge = true };
                            var e = ExprNode.CreateStem(Stem);
                            NodePositions.Add(Children, ChildRange);
                            NodePositions.Add(Stem, ChildRange);
                            NodePositions.Add(e, ChildRange);
                            sb.PushNode(e);
                        }
                    }
                }

                if (!sb.IsInParenthesis)
                {
                    var Nodes = sb.GetResult(nm.Text, TokenPositions, NodePositions);
                    if (Nodes.Count == 1)
                    {
                        l.Add(Nodes.Single());
                    }
                    else
                    {
                        var Undetermined = new ExprNodeUndetermined { Nodes = Nodes };
                        var n = ExprNode.CreateUndetermined(Undetermined);
                        if (Range.OnHasValue)
                        {
                            NodePositions.Add(Nodes, Range.Value);
                            NodePositions.Add(Undetermined, Range.Value);
                            NodePositions.Add(n, Range.Value);
                        }
                        l.Add(n);
                    }

                    sb = new SequenceBuilder();
                }
            }

            if (sb.IsInParenthesis)
            {
                if (Lines.Count > 0)
                {
                    var Range = new TextRange { Start = Lines.First().Range.Start, End = Lines.Last().Range.End };
                    throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = nm.Text, Range = Range });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            return l;
        }
    }
}
