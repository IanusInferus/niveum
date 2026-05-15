using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat.Syntax;
using static Firefly.TextEncoding.ControlChars;

namespace Firefly.Texting.TreeFormat
{
    public class TreeFormatParseSetting
    {
        public Func<string, bool> IsTreeParameterFunction = Name => false;
        public Func<string, bool> IsTableParameterFunction = Name => false;
        public Func<string, bool> IsTreeContentFunction = Name => false;
        public Func<string, bool> IsTableContentFunction = Name => false;
    }

    public class TreeFormatParseResult
    {
        public Forest Value;
        public Text Text;

        /// <summary>Token | SyntaxRule => Range</summary>
        public Dictionary<object, TextRange> Positions;

        /// <summary>SingleLineFunctionNode | FunctionNodes => RawFunctionCall</summary>
        public Dictionary<object, RawFunctionCall> RawFunctionCalls;
    }

    public class TreeFormatSyntaxParser
    {
        public Text Text { get; set; }

        private Func<string, bool> IsTreeParameterFunction = f => false;
        private Func<string, bool> IsTableParameterFunction = f => false;
        private Func<string, bool> IsTreeContentFunction = f => false;
        private Func<string, bool> IsTableContentFunction = f => false;

        private Dictionary<object, TextRange> Positions = new Dictionary<object, TextRange>();
        private Dictionary<object, FileTextRange> FilePositions = new Dictionary<object, FileTextRange>();
        private Dictionary<object, RawFunctionCall> RawFunctionCalls = new Dictionary<object, RawFunctionCall>();

        public TreeFormatSyntaxParser(Text Text)
            : this(new TreeFormatParseSetting(), Text)
        {
        }
        public TreeFormatSyntaxParser(TreeFormatParseSetting Setting, Text Text)
        {
            this.Text = Text;
            this.IsTreeParameterFunction = Setting.IsTreeParameterFunction;
            this.IsTableParameterFunction = Setting.IsTableParameterFunction;
            this.IsTreeContentFunction = Setting.IsTreeContentFunction;
            this.IsTableContentFunction = Setting.IsTableContentFunction;
        }

        public TreeFormatParseResult Parse()
        {
            var Lines = new TextLineRange { StartRow = 1, EndRow = Text.Lines.Count + 1 };
            var MultiNodesList = ParseMultiNodesList(Lines, 0);
            var Forest = Mark(new Forest { MultiNodesList = MultiNodesList }, Lines);
            return new TreeFormatParseResult { Value = Forest, Text = Text, Positions = Positions, RawFunctionCalls = RawFunctionCalls };
        }

        private Optional<TextRange> GetRange(object Obj)
        {
            if (!Positions.ContainsKey(Obj)) return Optional<TextRange>.Empty;
            return Positions[Obj];
        }
        private Optional<FileTextRange> GetFileRange(object Obj)
        {
            if (FilePositions.ContainsKey(Obj)) return FilePositions[Obj];
            if (!Positions.ContainsKey(Obj)) return Optional<FileTextRange>.Empty;
            var fp = new FileTextRange { Text = Text, Range = Positions[Obj] };
            FilePositions.Add(Obj, fp);
            return fp;
        }
        private T Mark<T>(T Obj, Optional<TextRange> Range)
        {
            if (Range.OnSome)
            {
                Positions.Add(Obj, Range.Value);
            }
            return Obj;
        }
        private T Mark<T>(T Obj, TextLineRange Range)
        {
            var Start = Text.GetTextLine(Range.StartRow).Range.Start;
            TextPosition End;
            if (Range.EndRow >= Text.Lines.Count)
            {
                End = Text.GetTextLine(Range.EndRow - 1).Range.End;
            }
            else
            {
                End = Text.GetTextLine(Range.EndRow).Range.Start;
            }
            return Mark(Obj, new TextRange { Start = Start, End = End });
        }

        private List<MultiNodes> ParseMultiNodesList(TextLineRange Lines, int IndentLevel)
        {
            var l = new List<MultiNodes>();
            Optional<TextLineRange> RemainingLines = Lines;
            while (RemainingLines.OnSome)
            {
                var Result = ReadMultiNodes(RemainingLines.Value, IndentLevel);
                if (Result.MultiNodes.OnSome) l.Add(Result.MultiNodes.Value);
                RemainingLines = Result.RemainingLines;
            }
            return l;
        }

