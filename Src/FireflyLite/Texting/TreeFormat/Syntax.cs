using System;
using System.Collections.Generic;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.MetaSchema;

namespace Firefly.Texting.TreeFormat.Syntax
{
    [Record]
    [DebuggerDisplay("{ToString()}")]
    public struct TextPosition
    {
        public int CharIndex;
        public int Row;
        public int Column;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public struct TextRange
    {
        public TextPosition Start;
        public TextPosition End;

        public override string ToString()
        {
            return string.Format("({0}, {1})-({2}, {3})".Formats(Start.Row, Start.Column, End.Row, End.Column));
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public struct TextLine
    {
        public string Text;
        public TextRange Range;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public struct TextLineRange
    {
        public int StartRow;
        public int EndRow;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public partial class Text
    {
        public string Path;
        public List<TextLine> Lines;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class Forest
    {
        public List<MultiNodes> MultiNodesList;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum MultiNodesTag
    {
        Node,
        ListNodes,
        TableNodes,
        FunctionNodes
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class MultiNodes
    {
        [Tag] public MultiNodesTag _Tag;
        public Node Node;
        public ListNodes ListNodes;
        public TableNodes TableNodes;
        public FunctionNodes FunctionNodes;

        public static MultiNodes CreateNode(Node Value)
        {
            return new MultiNodes { _Tag = MultiNodesTag.Node, Node = Value };
        }
        public static MultiNodes CreateListNodes(ListNodes Value)
        {
            return new MultiNodes { _Tag = MultiNodesTag.ListNodes, ListNodes = Value };
        }
        public static MultiNodes CreateTableNodes(TableNodes Value)
        {
            return new MultiNodes { _Tag = MultiNodesTag.TableNodes, TableNodes = Value };
        }
        public static MultiNodes CreateFunctionNodes(FunctionNodes Value)
        {
            return new MultiNodes { _Tag = MultiNodesTag.FunctionNodes, FunctionNodes = Value };
        }

        public bool OnNode { get { return _Tag == MultiNodesTag.Node; } }
        public bool OnListNodes { get { return _Tag == MultiNodesTag.ListNodes; } }
        public bool OnTableNodes { get { return _Tag == MultiNodesTag.TableNodes; } }
        public bool OnFunctionNodes { get { return _Tag == MultiNodesTag.FunctionNodes; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum NodeTag
    {
        SingleLineNodeLine,
        MultiLineLiteral,
        SingleLineComment,
        MultiLineComment,
        MultiLineNode
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class Node
    {
        [Tag] public NodeTag _Tag;
        public SingleLineNodeLine SingleLineNodeLine;
        public MultiLineLiteral MultiLineLiteral;
        public SingleLineComment SingleLineComment;
        public MultiLineComment MultiLineComment;
        public MultiLineNode MultiLineNode;

        public static Node CreateSingleLineNodeLine(SingleLineNodeLine Value)
        {
            return new Node { _Tag = NodeTag.SingleLineNodeLine, SingleLineNodeLine = Value };
        }
        public static Node CreateMultiLineLiteral(MultiLineLiteral Value)
        {
            return new Node { _Tag = NodeTag.MultiLineLiteral, MultiLineLiteral = Value };
        }
        public static Node CreateSingleLineComment(SingleLineComment Value)
        {
            return new Node { _Tag = NodeTag.SingleLineComment, SingleLineComment = Value };
        }
        public static Node CreateMultiLineComment(MultiLineComment Value)
        {
            return new Node { _Tag = NodeTag.MultiLineComment, MultiLineComment = Value };
        }
        public static Node CreateMultiLineNode(MultiLineNode Value)
        {
            return new Node { _Tag = NodeTag.MultiLineNode, MultiLineNode = Value };
        }

        public bool OnSingleLineNodeLine { get { return _Tag == NodeTag.SingleLineNodeLine; } }
        public bool OnMultiLineLiteral { get { return _Tag == NodeTag.MultiLineLiteral; } }
        public bool OnSingleLineComment { get { return _Tag == NodeTag.SingleLineComment; } }
        public bool OnMultiLineComment { get { return _Tag == NodeTag.MultiLineComment; } }
        public bool OnMultiLineNode { get { return _Tag == NodeTag.MultiLineNode; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineNodeLine
    {
        public SingleLineNode SingleLineNode;
        public Optional<SingleLineComment> SingleLineComment;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum SingleLineNodeTag
    {
        EmptyNode,
        SingleLineFunctionNode,
        SingleLineLiteral,
        ParenthesisNode,
        SingleLineNodeWithParameters
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineNode
    {
        [Tag] public SingleLineNodeTag _Tag;
        public EmptyNode EmptyNode;
        public SingleLineFunctionNode SingleLineFunctionNode;
        public SingleLineLiteral SingleLineLiteral;
        public ParenthesisNode ParenthesisNode;
        public SingleLineNodeWithParameters SingleLineNodeWithParameters;

        public static SingleLineNode CreateEmptyNode(EmptyNode Value)
        {
            return new SingleLineNode { _Tag = SingleLineNodeTag.EmptyNode, EmptyNode = Value };
        }
        public static SingleLineNode CreateSingleLineFunctionNode(SingleLineFunctionNode Value)
        {
            return new SingleLineNode { _Tag = SingleLineNodeTag.SingleLineFunctionNode, SingleLineFunctionNode = Value };
        }
        public static SingleLineNode CreateSingleLineLiteral(SingleLineLiteral Value)
        {
            return new SingleLineNode { _Tag = SingleLineNodeTag.SingleLineLiteral, SingleLineLiteral = Value };
        }
        public static SingleLineNode CreateParenthesisNode(ParenthesisNode Value)
        {
            return new SingleLineNode { _Tag = SingleLineNodeTag.ParenthesisNode, ParenthesisNode = Value };
        }
        public static SingleLineNode CreateSingleLineNodeWithParameters(SingleLineNodeWithParameters Value)
        {
            return new SingleLineNode { _Tag = SingleLineNodeTag.SingleLineNodeWithParameters, SingleLineNodeWithParameters = Value };
        }

        public bool OnEmptyNode { get { return _Tag == SingleLineNodeTag.EmptyNode; } }
        public bool OnSingleLineFunctionNode { get { return _Tag == SingleLineNodeTag.SingleLineFunctionNode; } }
        public bool OnSingleLineLiteral { get { return _Tag == SingleLineNodeTag.SingleLineLiteral; } }
        public bool OnParenthesisNode { get { return _Tag == SingleLineNodeTag.ParenthesisNode; } }
        public bool OnSingleLineNodeWithParameters { get { return _Tag == SingleLineNodeTag.SingleLineNodeWithParameters; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineNodeWithParameters
    {
        public SingleLineLiteral Head;
        public List<ParenthesisNode> Children;
        public Optional<SingleLineNode> LastChild;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class MultiLineNode
    {
        public SingleLineLiteral Head;
        public Optional<SingleLineComment> SingleLineComment;
        public List<MultiNodes> Children;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class ParenthesisNode
    {
        public SingleLineNode SingleLineNode;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineComment
    {
        public FreeContent Content;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class MultiLineComment
    {
        public Optional<SingleLineComment> SingleLineComment;
        public FreeContent Content;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class EmptyNode
    {
        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineFunctionNode
    {
        public FunctionDirective FunctionDirective;
        public List<Token> Parameters;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class MultiLineLiteral
    {
        public Optional<SingleLineComment> SingleLineComment;
        public FreeContent Content;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class ListNodes
    {
        public SingleLineLiteral ChildHead;
        public Optional<SingleLineComment> SingleLineComment;
        public List<MultiNodes> Children;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class TableNodes
    {
        public SingleLineLiteral ChildHead;
        public List<SingleLineLiteral> ChildFields;
        public Optional<SingleLineComment> SingleLineComment;
        public List<TableLine> Children;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class TableLine
    {
        public List<TableLineNode> Nodes;
        public Optional<SingleLineComment> SingleLineComment;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum TableLineNodeTag
    {
        EmptyNode,
        SingleLineFunctionNode,
        SingleLineLiteral,
        ParenthesisNode
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class TableLineNode
    {
        [Tag] public TableLineNodeTag _Tag;
        public EmptyNode EmptyNode;
        public SingleLineFunctionNode SingleLineFunctionNode;
        public SingleLineLiteral SingleLineLiteral;
        public ParenthesisNode ParenthesisNode;

        public static TableLineNode CreateEmptyNode(EmptyNode Value)
        {
            return new TableLineNode { _Tag = TableLineNodeTag.EmptyNode, EmptyNode = Value };
        }
        public static TableLineNode CreateSingleLineFunctionNode(SingleLineFunctionNode Value)
        {
            return new TableLineNode { _Tag = TableLineNodeTag.SingleLineFunctionNode, SingleLineFunctionNode = Value };
        }
        public static TableLineNode CreateSingleLineLiteral(SingleLineLiteral Value)
        {
            return new TableLineNode { _Tag = TableLineNodeTag.SingleLineLiteral, SingleLineLiteral = Value };
        }
        public static TableLineNode CreateParenthesisNode(ParenthesisNode Value)
        {
            return new TableLineNode { _Tag = TableLineNodeTag.ParenthesisNode, ParenthesisNode = Value };
        }

        public bool OnEmptyNode { get { return _Tag == TableLineNodeTag.EmptyNode; } }
        public bool OnSingleLineFunctionNode { get { return _Tag == TableLineNodeTag.SingleLineFunctionNode; } }
        public bool OnSingleLineLiteral { get { return _Tag == TableLineNodeTag.SingleLineLiteral; } }
        public bool OnParenthesisNode { get { return _Tag == TableLineNodeTag.ParenthesisNode; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionNodes
    {
        public FunctionDirective FunctionDirective;
        public List<Token> Parameters;
        public Optional<SingleLineComment> SingleLineComment;
        public FunctionContent Content;
        public Optional<EndDirective> EndDirective;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class EndDirective
    {
        public Optional<SingleLineComment> EndSingleLineComment;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionDirective
    {
        public string Text;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum TokenTag
    {
        SingleLineLiteral,
        LeftParenthesis,
        RightParenthesis,
        PreprocessDirective,
        FunctionDirective,
        SingleLineComment
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class Token
    {
        [Tag] public TokenTag _Tag;
        public string SingleLineLiteral;
        public Unit LeftParenthesis;
        public Unit RightParenthesis;
        public string PreprocessDirective;
        public string FunctionDirective;
        public string SingleLineComment;

        public static Token CreateSingleLineLiteral(string Value)
        {
            return new Token { _Tag = TokenTag.SingleLineLiteral, SingleLineLiteral = Value };
        }
        public static Token CreateLeftParenthesis()
        {
            return new Token { _Tag = TokenTag.LeftParenthesis, LeftParenthesis = new Unit() };
        }
        public static Token CreateRightParenthesis()
        {
            return new Token { _Tag = TokenTag.RightParenthesis, RightParenthesis = new Unit() };
        }
        public static Token CreatePreprocessDirective(string Value)
        {
            return new Token { _Tag = TokenTag.PreprocessDirective, PreprocessDirective = Value };
        }
        public static Token CreateFunctionDirective(string Value)
        {
            return new Token { _Tag = TokenTag.FunctionDirective, FunctionDirective = Value };
        }
        public static Token CreateSingleLineComment(string Value)
        {
            return new Token { _Tag = TokenTag.SingleLineComment, SingleLineComment = Value };
        }

        public bool OnSingleLineLiteral { get { return _Tag == TokenTag.SingleLineLiteral; } }
        public bool OnLeftParenthesis { get { return _Tag == TokenTag.LeftParenthesis; } }
        public bool OnRightParenthesis { get { return _Tag == TokenTag.RightParenthesis; } }
        public bool OnPreprocessDirective { get { return _Tag == TokenTag.PreprocessDirective; } }
        public bool OnFunctionDirective { get { return _Tag == TokenTag.FunctionDirective; } }
        public bool OnSingleLineComment { get { return _Tag == TokenTag.SingleLineComment; } }

        public override string ToString()
        {
            switch (_Tag)
            {
                case TokenTag.SingleLineLiteral:
                    return SingleLineLiteral;
                case TokenTag.LeftParenthesis:
                    return "(";
                case TokenTag.RightParenthesis:
                    return ")";
                case TokenTag.PreprocessDirective:
                    return "$" + PreprocessDirective;
                case TokenTag.FunctionDirective:
                    return "#" + FunctionDirective;
                case TokenTag.SingleLineComment:
                    return "//" + SingleLineComment;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class SingleLineLiteral
    {
        public string Text;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FreeContent
    {
        public string Text;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionContent
    {
        public List<TextLine> Lines;
        public int IndentLevel;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class RawFunctionCall
    {
        public FunctionDirective Name;
        public FunctionCallReturnValueMode ReturnValueMode;
        public RawFunctionCallParameters Parameters;
        public Optional<RawFunctionCallContent> Content;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum RawFunctionCallParametersTag
    {
        TokenParameters,
        TreeParameter,
        TableParameters
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class RawFunctionCallParameters
    {
        [Tag] public RawFunctionCallParametersTag _Tag;
        public List<Token> TokenParameters;
        public Optional<SingleLineNode> TreeParameter;
        public List<TableLineNode> TableParameters;

        public static RawFunctionCallParameters CreateTokenParameters(List<Token> Value)
        {
            return new RawFunctionCallParameters { _Tag = RawFunctionCallParametersTag.TokenParameters, TokenParameters = Value };
        }
        public static RawFunctionCallParameters CreateTreeParameter(Optional<SingleLineNode> Value)
        {
            return new RawFunctionCallParameters { _Tag = RawFunctionCallParametersTag.TreeParameter, TreeParameter = Value };
        }
        public static RawFunctionCallParameters CreateTableParameters(List<TableLineNode> Value)
        {
            return new RawFunctionCallParameters { _Tag = RawFunctionCallParametersTag.TableParameters, TableParameters = Value };
        }

        public bool OnTokenParameters { get { return _Tag == RawFunctionCallParametersTag.TokenParameters; } }
        public bool OnTreeParameter { get { return _Tag == RawFunctionCallParametersTag.TreeParameter; } }
        public bool OnTableParameters { get { return _Tag == RawFunctionCallParametersTag.TableParameters; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum RawFunctionCallContentTag
    {
        LineContent,
        TreeContent,
        TableContent
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class RawFunctionCallContent
    {
        [Tag] public RawFunctionCallContentTag _Tag;
        public FunctionContent LineContent;
        public List<MultiNodes> TreeContent;
        public List<TableLine> TableContent;

        public static RawFunctionCallContent CreateLineContent(FunctionContent Value)
        {
            return new RawFunctionCallContent { _Tag = RawFunctionCallContentTag.LineContent, LineContent = Value };
        }
        public static RawFunctionCallContent CreateTreeContent(List<MultiNodes> Value)
        {
            return new RawFunctionCallContent { _Tag = RawFunctionCallContentTag.TreeContent, TreeContent = Value };
        }
        public static RawFunctionCallContent CreateTableContent(List<TableLine> Value)
        {
            return new RawFunctionCallContent { _Tag = RawFunctionCallContentTag.TableContent, TableContent = Value };
        }

        public bool OnLineContent { get { return _Tag == RawFunctionCallContentTag.LineContent; } }
        public bool OnTreeContent { get { return _Tag == RawFunctionCallContentTag.TreeContent; } }
        public bool OnTableContent { get { return _Tag == RawFunctionCallContentTag.TableContent; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum FunctionCallReturnValueMode
    {
        SingleNode,
        MultipleNodes
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionCall
    {
        public FunctionDirective Name;
        public FunctionCallReturnValueMode ReturnValueMode;
        public List<Semantics.Node> Parameters;
        public Optional<FunctionCallContent> Content;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum FunctionCallContentTag
    {
        LineContent,
        TreeContent,
        TableContent
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionCallContent
    {
        [Tag] public FunctionCallContentTag _Tag;
        public FunctionContent LineContent;
        public List<Semantics.Node> TreeContent;
        public List<FunctionCallTableLine> TableContent;

        public static FunctionCallContent CreateLineContent(FunctionContent Value)
        {
            return new FunctionCallContent { _Tag = FunctionCallContentTag.LineContent, LineContent = Value };
        }
        public static FunctionCallContent CreateTreeContent(List<Semantics.Node> Value)
        {
            return new FunctionCallContent { _Tag = FunctionCallContentTag.TreeContent, TreeContent = Value };
        }
        public static FunctionCallContent CreateTableContent(List<FunctionCallTableLine> Value)
        {
            return new FunctionCallContent { _Tag = FunctionCallContentTag.TableContent, TableContent = Value };
        }

        public bool OnLineContent { get { return _Tag == FunctionCallContentTag.LineContent; } }
        public bool OnTreeContent { get { return _Tag == FunctionCallContentTag.TreeContent; } }
        public bool OnTableContent { get { return _Tag == FunctionCallContentTag.TableContent; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class FunctionCallTableLine
    {
        public List<Semantics.Node> Nodes;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
}
