using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat.Syntax;
using static Firefly.TextEncoding.ControlChars;

namespace Firefly.Texting.TreeFormat
{
    public class TreeFormatSyntaxWriter
    {
        private StreamWriter sw;
        public TreeFormatSyntaxWriter(StreamWriter sw)
        {
            this.sw = sw;
        }

        private static readonly string Comment = "$Comment";
        private static readonly string Empty = "$Empty";
        private static readonly string StringDirective = "$String";
        private static readonly string EndDirective = "$End";
        private static readonly string List = "$List";
        private static readonly string Table = "$Table";

        public void Write(Forest Forest)
        {
            foreach (var mn in Forest.MultiNodesList)
            {
                WriteMultiNodes(0, mn);
            }
        }

        private void WriteMultiNodes(int IndentLevel, MultiNodes mn)
        {
            switch (mn._Tag)
            {
                case MultiNodesTag.Node:
                    WriteNode(IndentLevel, mn.Node);
                    break;
                case MultiNodesTag.ListNodes:
                    WriteListNodes(IndentLevel, mn.ListNodes);
                    break;
                case MultiNodesTag.TableNodes:
                    WriteTableNodes(IndentLevel, mn.TableNodes);
                    break;
                case MultiNodesTag.FunctionNodes:
                    WriteFunctionNodes(IndentLevel, mn.FunctionNodes);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private void WriteNode(int IndentLevel, Node n)
        {
            switch (n._Tag)
            {
                case NodeTag.SingleLineNodeLine:
                    WriteSingleLineNodeLine(IndentLevel, n.SingleLineNodeLine);
                    break;
                case NodeTag.MultiLineLiteral:
                    WriteMultiLineLiteral(IndentLevel, n.MultiLineLiteral);
                    break;
                case NodeTag.SingleLineComment:
                    WriteSingleLineComment(IndentLevel, n.SingleLineComment);
                    break;
                case NodeTag.MultiLineComment:
                    WriteMultiLineComment(IndentLevel, n.MultiLineComment);
                    break;
                case NodeTag.MultiLineNode:
                    WriteMultiLineNode(IndentLevel, n.MultiLineNode);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
        private void WriteListNodes(int IndentLevel, ListNodes ln)
        {
            var ChildHeadLiteral = GetLiteral(ln.ChildHead.Text, true).SingleLine;
            if (ln.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(ln.SingleLineComment.Value);
                WriteRaw(IndentLevel, List, ChildHeadLiteral, slc);
            }
            else
            {
                WriteRaw(IndentLevel, List, ChildHeadLiteral);
            }
            foreach (var mn in ln.Children)
            {
                WriteMultiNodes(IndentLevel + 1, mn);
            }
            WriteEndDirective(IndentLevel, ln.EndDirective);
        }
        private void WriteTableNodes(int IndentLevel, TableNodes tn)
        {
            var ChildHeadLiteral = GetLiteral(tn.ChildHead.Text, true).SingleLine;
            var ChildFields = Combine(tn.ChildFields.Select(f => GetSingleLineLiteral(f)).ToArray());
            if (tn.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(tn.SingleLineComment.Value);
                WriteRaw(IndentLevel, Table, ChildHeadLiteral, ChildFields, slc);
            }
            else
            {
                WriteRaw(IndentLevel, Table, ChildHeadLiteral, ChildFields);
            }
            var NumColumn = (new int[] { 0 }).Concat(tn.Children.Select(c => c.Nodes.Count)).Max() + 1;
            var DataTable = new List<string[]>();
            foreach (var tl in tn.Children)
            {
                var l = new List<string>();
                foreach (var n in tl.Nodes)
                {
                    l.Add(GetTableLineNode(n));
                }
                if (tl.SingleLineComment.OnSome)
                {
                    l.Add(GetSingleLineComment(tl.SingleLineComment.Value));
                }
                while (l.Count < NumColumn)
                {
                    l.Add("");
                }
                DataTable.Add(l.ToArray());
            }
            var DataLines = GetDataLines(DataTable.ToArray());
            foreach (var dl in DataLines)
            {
                WriteRaw(IndentLevel + 1, dl);
            }
            WriteEndDirective(IndentLevel, tn.EndDirective);
        }
        private string[] GetDataLines(string[][] DataTable)
        {
            if (DataTable.Length == 0) return new string[] { };

            var NumColumn = DataTable.Select(Row => Row.Length).Distinct().Single();
            var ColumnLength = Enumerable.Range(0, NumColumn).Select(i => ((new int[] { 0 }).Concat(DataTable.Select(Row => CalculateCharWidth(Row[i]))).Max() + 1).CeilToMultipleOf(4) + 4).ToArray();

            var NodeLines = new List<string>();
            foreach (var Row in DataTable)
            {
                NodeLines.Add(string.Join("", Row.Zip(ColumnLength, (v, l) => v + new string(' ', l - CalculateCharWidth(v)))));
            }
            return NodeLines.Select(Line => Line.TrimEnd(' ')).ToArray();
        }
        private int CalculateCharWidth(string s)
        {
            return s.ToUTF32().Select(c => c.IsHalfWidth() ? 1 : 2).Sum();
        }
        private void WriteFunctionNodes(int IndentLevel, FunctionNodes fn)
        {
            var Tokens = CombineTokens(fn.Parameters.Select(t => GetToken(t)).ToArray());
            if (fn.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(fn.SingleLineComment.Value);
                WriteRaw(IndentLevel, GetFunctionDirective(fn.FunctionDirective), Tokens, slc);
            }
            else
            {
                WriteRaw(IndentLevel, GetFunctionDirective(fn.FunctionDirective), Tokens);
            }
            var ContentIndentLevel = fn.Content.IndentLevel;
            foreach (var l in fn.Content.Lines)
            {
                if (l.Text.Take(ContentIndentLevel * 4).Where(c => c != ' ').Count() > 0)
                {
                    throw new ArgumentException(l.Text);
                }
                WriteRaw(IndentLevel + 1, new string(l.Text.Skip(ContentIndentLevel * 4).ToArray()));
            }
            WriteEndDirective(IndentLevel, fn.EndDirective);
        }

        private void WriteSingleLineNodeLine(int IndentLevel, SingleLineNodeLine slnl)
        {
            var sln = GetSingleLineNode(slnl.SingleLineNode);
            if (slnl.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(slnl.SingleLineComment.Value);
                WriteRaw(IndentLevel, sln, slc);
            }
            else
            {
                WriteRaw(IndentLevel, sln);
            }
        }
        private void WriteMultiLineLiteral(int IndentLevel, MultiLineLiteral mll)
        {
            if (mll.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(mll.SingleLineComment.Value);
                WriteRaw(IndentLevel, StringDirective, slc);
            }
            else
            {
                WriteRaw(IndentLevel, StringDirective);
            }
            var MultiLine = GetLiteral(mll.Content.Text, false, true).MultiLine;
            foreach (var Line in MultiLine)
            {
                WriteRaw(IndentLevel + 1, Line);
            }
            WriteEndDirective(IndentLevel, mll.EndDirective);
        }
        private void WriteSingleLineComment(int IndentLevel, SingleLineComment slc)
        {
            WriteRaw(IndentLevel, GetSingleLineComment(slc));
        }
        private void WriteMultiLineComment(int IndentLevel, MultiLineComment mlc)
        {
            if (mlc.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(mlc.SingleLineComment.Value);
                WriteRaw(IndentLevel, Comment, slc);
            }
            else
            {
                WriteRaw(IndentLevel, Comment);
            }
            var MultiLine = GetLiteral(mlc.Content.Text, false, true).MultiLine;
            foreach (var Line in MultiLine)
            {
                WriteRaw(IndentLevel + 1, Line);
            }
            WriteEndDirective(IndentLevel, mlc.EndDirective);
        }
        private void WriteMultiLineNode(int IndentLevel, MultiLineNode mln)
        {
            var HeadLiteral = GetLiteral(mln.Head.Text, true).SingleLine;
            if (mln.SingleLineComment.OnSome)
            {
                var slc = GetSingleLineComment(mln.SingleLineComment.Value);
                WriteRaw(IndentLevel, HeadLiteral, slc);
            }
            else
            {
                WriteRaw(IndentLevel, HeadLiteral);
            }
            foreach (var mn in mln.Children)
            {
                WriteMultiNodes(IndentLevel + 1, mn);
            }
            WriteEndDirective(IndentLevel, mln.EndDirective);
        }

        private string GetSingleLineNode(SingleLineNode sln)
        {
            switch (sln._Tag)
            {
                case SingleLineNodeTag.EmptyNode:
                    return GetEmptyNode(sln.EmptyNode);
                case SingleLineNodeTag.SingleLineFunctionNode:
                    return GetSingleLineFunctionNode(sln.SingleLineFunctionNode);
                case SingleLineNodeTag.SingleLineLiteral:
                    return GetSingleLineLiteral(sln.SingleLineLiteral);
                case SingleLineNodeTag.ParenthesisNode:
                    return GetParenthesisNode(sln.ParenthesisNode);
                case SingleLineNodeTag.SingleLineNodeWithParameters:
                    return GetSingleLineNodeWithParameters(sln.SingleLineNodeWithParameters);
                default:
                    throw new InvalidOperationException();
            }
        }
        private string GetTableLineNode(TableLineNode tln)
        {
            switch (tln._Tag)
            {
                case TableLineNodeTag.EmptyNode:
                    return GetEmptyNode(tln.EmptyNode);
                case TableLineNodeTag.SingleLineFunctionNode:
                    return GetSingleLineFunctionNode(tln.SingleLineFunctionNode);
                case TableLineNodeTag.SingleLineLiteral:
                    return GetSingleLineLiteral(tln.SingleLineLiteral);
                case TableLineNodeTag.ParenthesisNode:
                    return GetParenthesisNode(tln.ParenthesisNode);
                default:
                    throw new InvalidOperationException();
            }
        }

        private string GetEmptyNode(EmptyNode en)
        {
            return Empty;
        }
        private string GetSingleLineFunctionNode(SingleLineFunctionNode slfn)
        {
            var Tokens = CombineTokens(slfn.Parameters.Select(t => GetToken(t)).ToArray());
            return Combine(GetFunctionDirective(slfn.FunctionDirective), Tokens);
        }
        private string GetParenthesisNode(ParenthesisNode pn)
        {
            return "(" + GetSingleLineNode(pn.SingleLineNode) + ")";
        }
        private string GetSingleLineNodeWithParameters(SingleLineNodeWithParameters slnp)
        {
            var l = new List<string>();
            foreach (var c in slnp.Children)
            {
                l.Add(GetParenthesisNode(c));
            }
            if (slnp.LastChild.OnSome)
            {
                l.Add(GetSingleLineNode(slnp.LastChild.Value));
            }
            return Combine(GetSingleLineLiteral(slnp.Head), Combine(l.ToArray()));
        }

        private static string GetToken(Token t)
        {
            switch (t._Tag)
            {
                case TokenTag.SingleLineLiteral:
                    return GetSingleLineLiteral(t.SingleLineLiteral);
                case TokenTag.LeftParenthesis:
                    return "(";
                case TokenTag.RightParenthesis:
                    return ")";
                case TokenTag.PreprocessDirective:
                    return GetPreprocessDirective(t.PreprocessDirective);
                case TokenTag.FunctionDirective:
                    return GetFunctionDirective(t.FunctionDirective);
                case TokenTag.SingleLineComment:
                    return GetSingleLineComment(t.SingleLineComment);
                default:
                    throw new InvalidOperationException();
            }
        }

        private static string GetSingleLineLiteral(string sll)
        {
            return GetLiteral(sll, true).SingleLine;
        }
        private static string GetSingleLineLiteral(SingleLineLiteral sll)
        {
            return GetSingleLineLiteral(sll.Text);
        }

        private static string GetPreprocessDirective(string pd)
        {
            var s = GetLiteral(pd, true).SingleLine;
            if (s.Contains("\"")) throw new ArgumentException(pd);
            return "$" + s;
        }

        private static string GetFunctionDirective(string fd)
        {
            var s = GetLiteral(fd, true).SingleLine;
            if (s.Contains("\"")) throw new ArgumentException(fd);
            return "#" + s;
        }
        private static string GetFunctionDirective(FunctionDirective fd)
        {
            return GetFunctionDirective(fd.Text);
        }

        private static string GetSingleLineComment(string slc)
        {
            if (slc.Contains(Cr) || slc.Contains(Lf))
            {
                throw new InvalidOperationException();
            }
            return "//" + slc;
        }
        private static string GetSingleLineComment(SingleLineComment slc)
        {
            return GetSingleLineComment(slc.Content.Text);
        }

        private void WriteEndDirective(int IndentLevel, Optional<EndDirective> ed)
        {
            if (ed.OnSome)
            {
                if (ed.Value.EndSingleLineComment.OnSome)
                {
                    var eslc = GetSingleLineComment(ed.Value.EndSingleLineComment.Value);
                    WriteRaw(IndentLevel, EndDirective, eslc);
                }
                else
                {
                    WriteRaw(IndentLevel, EndDirective);
                }
            }
        }

        private string CombineTokens(params string[] Values)
        {
            //左括号的右边或右括号的左边没有空格

            if (Values.Length == 0) return "";

            var l = new List<string>();
            for (var k = 0; k <= Values.Length - 2; k += 1)
            {
                var a = Values[k];
                var b = Values[k + 1];
                l.Add(a);
                if (a == "(" || b == ")") continue;
                l.Add(" ");
            }
            l.Add(Values[Values.Length - 1]);

            return string.Join("", l);
        }
        private string Combine(params string[] Values)
        {
            return string.Join(" ", Values.Where(v => v != ""));
        }
        private void WriteRaw(int IndentLevel, string Value1, string Value2, params string[] Values)
        {
            WriteRaw(IndentLevel, Combine((new string[] { Value1, Value2 }).Concat(Values).ToArray()));
        }
        private void WriteRaw(int IndentLevel, string Value)
        {
            var si = new string(' ', 4 * IndentLevel);
            sw.WriteLine(si + Value);
        }

        private static Literal GetLiteral(string Value, bool MustSingleLine, bool MustMultiLine = false)
        {
            return TreeFormatLiteralWriter.GetLiteral(Value, MustSingleLine, MustMultiLine);
        }
    }
}