        private class MultiNodesParseResult
        {
            public Optional<MultiNodes> MultiNodes;
            public Optional<TextLineRange> RemainingLines;
        }
        private MultiNodesParseResult ReadMultiNodes(TextLineRange Lines, int IndentLevel)
        {
            var NullNode = Optional<MultiNodes>.Empty;
            var NullRemainingLines = Optional<TextLineRange>.Empty;

            var FirstLineIndex = Lines.StartRow;
            while (true)
            {
                if (FirstLineIndex >= Lines.EndRow) return new MultiNodesParseResult { MultiNodes = NullNode, RemainingLines = NullRemainingLines };
                if (!TreeFormatTokenParser.IsBlankLine(Text.GetTextLine(FirstLineIndex))) break;
                FirstLineIndex += 1;
            }

            var FirstLine = Text.GetTextLine(FirstLineIndex);
            if (!TreeFormatTokenParser.IsExactFitIndentLevel(FirstLine, IndentLevel)) throw new InvalidTokenException("InvaildIndentLevel", new FileTextRange { Text = Text, Range = FirstLine.Range }, FirstLine.Text);

            Optional<TextLineRange> ChildLines = Optional<TextLineRange>.Empty;
            var ChildStartLineIndex = FirstLineIndex + 1;
            var ChildEndLineIndex = ChildStartLineIndex;
            while (true)
            {
                if (ChildEndLineIndex >= Lines.EndRow)
                {
                    if (ChildEndLineIndex > ChildStartLineIndex)
                    {
                        ChildLines = new TextLineRange { StartRow = ChildStartLineIndex, EndRow = ChildEndLineIndex };
                    }
                    else
                    {
                        ChildLines = new TextLineRange { StartRow = ChildStartLineIndex, EndRow = ChildStartLineIndex };
                    }
                    break;
                }
                var ChildCurrentLine = Text.GetTextLine(ChildEndLineIndex);
                if (!TreeFormatTokenParser.IsBlankLine(ChildCurrentLine))
                {
                    if (TreeFormatTokenParser.IsExactFitIndentLevel(ChildCurrentLine, IndentLevel))
                    {
                        ChildLines = new TextLineRange { StartRow = ChildStartLineIndex, EndRow = ChildEndLineIndex };
                        break;
                    }
                    if (!TreeFormatTokenParser.IsFitIndentLevel(ChildCurrentLine, IndentLevel + 1))
                    {
                        throw new InvalidTokenException("InvaildIndentLevel", new FileTextRange { Text = Text, Range = ChildCurrentLine.Range }, ChildCurrentLine.Text);
                    }
                }
                ChildEndLineIndex += 1;
            }
            if (!ChildLines.OnSome) throw new InvalidOperationException();

            var EndLineIndex = ChildEndLineIndex;
            var EndLine = Optional<TextLine>.Empty;

            //如果最后有$End预处理指令，则将其包含到
            if (ChildEndLineIndex < Lines.EndRow)
            {
                var CurrentLine = Text.GetTextLine(ChildEndLineIndex);
                var FirstToken = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentLine.Range);
                if (FirstToken.OnSome)
                {
                    if (FirstToken.Value.Token.OnPreprocessDirective)
                    {
                        if (FirstToken.Value.Token.PreprocessDirective == "End")
                        {
                            EndLineIndex += 1;
                            EndLine = CurrentLine;
                        }
                    }
                }
            }

            //获得剩余行数
            Optional<TextLineRange> RemainingLines;
            if (EndLineIndex >= Lines.EndRow)
            {
                RemainingLines = NullRemainingLines;
            }
            else
            {
                RemainingLines = new TextLineRange { StartRow = EndLineIndex, EndRow = Lines.EndRow };
            }

            var MultiNodesLines = new TextLineRange { StartRow = FirstLineIndex, EndRow = EndLineIndex };
            var MultiNodes = ParseMultiNodes(MultiNodesLines, FirstLine, ChildLines.Value, EndLine, IndentLevel);
            return new MultiNodesParseResult { MultiNodes = MultiNodes, RemainingLines = RemainingLines };
        }
        private MultiNodes ParseMultiNodes(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel)
        {
            var FirstTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, FirstLine.Range);
            if (!FirstTokenResult.OnSome) throw new InvalidOperationException();
            var FirstToken = FirstTokenResult.Value.Token;
            var RemainingChars = FirstTokenResult.Value.RemainingChars;

