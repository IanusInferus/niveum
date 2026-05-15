using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat.Semantics;

namespace Firefly.Texting.TreeFormat
{
    public enum LiteralTag
    {
        SingleLine,
        MultiLine
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class Literal
    {
        [Tag] public LiteralTag _Tag;
        public string SingleLine;
        public List<string> MultiLine;

        public static Literal CreateSingleLine(string Value)
        {
            return new Literal { _Tag = LiteralTag.SingleLine, SingleLine = Value };
        }
        public static Literal CreateMultiLine(List<string> Value)
        {
            return new Literal { _Tag = LiteralTag.MultiLine, MultiLine = Value };
        }

        public bool OnSingleLine { get { return _Tag == LiteralTag.SingleLine; } }
        public bool OnMultiLine { get { return _Tag == LiteralTag.MultiLine; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public sealed class TreeFormatLiteralWriter
    {
        private TreeFormatLiteralWriter()
        {
        }

        private static readonly string Empty = "$Empty";
        private enum ParenthesisType
        {
            Angle,
            Bracket,
            Brace
        }
        private static Regex rCrLf = new Regex("\r\n", RegexOptions.ExplicitCapture);
        private static Regex rWildCrOrLf = new Regex("\r(?!\n)|(?<!\r)\n", RegexOptions.ExplicitCapture);
        private static Regex rForbiddenChars = new Regex("[()\f\t\v]", RegexOptions.ExplicitCapture);
        private static Regex rForbiddenHeadChars = new Regex("^[!%&;=?\\^`|~]|^//", RegexOptions.ExplicitCapture);
        public static Literal GetLiteral(string Value, bool MustSingleLine, bool MustMultiLine)
        {
            if (Value == null)
            {
                if (MustMultiLine)
                {
                    return Literal.CreateMultiLine(new List<string> { });
                }
                else
                {
                    return Literal.CreateSingleLine(Empty);
                }
            }
            if (Value == "")
            {
                if (MustMultiLine)
                {
                    return Literal.CreateMultiLine(new List<string> { "" });
                }
                else
                {
                    return Literal.CreateSingleLine("\"\"");
                }
            }
            if (MustMultiLine)
            {
                return Literal.CreateMultiLine(rCrLf.Split(Value.UnifyNewLineToCrLf()).ToList());
            }
            var wm = rWildCrOrLf.Match(Value).Success;
            var cm = rCrLf.Match(Value).Success;
            if (wm || (cm && MustSingleLine))
            {
                var s = Value.Escape().Replace("\"", "\\\"");
                if (s.StartsWith(" "))
                {
                    return Literal.CreateSingleLine("\"\"\\" + s + "\"\"");
                }
                else
                {
                    return Literal.CreateSingleLine("\"\"" + s + "\"\"");
                }
            }
            if (cm) return Literal.CreateMultiLine(rCrLf.Split(Value).ToList());

            Func<Literal> CreateQuotationLiteral = () => Literal.CreateSingleLine("\"" + Value.Replace("\"", "\"\"") + "\"");

            if (rForbiddenHeadChars.Match(Value).Success || rForbiddenChars.Match(Value).Success)
            {
                return CreateQuotationLiteral();
            }

            var Stack = new Stack<ParenthesisType>();
            foreach (var c in Value)
            {
                var cs = new string(c, 1);
                switch (cs)
                {
                    case "\"":
                    case " ":
                        if (Stack.Count == 0)
                        {
                            return CreateQuotationLiteral();
                        }
                        break;
                    case "<":
                        Stack.Push(ParenthesisType.Angle);
                        break;
                    case "[":
                        Stack.Push(ParenthesisType.Bracket);
                        break;
                    case "{":
                        Stack.Push(ParenthesisType.Brace);
                        break;
                    case ">":
                        if (Stack.Count == 0 || Stack.Peek() != ParenthesisType.Angle)
                        {
                            return CreateQuotationLiteral();
                        }
                        Stack.Pop();
                        break;
                    case "]":
                        if (Stack.Count == 0 || Stack.Peek() != ParenthesisType.Bracket)
                        {
                            return CreateQuotationLiteral();
                        }
                        Stack.Pop();
                        break;
                    case "}":
                        if (Stack.Count == 0 || Stack.Peek() != ParenthesisType.Brace)
                        {
                            return CreateQuotationLiteral();
                        }
                        Stack.Pop();
                        break;
                    default:
                        break;
                }
            }
            if (Stack.Count != 0) return CreateQuotationLiteral();

            return Literal.CreateSingleLine(Value);
        }
    }
}
