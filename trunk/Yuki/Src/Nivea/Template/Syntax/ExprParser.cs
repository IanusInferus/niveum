//==========================================================================
//
//  File:        ExprParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式解析器
//  Version:     2016.07.19.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Firefly.Texting.TreeFormat.Syntax;
using Nivea.Template.Semantics;

namespace Nivea.Template.Syntax
{
    public static class ExprParser
    {
        public static List<TemplateExpr> ParseTemplateBody(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, Regex InlineIdentifierRegex, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
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

                    var e = ParseExprLines(IndentedExprLines.ToList(), Range, LinesIndentSpace + IndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, EnableEmbeddedExpr, nm, Positions);
                    var ee = new IndentedExpr { IndentSpace = IndentSpace, Expr = e };

                    Positions.Add(ee, Range);
                    var te = TemplateExpr.CreateIndentedExpr(ee);
                    Positions.Add(te, Range);
                    Body.Add(te);
                }
                else
                {
                    var te = ParseTemplateLine(LineText, new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + LineText.Length) }, InlineExpressionRegex, InlineIdentifierRegex, EnableEmbeddedExpr, nm, Positions);
                    Body.Add(te);
                }
            }

            return Body;
        }

        private static TemplateExpr ParseTemplateLine(String s, TextRange sRange, Regex InlineExpressionRegex, Regex InlineIdentifierRegex, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<object, TextRange> Positions)
        {
            var Matches = InlineIdentifierRegex.Matches(s).Cast<Match>().ToList();
            var PreviousEndIndex = 0;
            var Spans = new List<TemplateSpan>();
            Action<int, int> AddNonIdentifier = (StartIndex, Length) =>
            {
                var sNonIdentifier = s.Substring(StartIndex, Length);
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                var IdentifierSpans = ParseTemplateSpans(sNonIdentifier, Range, InlineExpressionRegex, EnableEmbeddedExpr, nm, Positions);
                Spans.AddRange(IdentifierSpans);
            };
            Action<int, int> AddIdentifier = (StartIndex, Length) =>
            {
                var sIdentifier = s.Substring(StartIndex, Length);
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                var IdentifierSpans = ParseTemplateSpans(sIdentifier, Range, InlineExpressionRegex, EnableEmbeddedExpr, nm, Positions);
                var ts = TemplateSpan.CreateIdentifier(IdentifierSpans);
                Positions.Add(ts, Range);
                Spans.Add(ts);
            };
            foreach (var m in Matches)
            {
                if (m.Index > PreviousEndIndex)
                {
                    AddNonIdentifier(PreviousEndIndex, m.Index - PreviousEndIndex);
                }
                var g = m.Groups["Identifier"];
                if (!g.Success)
                {
                    var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, m.Index), End = nm.Text.Calc(sRange.Start, m.Index + m.Length) };
                    throw new InvalidSyntaxException("InvalidInlineIdentifierRegexLackIdentifier", new FileTextRange { Text = nm.Text, Range = Range });
                }
                AddIdentifier(g.Index, g.Length);
                PreviousEndIndex = m.Index + m.Length;
            }
            if (s.Length > PreviousEndIndex)
            {
                AddNonIdentifier(PreviousEndIndex, s.Length - PreviousEndIndex);
            }

            Positions.Add(Spans, sRange);
            var te = TemplateExpr.CreateLine(Spans);
            Positions.Add(te, sRange);
            return te;
        }
        private static List<TemplateSpan> ParseTemplateSpans(String s, TextRange sRange, Regex InlineExpressionRegex, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<object, TextRange> Positions)
        {
            var Matches = InlineExpressionRegex.Matches(s).Cast<Match>().ToList();
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
            Action<int, int> AddExpr = (StartIndex, Length) =>
            {
                var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, StartIndex), End = nm.Text.Calc(sRange.Start, StartIndex + Length) };
                var Expr = ParseExprInLine(Range, EnableEmbeddedExpr, nm, Positions);
                var ts = TemplateSpan.CreateExpr(Expr);
                Positions.Add(ts, Range);
                Spans.Add(ts);
            };
            foreach (var m in Matches)
            {
                if (m.Index > PreviousEndIndex)
                {
                    AddLiteral(PreviousEndIndex, m.Index - PreviousEndIndex);
                }
                var g = m.Groups["Expr"];
                if (!g.Success)
                {
                    var Range = new TextRange { Start = nm.Text.Calc(sRange.Start, m.Index), End = nm.Text.Calc(sRange.Start, m.Index + m.Length) };
                    throw new InvalidSyntaxException("InvalidInlineExpressionRegexLackExpr", new FileTextRange { Text = nm.Text, Range = Range });
                }
                AddExpr(g.Index, g.Length);
                PreviousEndIndex = m.Index + m.Length;
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

        public static Expr ParseExprLines(List<TextLine> Lines, Optional<TextRange> Range, int LinesIndentSpace, Regex InlineExpressionRegex, Regex InlineIdentifierRegex, bool EnableEmbeddedExpr, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
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
                    if ((Trimmed == "#") || (Trimmed == "##"))
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

                        var Children = ParseTemplateBody(IndentedExprLines.ToList(), LinesIndentSpace + IndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, EnableEmbeddedExpr, nm, Positions);
                        Expr te;
                        if (Trimmed == "#")
                        {
                            te = Expr.CreateTemplate(Children);
                        }
                        else
                        {
                            te = Expr.CreateYieldTemplate(Children);
                        }
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
                var l = ParseExprLinesToNodes(Lines, LinesIndentSpace, InlineExpressionRegex, InlineIdentifierRegex, nm, NodePositions, Positions);
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

        public static List<ExprNode> ParseExprLinesToNodes(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, Regex InlineIdentifierRegex, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> NodePositions, Dictionary<Object, TextRange> Positions)
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
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, nm, NodePositions, Positions);
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
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, nm, NodePositions, Positions);
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
                        var Children = ParseTemplateBody(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, false, nm, Positions);
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
                        var Children = ParseTemplateBody(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, false, nm, Positions);
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
                        var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, InlineIdentifierRegex, nm, NodePositions, Positions);
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