            switch (FirstToken._Tag)
            {
                case TokenTag.SingleLineLiteral:
                case TokenTag.LeftParenthesis:
                case TokenTag.RightParenthesis:
                case TokenTag.SingleLineComment:
                    {
                        var Node = ParseNode(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars);
                        return Mark(MultiNodes.CreateNode(Node), Lines);
                    }
                case TokenTag.PreprocessDirective:
                    switch (FirstToken.PreprocessDirective)
                    {
                        case "List":
                            {
                                var ListNodes = ParseListNodes(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars);
                                return Mark(MultiNodes.CreateListNodes(ListNodes), Lines);
                            }
                        case "Table":
                            {
                                var TableNodes = ParseTableNodes(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars);
                                return Mark(MultiNodes.CreateTableNodes(TableNodes), Lines);
                            }
                        default:
                            {
                                var Node = ParseNode(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars);
                                return Mark(MultiNodes.CreateNode(Node), Lines);
                            }
                    }
                case TokenTag.FunctionDirective:
                    {
                        var FunctionNodes = ParseFunctionNodes(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars);
                        return Mark(MultiNodes.CreateFunctionNodes(FunctionNodes), Lines);
                    }
                default:
                    throw new InvalidOperationException();
            }
        }
        private Node ParseNode(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FirstTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, FirstLine.Range);
            if (!FirstTokenResult.OnSome) throw new InvalidOperationException();
            var FirstTokenRange = GetRange(FirstToken);

            switch (FirstToken._Tag)
            {
                case TokenTag.SingleLineLiteral:
                    if (!EndLine.OnSome && (ChildLines.StartRow >= ChildLines.EndRow || Text.GetLines(ChildLines).All(Line => TreeFormatTokenParser.IsBlankLine(Line))))
                    {
                        return Mark(Node.CreateSingleLineNodeLine(ParseSingleLineNodeLine(FirstLine, FirstToken, RemainingChars)), FirstLine.Range);
                    }
                    return Mark(Node.CreateMultiLineNode(ParseMultiLineNode(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars)), Lines);
                case TokenTag.LeftParenthesis:
                case TokenTag.RightParenthesis:
                    return Mark(Node.CreateSingleLineNodeLine(ParseSingleLineNodeLine(FirstLine, FirstToken, RemainingChars)), FirstLine.Range);
                case TokenTag.PreprocessDirective:
                    switch (FirstToken.PreprocessDirective)
                    {
                        case "String":
                            return Mark(Node.CreateMultiLineLiteral(ParseMultiLineLiteral(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars)), Lines);
                        case "Comment":
                            return Mark(Node.CreateMultiLineComment(ParseMultiLineComment(Lines, FirstLine, ChildLines, EndLine, IndentLevel, FirstToken, RemainingChars)), Lines);
                        case "Empty":
                            if (EndLine.OnSome || (ChildLines.StartRow < ChildLines.EndRow && Text.GetLines(ChildLines).Any(Line => !TreeFormatTokenParser.IsBlankLine(Line))))
                            {
                                throw new InvalidSyntaxRuleException("InvalidEmptyDirective", GetFileRange(FirstToken), FirstToken);
                            }
                            return Mark(Node.CreateSingleLineNodeLine(ParseSingleLineNodeLine(FirstLine, FirstToken, RemainingChars)), FirstLine.Range);
                        default:
                            throw new InvalidSyntaxRuleException("InvalidPreprocessDirective", GetFileRange(FirstToken), FirstToken);
                    }
                case TokenTag.FunctionDirective:
                    throw new InvalidOperationException();
                case TokenTag.SingleLineComment:
                    {
                        if (FirstTokenResult.Value.RemainingChars.OnSome) throw new InvalidOperationException();
                        var FreeContent = Mark(new FreeContent { Text = FirstToken.SingleLineComment }, FirstTokenRange);
                        var SingleLineComment = Mark(new SingleLineComment { Content = FreeContent }, FirstTokenRange);
                        return Mark(Node.CreateSingleLineComment(SingleLineComment), FirstTokenRange);
                    }
                default:
                    throw new InvalidOperationException();
            }
        }
        private ListNodes ParseListNodes(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            if (!RemainingChars.OnSome) throw new InvalidSyntaxRuleException("ListChildHeadNotExist", GetFileRange(FirstToken), FirstToken);
            var SecondTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
            if (!SecondTokenResult.OnSome) throw new InvalidSyntaxRuleException("ListChildHeadNotExist", GetFileRange(FirstToken), FirstToken);
            var SecondToken = SecondTokenResult.Value.Token;
            if (!SecondToken.OnSingleLineLiteral) throw new InvalidSyntaxRuleException("ListChildHeadExpected", GetFileRange(SecondToken), SecondToken);
            var ChildHead = Mark(new SingleLineLiteral { Text = SecondToken.SingleLineLiteral }, GetRange(SecondToken));
            var SingleLineComment = ParseSingleLineComment(SecondTokenResult.Value.RemainingChars);
            var Children = ParseMultiNodesList(ChildLines, IndentLevel + 1);
            var EndDirective = ParseEndDirective(EndLine);
            return Mark(new ListNodes { ChildHead = ChildHead, SingleLineComment = SingleLineComment, Children = Children, EndDirective = EndDirective }, Lines);
        }
        private TableNodes ParseTableNodes(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            if (!RemainingChars.OnSome) throw new InvalidSyntaxRuleException("TableChildHeadNotExist", GetFileRange(FirstToken), FirstToken);
            var SecondTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
            if (!SecondTokenResult.OnSome) throw new InvalidSyntaxRuleException("TableChildHeadNotExist", GetFileRange(FirstToken), FirstToken);
            var SecondToken = SecondTokenResult.Value.Token;
            if (!SecondToken.OnSingleLineLiteral) throw new InvalidSyntaxRuleException("TableChildHeadExpected", GetFileRange(SecondToken), SecondToken);
            var ChildHead = Mark(new SingleLineLiteral { Text = SecondToken.SingleLineLiteral }, GetRange(SecondToken));
            var ChildFields = new List<SingleLineLiteral>();
            Optional<TextRange> CurrentRemainingChars = SecondTokenResult.Value.RemainingChars;
            var l = new List<SingleLineLiteral>();
            while (CurrentRemainingChars.OnSome)
            {
                var ChildHeadResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingChars.Value);
                if (!ChildHeadResult.OnSome)
                {
                    CurrentRemainingChars = ChildHeadResult.Value.RemainingChars;
                    break;
                }
                var ChildHeadToken = ChildHeadResult.Value.Token;
                switch (ChildHeadToken._Tag)
                {
                    case TokenTag.SingleLineLiteral:
                        {
                            var FieldHead = Mark(new SingleLineLiteral { Text = ChildHeadToken.SingleLineLiteral }, GetRange(ChildHeadToken));
                            ChildFields.Add(FieldHead);
                            CurrentRemainingChars = ChildHeadResult.Value.RemainingChars;
                            break;
                        }
                    case TokenTag.SingleLineComment:
                        goto ExitWhile;
                    default:
                        throw new InvalidSyntaxRuleException("SingleLineLiteralOrSingleLineCommentExpected", GetFileRange(ChildHeadToken), ChildHeadToken);
                }
            }
            ExitWhile:
            var SingleLineComment = ParseSingleLineComment(CurrentRemainingChars);
            var Children = new List<TableLine>();
            foreach (var Line in Text.GetLines(ChildLines))
            {
                var OptTableLine = ParseTableLine(Line);
                if (OptTableLine.OnSome)
                {
                    Children.Add(OptTableLine.Value);
                }
            }
            var EndDirective = ParseEndDirective(EndLine);
            return Mark(new TableNodes { ChildHead = ChildHead, ChildFields = ChildFields, SingleLineComment = SingleLineComment, Children = Children, EndDirective = EndDirective }, Lines);
        }
        private FunctionNodes ParseFunctionNodes(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FunctionDirective = Mark(new FunctionDirective { Text = FirstToken.FunctionDirective }, GetRange(FirstToken));
            var l = new List<Token>();
            var ParametersStart = Optional<TextPosition>.Empty;
            if (RemainingChars.OnSome)
            {
                ParametersStart = RemainingChars.Value.Start;
            }
            else
            {
                var TokenRange = GetRange(FirstToken);
                if (TokenRange.OnSome)
                {
                    ParametersStart = TokenRange.Value.End;
                }
            }
            var ParameterEnd = ParametersStart;
            Optional<TextRange> CurrentRemainingChars = RemainingChars;
            var Level = 0;
            while (CurrentRemainingChars.OnSome)
            {
                var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingChars.Value);
                if (!TokenResult.OnSome)
                {
                    CurrentRemainingChars = Optional<TextRange>.Empty;
                    break;
                }
                var Token = TokenResult.Value.Token;
                switch (Token._Tag)
                {
                    case TokenTag.SingleLineLiteral:
                    case TokenTag.PreprocessDirective:
                    case TokenTag.FunctionDirective:
                        {
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.LeftParenthesis:
                        {
                            Level += 1;
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.RightParenthesis:
                        {
                            if (Level == 0) goto ExitWhile;
                            Level -= 1;
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.SingleLineComment:
                        goto ExitWhile;
                    default:
                        throw new InvalidOperationException();
                }
                CurrentRemainingChars = TokenResult.Value.RemainingChars;
            }
            ExitWhile:
            var ParameterRange = Optional<TextRange>.Empty;
            if (ParametersStart.OnSome && ParameterEnd.OnSome)
            {
                ParameterRange = new TextRange { Start = ParametersStart.Value, End = ParameterEnd.Value };
            }
            var SingleLineComment = ParseSingleLineComment(CurrentRemainingChars);
            var EndDirective = ParseEndDirective(EndLine);
            FunctionContent Content;
            if (EndDirective.OnSome)
            {
                Content = Mark(new FunctionContent { Lines = Text.GetLines(ChildLines).ToList(), IndentLevel = IndentLevel + 1 }, ChildLines);
            }
            else
            {
                var StartRow = ChildLines.StartRow;
                var EndRow = ChildLines.EndRow;
                while (EndRow > StartRow && Text.Lines[EndRow - 2].Text.Where(c => c != ' ').Count() == 0)
                {
                    EndRow -= 1;
                }
                var cl = new TextLineRange { StartRow = StartRow, EndRow = EndRow };
                Content = Mark(new FunctionContent { Lines = Text.GetLines(cl).ToList(), IndentLevel = IndentLevel + 1 }, cl);
            }
            var F = Mark(new FunctionNodes { FunctionDirective = FunctionDirective, Parameters = l, SingleLineComment = SingleLineComment, Content = Content, EndDirective = EndDirective }, Lines);

            RawFunctionCallParameters RawFunctionCallParameters;
            if (IsTreeParameterFunction(FunctionDirective.Text))
            {
                if (!RemainingChars.OnSome)
                {
                    RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(Optional<SingleLineNode>.Empty), ParameterRange);
                }
                else
                {
                    var SecondTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
                    if (!SecondTokenResult.OnSome)
                    {
                        RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(Optional<SingleLineNode>.Empty), ParameterRange);
                    }
                    else
                    {
                        var SecondToken = SecondTokenResult.Value.Token;
                        var SingleLineNodeResult = ParseSingleLineNode(SecondToken, SecondTokenResult.Value.RemainingChars);
                        var SingleLineNode = SingleLineNodeResult.Value;
                        RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(SingleLineNode), ParameterRange);
                    }
                }
            }
            else if (IsTableParameterFunction(FunctionDirective.Text))
            {
                var Nodes = new List<TableLineNode>();
                Optional<TextRange> CurrentRemainingCharsInTable = RemainingChars;
                while (CurrentRemainingCharsInTable.OnSome)
                {
                    var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingCharsInTable.Value);
                    if (!TokenResult.OnSome) break;
                    var Token = TokenResult.Value.Token;
                    if (Token.OnSingleLineComment)
                    {
                        break;
                    }
                    var TableLineNodeResult = ParseTableLineNode(Token, TokenResult.Value.RemainingChars);
                    Nodes.Add(TableLineNodeResult.Value);
                    CurrentRemainingCharsInTable = TableLineNodeResult.RemainingChars;
                }
                RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTableParameters(Nodes), ParameterRange);
            }
            else
            {
                RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTokenParameters(F.Parameters), ParameterRange);
            }

            RawFunctionCallContent RawFunctionCallContent;
            if (IsTreeContentFunction(FunctionDirective.Text))
            {
                var MultiNodesList = ParseMultiNodesList(ChildLines, IndentLevel + 1);
                RawFunctionCallContent = Mark(Syntax.RawFunctionCallContent.CreateTreeContent(MultiNodesList), ChildLines);
            }
            else if (IsTableContentFunction(FunctionDirective.Text))
            {
                var Children = new List<TableLine>();
                foreach (var Line in Text.GetLines(ChildLines))
                {
                    var OptTableLine = ParseTableLine(Line);
                    if (OptTableLine.OnSome)
                    {
                        Children.Add(OptTableLine.Value);
                    }
                }
                RawFunctionCallContent = Mark(Syntax.RawFunctionCallContent.CreateTableContent(Children), ChildLines);
            }
            else
            {
                RawFunctionCallContent = Mark(Syntax.RawFunctionCallContent.CreateLineContent(Content), ChildLines);
            }

            RawFunctionCalls.Add(F, Mark(new RawFunctionCall { Name = FunctionDirective, ReturnValueMode = FunctionCallReturnValueMode.MultipleNodes, Parameters = RawFunctionCallParameters, Content = RawFunctionCallContent }, Lines));

            return F;
        }

        private MultiLineNode ParseMultiLineNode(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var Head = Mark(new SingleLineLiteral { Text = FirstToken.SingleLineLiteral }, GetRange(FirstToken));
            var SingleLineComment = ParseSingleLineComment(RemainingChars);
            var Children = ParseMultiNodesList(ChildLines, IndentLevel + 1);
            var EndDirective = ParseEndDirective(EndLine);
            return Mark(new MultiLineNode { Head = Head, SingleLineComment = SingleLineComment, Children = Children, EndDirective = EndDirective }, Lines);
        }
        private MultiLineLiteral ParseMultiLineLiteral(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var SingleLineComment = ParseSingleLineComment(RemainingChars);
            var ContentLines = Text.GetLines(ChildLines).Select(cl => new string(cl.Text.Skip((IndentLevel + 1) * 4).ToArray()));
            var EndDirective = ParseEndDirective(EndLine);
            string ContentString;
            if (EndDirective.OnSome)
            {
                ContentString = string.Join(CrLf, ContentLines);
            }
            else
            {
                ContentString = string.Join(CrLf, ContentLines.Reverse().SkipWhile(Line => Line == "").Reverse());
            }
            var Content = Mark(new FreeContent { Text = ContentString }, ChildLines);
            return Mark(new MultiLineLiteral { SingleLineComment = SingleLineComment, Content = Content, EndDirective = EndDirective }, Lines);
        }
        private MultiLineComment ParseMultiLineComment(TextLineRange Lines, TextLine FirstLine, TextLineRange ChildLines, Optional<TextLine> EndLine, int IndentLevel, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var SingleLineComment = ParseSingleLineComment(RemainingChars);
            var ContentLines = Text.GetLines(ChildLines).Select(cl => new string(cl.Text.Skip((IndentLevel + 1) * 4).ToArray()));
            var EndDirective = ParseEndDirective(EndLine);
            string ContentString;
            if (EndDirective.OnSome)
            {
                ContentString = string.Join(CrLf, ContentLines);
            }
            else
            {
                ContentString = string.Join(CrLf, ContentLines.Reverse().SkipWhile(Line => Line == "").Reverse());
            }
            var Content = Mark(new FreeContent { Text = ContentString }, ChildLines);
            return Mark(new MultiLineComment { SingleLineComment = SingleLineComment, Content = Content, EndDirective = EndDirective }, Lines);
        }

        private Optional<TableLine> ParseTableLine(TextLine Line)
        {
            if (TreeFormatTokenParser.IsBlankLine(Line)) return Optional<TableLine>.Empty;
            var Nodes = new List<TableLineNode>();
            var SingleLineComment = Optional<SingleLineComment>.Empty;
            Optional<TextRange> CurrentRemainingChars = Line.Range;
            while (CurrentRemainingChars.OnSome)
            {
                var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingChars.Value);
                if (!TokenResult.OnSome) break;
                var Token = TokenResult.Value.Token;
                if (Token.OnSingleLineComment)
                {
                    SingleLineComment = ParseSingleLineComment(CurrentRemainingChars);
                    break;
                }
                var TableLineNodeResult = ParseTableLineNode(Token, TokenResult.Value.RemainingChars);
                Nodes.Add(TableLineNodeResult.Value);
                CurrentRemainingChars = TableLineNodeResult.RemainingChars;
            }
            return Mark(new TableLine { Nodes = Nodes, SingleLineComment = SingleLineComment }, Line.Range);
        }

        private SingleLineNodeLine ParseSingleLineNodeLine(TextLine Line, Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var SingleLineNodeResult = ParseSingleLineNode(FirstToken, RemainingChars);
            var SingleLineNode = SingleLineNodeResult.Value;
            var SingleLineComment = ParseSingleLineComment(SingleLineNodeResult.RemainingChars);
            return Mark(new SingleLineNodeLine { SingleLineNode = SingleLineNode, SingleLineComment = SingleLineComment }, Line.Range);
        }

        private class SyntaxParseResult<T>
        {
            public T Value;
            public Optional<TextRange> RemainingChars;
        }
        private SyntaxParseResult<SingleLineNode> ParseSingleLineNode(Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FirstTokenRange = GetRange(FirstToken);
            var NodeStartRange = FirstTokenRange;
            var NodeEndRange = FirstTokenRange;
            Func<Optional<TextRange>> CreateRange = () => (NodeStartRange.OnSome && NodeEndRange.OnSome) ? new TextRange { Start = NodeStartRange.Value.Start, End = NodeEndRange.Value.End } : Optional<TextRange>.Empty;

            switch (FirstToken._Tag)
            {
                case TokenTag.SingleLineLiteral:
                    {
                        var Head = Mark(new SingleLineLiteral { Text = FirstToken.SingleLineLiteral }, FirstTokenRange);
                        if (!RemainingChars.OnSome)
                        {
                            return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineLiteral(Head), CreateRange()), RemainingChars = RemainingChars };
                        }
                        Optional<TextRange> CurrentRemainingChars = RemainingChars;
                        var l = new List<ParenthesisNode>();
                        while (true)
                        {
                            if (!CurrentRemainingChars.OnSome)
                            {
                                if (l.Count == 0)
                                {
                                    return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineLiteral(Head), CreateRange()), RemainingChars = CurrentRemainingChars };
                                }
                                else
                                {
                                    var Node = Mark(new SingleLineNodeWithParameters { Head = Head, Children = l, LastChild = Optional<SingleLineNode>.Empty }, CreateRange());
                                    return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineNodeWithParameters(Node), CreateRange()), RemainingChars = CurrentRemainingChars };
                                }
                            }
                            var FollowingTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingChars.Value);
                            if (!FollowingTokenResult.OnSome)
                            {
                                if (l.Count == 0)
                                {
                                    return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineLiteral(Head), CreateRange()), RemainingChars = Optional<TextRange>.Empty };
                                }
                                else
                                {
                                    var Node = Mark(new SingleLineNodeWithParameters { Head = Head, Children = l, LastChild = Optional<SingleLineNode>.Empty }, CreateRange());
                                    return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineNodeWithParameters(Node), CreateRange()), RemainingChars = Optional<TextRange>.Empty };
                                }
                            }
                            var FollowingToken = FollowingTokenResult.Value.Token;
                            switch (FollowingToken._Tag)
                            {
                                case TokenTag.SingleLineLiteral:
                                case TokenTag.PreprocessDirective:
                                case TokenTag.FunctionDirective:
                                    {
                                        var ChildResult = ParseSingleLineNode(FollowingToken, FollowingTokenResult.Value.RemainingChars);
                                        var Child = ChildResult.Value;
                                        NodeEndRange = GetRange(FollowingToken);
                                        var Node = Mark(new SingleLineNodeWithParameters { Head = Head, Children = l, LastChild = Child }, CreateRange());
                                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineNodeWithParameters(Node), CreateRange()), RemainingChars = ChildResult.RemainingChars };
                                    }
                                case TokenTag.LeftParenthesis:
                                    {
                                        var ChildResult = ParseParenthesisNode(FollowingToken, FollowingTokenResult.Value.RemainingChars);
                                        var Child = ChildResult.Value;
                                        l.Add(Child);
                                        CurrentRemainingChars = ChildResult.RemainingChars;
                                        NodeEndRange = GetRange(FollowingToken);
                                        break;
                                    }
                                case TokenTag.RightParenthesis:
                                case TokenTag.SingleLineComment:
                                    if (l.Count == 0)
                                    {
                                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineLiteral(Head), CreateRange()), RemainingChars = CurrentRemainingChars };
                                    }
                                    else
                                    {
                                        var Node = Mark(new SingleLineNodeWithParameters { Head = Head, Children = l, LastChild = Optional<SingleLineNode>.Empty }, CreateRange());
                                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineNodeWithParameters(Node), CreateRange()), RemainingChars = CurrentRemainingChars };
                                    }
                                default:
                                    throw new InvalidOperationException();
                            }
                        }
                    }
                case TokenTag.LeftParenthesis:
                    {
                        var ParenthesisNodeResult = ParseParenthesisNode(FirstToken, RemainingChars);
                        var ParenthesisNode = ParenthesisNodeResult.Value;
                        NodeEndRange = GetRange(ParenthesisNode);
                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateParenthesisNode(ParenthesisNode), CreateRange()), RemainingChars = ParenthesisNodeResult.RemainingChars };
                    }
                case TokenTag.RightParenthesis:
                    throw new InvalidSyntaxRuleException("UnexpectedToken", GetFileRange(FirstToken), FirstToken);
                case TokenTag.PreprocessDirective:
                    if (FirstToken.PreprocessDirective == "Empty")
                    {
                        var EmptyNode = Mark(new EmptyNode(), FirstTokenRange);
                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateEmptyNode(EmptyNode), FirstTokenRange), RemainingChars = RemainingChars };
                    }
                    throw new InvalidSyntaxRuleException("InvalidPreprocessDirective", GetFileRange(FirstToken), FirstToken);
                case TokenTag.FunctionDirective:
                    {
                        var SingleLineFunctionNodeResult = ParseSingleLineFunctionNode(FirstToken, RemainingChars);
                        var SingleLineFunctionNode = SingleLineFunctionNodeResult.Value;
                        NodeEndRange = GetRange(SingleLineFunctionNode);
                        return new SyntaxParseResult<SingleLineNode> { Value = Mark(SingleLineNode.CreateSingleLineFunctionNode(SingleLineFunctionNode), CreateRange()), RemainingChars = SingleLineFunctionNodeResult.RemainingChars };
                    }
                case TokenTag.SingleLineComment:
                    throw new InvalidSyntaxRuleException("UnexpectedToken", GetFileRange(FirstToken), FirstToken);
                default:
                    throw new InvalidOperationException();
            }
        }

        private SyntaxParseResult<TableLineNode> ParseTableLineNode(Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FirstTokenRange = GetRange(FirstToken);
            var NodeStartRange = FirstTokenRange;
            var NodeEndRange = FirstTokenRange;
            Func<Optional<TextRange>> CreateRange = () => (NodeStartRange.OnSome && NodeEndRange.OnSome) ? new TextRange { Start = NodeStartRange.Value.Start, End = NodeEndRange.Value.End } : Optional<TextRange>.Empty;

            switch (FirstToken._Tag)
            {
                case TokenTag.SingleLineLiteral:
                    {
                        var Head = Mark(new SingleLineLiteral { Text = FirstToken.SingleLineLiteral }, GetRange(FirstToken));
                        return new SyntaxParseResult<TableLineNode> { Value = Mark(TableLineNode.CreateSingleLineLiteral(Head), CreateRange()), RemainingChars = RemainingChars };
                    }
                case TokenTag.LeftParenthesis:
                    {
                        var ParenthesisNodeResult = ParseParenthesisNode(FirstToken, RemainingChars);
                        var ParenthesisNode = ParenthesisNodeResult.Value;
                        return new SyntaxParseResult<TableLineNode> { Value = Mark(TableLineNode.CreateParenthesisNode(ParenthesisNode), CreateRange()), RemainingChars = ParenthesisNodeResult.RemainingChars };
                    }
                case TokenTag.RightParenthesis:
                    throw new InvalidSyntaxRuleException("UnexpectedToken", GetFileRange(FirstToken), FirstToken);
                case TokenTag.PreprocessDirective:
                    if (FirstToken.PreprocessDirective == "Empty")
                    {
                        var EmptyNode = Mark(new EmptyNode(), FirstTokenRange);
                        return new SyntaxParseResult<TableLineNode> { Value = Mark(TableLineNode.CreateEmptyNode(EmptyNode), FirstTokenRange), RemainingChars = RemainingChars };
                    }
                    throw new InvalidSyntaxRuleException("InvalidPreprocessDirective", GetFileRange(FirstToken), FirstToken);
                case TokenTag.FunctionDirective:
                    {
                        var SingleLineFunctionNodeResult = ParseSingleLineFunctionNode(FirstToken, RemainingChars);
                        var SingleLineFunctionNode = SingleLineFunctionNodeResult.Value;
                        return new SyntaxParseResult<TableLineNode> { Value = Mark(TableLineNode.CreateSingleLineFunctionNode(SingleLineFunctionNode), CreateRange()), RemainingChars = SingleLineFunctionNodeResult.RemainingChars };
                    }
                case TokenTag.SingleLineComment:
                    throw new InvalidSyntaxRuleException("UnexpectedToken", GetFileRange(FirstToken), FirstToken);
                default:
                    throw new InvalidOperationException();
            }
        }

        private SyntaxParseResult<SingleLineFunctionNode> ParseSingleLineFunctionNode(Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FirstTokenRange = GetRange(FirstToken);
            var NodeStartRange = FirstTokenRange;
            var NodeEndRange = FirstTokenRange;
            Func<Optional<TextRange>> CreateRange = () => (NodeStartRange.OnSome && NodeEndRange.OnSome) ? new TextRange { Start = NodeStartRange.Value.Start, End = NodeEndRange.Value.End } : Optional<TextRange>.Empty;

            var FunctionDirective = Mark(new FunctionDirective { Text = FirstToken.FunctionDirective }, GetRange(FirstToken));
            var l = new List<Token>();
            var ParametersStart = Optional<TextPosition>.Empty;
            if (RemainingChars.OnSome)
            {
                ParametersStart = RemainingChars.Value.Start;
            }
            else
            {
                var TokenRange = GetRange(FirstToken);
                if (TokenRange.OnSome)
                {
                    ParametersStart = TokenRange.Value.End;
                }
            }
            var ParameterEnd = ParametersStart;
            Optional<TextRange> CurrentRemainingChars = RemainingChars;
            var Level = 0;
            while (CurrentRemainingChars.OnSome)
            {
                var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingChars.Value);
                if (!TokenResult.OnSome)
                {
                    CurrentRemainingChars = Optional<TextRange>.Empty;
                    break;
                }
                var Token = TokenResult.Value.Token;
                switch (Token._Tag)
                {
                    case TokenTag.SingleLineLiteral:
                    case TokenTag.PreprocessDirective:
                    case TokenTag.FunctionDirective:
                        {
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.LeftParenthesis:
                        {
                            Level += 1;
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.RightParenthesis:
                        {
                            if (Level == 0) goto ExitWhile;
                            Level -= 1;
                            l.Add(Token);
                            var TokenRange = GetRange(Token);
                            if (TokenRange.OnSome)
                            {
                                ParametersStart = TokenRange.Value.End;
                            }
                            break;
                        }
                    case TokenTag.SingleLineComment:
                        goto ExitWhile;
                    default:
                        throw new InvalidOperationException();
                }
                CurrentRemainingChars = TokenResult.Value.RemainingChars;
            }
            ExitWhile:
            var ParameterRange = Optional<TextRange>.Empty;
            if (ParametersStart.OnSome && ParameterEnd.OnSome)
            {
                ParameterRange = new TextRange { Start = ParametersStart.Value, End = ParameterEnd.Value };
            }
            var FunctionRange = CreateRange();
            var F = Mark(new SingleLineFunctionNode { FunctionDirective = FunctionDirective, Parameters = l }, FunctionRange);

            RawFunctionCallParameters RawFunctionCallParameters;
            if (IsTreeParameterFunction(FunctionDirective.Text))
            {
                if (!RemainingChars.OnSome)
                {
                    RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(Optional<SingleLineNode>.Empty), ParameterRange);
                }
                else
                {
                    var SecondTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
                    if (!SecondTokenResult.OnSome)
                    {
                        RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(Optional<SingleLineNode>.Empty), ParameterRange);
                    }
                    else
                    {
                        var SecondToken = SecondTokenResult.Value.Token;
                        var SingleLineNodeResult = ParseSingleLineNode(SecondToken, SecondTokenResult.Value.RemainingChars);
                        var SingleLineNode = SingleLineNodeResult.Value;
                        RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTreeParameter(SingleLineNode), ParameterRange);
                    }
                }
            }
            else if (IsTableParameterFunction(FunctionDirective.Text))
            {
                var Nodes = new List<TableLineNode>();
                Optional<TextRange> CurrentRemainingCharsInTable = RemainingChars;
                while (CurrentRemainingCharsInTable.OnSome)
                {
                    var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, CurrentRemainingCharsInTable.Value);
                    if (!TokenResult.OnSome) break;
                    var Token = TokenResult.Value.Token;
                    if (Token.OnSingleLineComment)
                    {
                        break;
                    }
                    var TableLineNodeResult = ParseTableLineNode(Token, TokenResult.Value.RemainingChars);
                    Nodes.Add(TableLineNodeResult.Value);
                    CurrentRemainingCharsInTable = TableLineNodeResult.RemainingChars;
                }
                RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTableParameters(Nodes), ParameterRange);
            }
            else
            {
                RawFunctionCallParameters = Mark(Syntax.RawFunctionCallParameters.CreateTokenParameters(F.Parameters), ParameterRange);
            }

            RawFunctionCalls.Add(F, Mark(new RawFunctionCall { Name = FunctionDirective, ReturnValueMode = FunctionCallReturnValueMode.SingleNode, Parameters = RawFunctionCallParameters, Content = Optional<RawFunctionCallContent>.Empty }, FunctionRange));

            return new SyntaxParseResult<SingleLineFunctionNode> { Value = F, RemainingChars = CurrentRemainingChars };
        }

        private SyntaxParseResult<ParenthesisNode> ParseParenthesisNode(Token FirstToken, Optional<TextRange> RemainingChars)
        {
            var FirstTokenRange = GetRange(FirstToken);
            var NodeStartRange = FirstTokenRange;
            var NodeEndRange = FirstTokenRange;
            Func<Optional<TextRange>> CreateRange = () => (NodeStartRange.OnSome && NodeEndRange.OnSome) ? new TextRange { Start = NodeStartRange.Value.Start, End = NodeEndRange.Value.End } : Optional<TextRange>.Empty;

            if (!RemainingChars.OnSome) throw new InvalidSyntaxRuleException("ParenthesisNotMatched", GetFileRange(FirstToken), FirstToken);
            var SecondTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
            if (!SecondTokenResult.OnSome) throw new InvalidSyntaxRuleException("ParenthesisNotMatched", GetFileRange(FirstToken), FirstToken);
            var SecondToken = SecondTokenResult.Value.Token;
            NodeEndRange = GetRange(SecondToken);
            var SingleLineNodeResult = ParseSingleLineNode(SecondToken, SecondTokenResult.Value.RemainingChars);
            if (!SingleLineNodeResult.RemainingChars.OnSome) throw new InvalidSyntaxRuleException("ParenthesisNotMatched", new FileTextRange { Text = Text, Range = CreateRange() }, FirstToken);
            var SingleLineNode = SingleLineNodeResult.Value;
            NodeEndRange = GetRange(SingleLineNode);
            var EndTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, SingleLineNodeResult.RemainingChars.Value);
            if (!EndTokenResult.OnSome) throw new InvalidSyntaxRuleException("ParenthesisNotMatched", new FileTextRange { Text = Text, Range = CreateRange() }, FirstToken);
            var EndToken = EndTokenResult.Value.Token;
            if (!EndToken.OnRightParenthesis) throw new InvalidSyntaxRuleException("ParenthesisNotMatched", GetFileRange(EndToken), EndToken);
            NodeEndRange = GetRange(EndToken);
            return new SyntaxParseResult<ParenthesisNode> { Value = Mark(new ParenthesisNode { SingleLineNode = SingleLineNode }, CreateRange()), RemainingChars = EndTokenResult.Value.RemainingChars };
        }

        private Optional<SingleLineComment> ParseSingleLineComment(Optional<TextRange> RemainingChars)
        {
            if (!RemainingChars.OnSome) return Optional<SingleLineComment>.Empty;

            var TokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, RemainingChars.Value);
            if (!TokenResult.OnSome) return Optional<SingleLineComment>.Empty;

            if (TokenResult.Value.RemainingChars.OnSome) throw new InvalidTokenException("UnexpectedToken", new FileTextRange { Text = Text, Range = TokenResult.Value.RemainingChars.Value }, Text.GetTextInLine(TokenResult.Value.RemainingChars.Value));

            var Token = TokenResult.Value.Token;
            if (!Token.OnSingleLineComment) throw new InvalidSyntaxRuleException("UnexpectedToken", GetFileRange(Token), Token);
            var Content = Mark(new FreeContent { Text = Token.SingleLineComment }, RemainingChars.Value);
            return Mark(new SingleLineComment { Content = Content }, RemainingChars.Value);
        }
        private Optional<EndDirective> ParseEndDirective(Optional<TextLine> Line)
        {
            if (!Line.OnSome) return Optional<EndDirective>.Empty;
            var LineValue = Line.Value;
            var EndTokenResult = TreeFormatTokenParser.ReadToken(Text, Positions, LineValue.Range);
            if (!EndTokenResult.OnSome) throw new InvalidOperationException();
            if (!EndTokenResult.Value.Token.OnPreprocessDirective) throw new InvalidOperationException();
            if (EndTokenResult.Value.Token.PreprocessDirective != "End") throw new InvalidOperationException();
            var SingleLineComment = ParseSingleLineComment(EndTokenResult.Value.RemainingChars);
            return Mark(new EndDirective { EndSingleLineComment = SingleLineComment }, LineValue.Range);
        }
    }
}

