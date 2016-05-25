//==========================================================================
//
//  File:        ExprParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 表达式解析器
//  Version:     2016.05.25.
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

namespace Nivea.Template.Syntax
{
    public static class ExprParser
    {
        public static List<TemplateExpr> ParseTemplateBody(IEnumerable<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var Body = new List<TemplateExpr>();

            var InIndent = false;
            var IndentSpace = 0;
            var HeadRange = Optional<TextRange>.Empty;
            var IndentedExprLines = new List<TextLine>();

            Action<TextLine, String> AddLine = (Line, LineText) =>
            {
                var Matches = InlineExpressionRegex.Matches(LineText).Cast<Match>().ToList();
                var PreviousEndIndex = 0;
                var Spans = new List<TemplateSpan>();
                Action<int, int> AddLiteral = (StartIndex, Length) =>
                {
                    var Literal = LineText.Substring(StartIndex, Length);
                    var ts = TemplateSpan.CreateLiteral(Literal);
                    var Range = new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + StartIndex), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + StartIndex + Length) };
                    Positions.Add(ts, Range);
                    Spans.Add(ts);
                };
                Action<int, int> AddExpr = (StartIndex, Length) =>
                {
                    var Range = new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + StartIndex), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + StartIndex + Length) };
                    var Expr = ParseExprInLine(Range, nm, Positions);
                    Positions.Add(Expr, Range);
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
                        var Range = new TextRange { Start = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + m.Index), End = nm.Text.Calc(Line.Range.Start, LinesIndentSpace + m.Index + m.Length) };
                        throw new InvalidSyntaxException("InvalidInlineExpressionRegexLackExpr", new FileTextRange { Text = nm.Text, Range = Range });
                    }
                    AddExpr(g.Index, g.Length);
                    PreviousEndIndex = m.Index + m.Length;
                }
                if (LineText.Length > PreviousEndIndex)
                {
                    AddLiteral(PreviousEndIndex, LineText.Length - PreviousEndIndex);
                }

                var te = TemplateExpr.CreateLine(Spans);
                Positions.Add(te, Line.Range);
                Body.Add(te);
            };

            Action AddIndentedExpr = () =>
            {
                var e = ParseExprLines(IndentedExprLines, LinesIndentSpace + IndentSpace + 4, InlineExpressionRegex, nm, Positions);
                var ee = new IndentedExpr { IndentSpace = IndentSpace, Expr = e };

                var Range = new TextRange { Start = HeadRange.Value.Start, End = HeadRange.Value.End };
                if (IndentedExprLines.Count > 0)
                {
                    Range.End = IndentedExprLines.Last().Range.End;
                }
                Positions.Add(e, Range);
                Positions.Add(ee, Range);
                var te = TemplateExpr.CreateIndentedExpr(ee);
                Positions.Add(te, Range);
                Body.Add(te);
            };

            foreach (var Line in Lines)
            {
                var LineText = Line.Text.Substring(Math.Min(LinesIndentSpace, Line.Text.Length));
                if (!InIndent)
                {
                    var Trimmed = LineText.Trim(' ');
                    if (Trimmed == "$$")
                    {
                        InIndent = true;
                        IndentSpace = LineText.TakeWhile(c => c == ' ').Count();
                        HeadRange = Line.Range;
                        IndentedExprLines = new List<TextLine>();
                    }
                    else
                    {
                        AddLine(Line, LineText);
                    }
                }
                else
                {
                    var SpaceCount = LineText.TakeWhile(c => c == ' ').Count();
                    if (TokenParser.IsBlankLine(LineText))
                    {
                        IndentedExprLines.Add(Line);
                    }
                    else if (SpaceCount < IndentSpace)
                    {
                        AddIndentedExpr();
                        InIndent = false;
                        AddLine(Line, LineText);
                    }
                    else if ((SpaceCount == IndentSpace) && (LineText.Substring(SpaceCount) == "$End"))
                    {
                        AddIndentedExpr();
                        InIndent = false;
                    }
                    else if (SpaceCount >= IndentSpace + 4)
                    {
                        IndentedExprLines.Add(Line);
                    }
                    else
                    {
                        throw new InvalidSyntaxException("InvalidIndent", new FileTextRange { Text = nm.Text, Range = Line.Range });
                    }
                }
            }

            if (InIndent)
            {
                AddIndentedExpr();
                InIndent = false;
            }

            return Body;
        }

        public static Expr ParseExprInLine(TextRange Range, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var l = ParseExprInLineToNodes(Range, nm, Positions);
            //TODO
            return Expr.CreateNull();
        }

        public static Expr ParseExprLines(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var l = ParseExprLinesToNodes(Lines, LinesIndentSpace, InlineExpressionRegex, nm, Positions);
            //TODO
            return Expr.CreateNull();
        }

        public static List<ExprNode> ParseExprInLineToNodes(TextRange Range, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<ExprNode>();

            var sb = new SequenceBuilder();
            var Tokens = TokenParser.ReadTokensInLine(nm.Text, Positions, Range);
            foreach (var t in Tokens)
            {
                sb.PushToken(t, nm, Positions);
            }

            if (sb.IsInParenthesis)
            {
                throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = nm.Text, Range = Range });
            }

            return sb.GetResult(nm, Positions);
        }

        public static List<ExprNode> ParseExprLinesToNodes(List<TextLine> Lines, int LinesIndentSpace, Regex InlineExpressionRegex, ISemanticsNodeMaker nm, Dictionary<Object, TextRange> Positions)
        {
            var l = new List<ExprNode>();

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
                    if (sb.IsInParenthesis)
                    {
                        throw new InvalidSyntaxException("InvalidParenthesis", new FileTextRange { Text = nm.Text, Range = Line.Range });
                    }
                    continue;
                }
                if (!TokenParser.IsExactFitIndentCount(Line.Text, LinesIndentSpace))
                {
                    throw new InvalidSyntaxException("InvalidIndent", new FileTextRange { Text = nm.Text, Range = Line.Range });
                }
                var LineText = Line.Text.Substring(Math.Min(LinesIndentSpace, Line.Text.Length));
                var Tokens = TokenParser.ReadTokensInLine(nm.Text, Positions, Line.Range);
                foreach (var t in Tokens)
                {
                    sb.PushToken(t, nm, Positions);
                }

                var ChildLines = new List<TextLine>();
                while (LineQueue.Count > 0)
                {
                    var NextLine = LineQueue.Peek();
                    if (!TokenParser.IsFitIndentCount(NextLine.Text, LinesIndentSpace + 4))
                    {
                        if (TokenParser.IsExactFitIndentCount(NextLine.Text, LinesIndentSpace) && (NextLine.Text.Trim(' ') == "$End"))
                        {
                            LineQueue.Dequeue();
                        }
                        break;
                    }
                    ChildLines.Add(NextLine);
                    LineQueue.Dequeue();
                }

                var PreprocessDirectiveReduced = sb.TryReducePreprocessDirective((PreprocessDirectiveToken, Parameters) =>
                {
                    //TODO
                    return new List<ExprNode> { ExprNode.CreateLiteral(PreprocessDirectiveToken.ToString()) };
                }, nm, Positions);

                if (!PreprocessDirectiveReduced)
                {
                    var Children = ParseExprLinesToNodes(ChildLines, LinesIndentSpace + 4, InlineExpressionRegex, nm, Positions);
                    foreach (var c in Children)
                    {
                        sb.PushNode(c);
                    }
                }

                if (!sb.IsInParenthesis)
                {
                    if (ChildLines.Count > 0)
                    {
                        RangeEnd = ChildLines.Last().Range;
                    }
                    var Undetermined = new ExprNodeUndetermined { Nodes = sb.GetResult(nm, Positions) };
                    var n = ExprNode.CreateUndetermined(Undetermined);
                    if (RangeStart.OnHasValue && RangeEnd.OnHasValue)
                    {
                        var Range = new TextRange { Start = RangeStart.Value.Start, End = RangeEnd.Value.End };
                        Positions.Add(Undetermined, Range);
                        Positions.Add(n, Range);
                    }
                    l.Add(n);

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