namespace Firefly.Texting.TreeFormat.Syntax
{
    internal static class Collections
    {
        public static T BinarySearch<T>(this IList<T> l, Func<T, int> CompareWithKey)
        {
            var Min = 0;
            var Max = l.Count;
            while (Min < Max)
            {
                int Mid = (Max + Min) / 2;
                T MidItem = l[Mid];
                int Comp = CompareWithKey(MidItem);
                if (Comp < 0)
                {
                    Min = Mid + 1;
                }
                else if (Comp > 0)
                {
                    Max = Mid - 1;
                }
                else
                {
                    return MidItem;
                }
            }
            if (Min == Max && CompareWithKey(l[Min]) == 0)
            {
                return l[Min];
            }
            throw new InvalidOperationException();
        }

        public static T BinarySearch<T, TKey>(this IList<T> l, Func<T, TKey> KeySelector, TKey Key) where TKey : IComparable<TKey>
        {
            return BinarySearch(l, Left => KeySelector(Left).CompareTo(Key));
        }
    }

    public partial class Text
    {
        public TextLine GetTextLine(int Row)
        {
            return Lines[Row - 1];
        }
        public TextPosition GetPosition(int CharIndex)
        {
            var Line = Lines.BinarySearch(
                l =>
                {
                    if (l.Range.End.CharIndex < CharIndex) return -1;
                    if (l.Range.Start.CharIndex > CharIndex) return 1;
                    return 0;
                }
            );
            var ColumnIndex = CharIndex - Line.Range.Start.CharIndex;
            return new TextPosition { CharIndex = CharIndex, Row = Line.Range.Start.Row, Column = Line.Range.Start.Column + ColumnIndex };
        }
        public string GetTextInLine(TextRange Range)
        {
            if (Range.Start.Row != Range.End.Row) throw new ArgumentException();
            var Line = Lines[Range.Start.Row - 1];
            return Line.Text.Substring(Range.Start.Column - Line.Range.Start.Column, Range.End.Column - Range.Start.Column);
        }
        public TextPosition Calc(TextPosition p, int Offset)
        {
            var Line = Lines[p.Row - 1];
            var CharIndex = p.CharIndex + Offset;
            if (CharIndex >= Line.Range.Start.CharIndex && CharIndex <= Line.Range.End.CharIndex)
            {
                var ColumnIndex = p.CharIndex + Offset - Line.Range.Start.CharIndex;
                return new TextPosition { CharIndex = CharIndex, Row = Line.Range.Start.Row, Column = Line.Range.Start.Column + ColumnIndex };
            }
            return GetPosition(p.CharIndex + Offset);
        }
        public IEnumerable<TextLine> GetLines(TextLineRange Range)
        {
            return Lines.Skip(Range.StartRow - 1).Take(Range.EndRow - Range.StartRow);
        }
    }
}
