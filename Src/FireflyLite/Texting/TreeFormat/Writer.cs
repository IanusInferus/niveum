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
    public class TreeFormatWriter
    {
        private StreamWriter sw;
        public TreeFormatWriter(StreamWriter sw)
        {
            this.sw = sw;
        }

        private static readonly string Empty = "$Empty";
        private static readonly string StringDirective = "$String";
        private static readonly string EndDirective = "$End";
        private static readonly string List = "$List";

        public void Write(Forest Forest)
        {
            foreach (var n in Forest.Nodes)
            {
                WriteNode(0, n);
            }
        }

        private void WriteNode(int IndentLevel, Node Node)
        {
            var s = TryGetNodeAsSingleLineString(Node);
            if (s.OnSome)
            {
                WriteRaw(IndentLevel, s.Value);
                return;
            }

            switch (Node._Tag)
            {
                case NodeTag.Empty:
                    throw new InvalidOperationException();
                case NodeTag.Leaf:
                    WriteValue(IndentLevel, Node.Leaf);
                    break;
                case NodeTag.Stem:
                    var ns = Node.Stem;
                    var Name = GetLiteral(ns.Name, true).SingleLine;
                    WriteRaw(IndentLevel, Name);

                    if (ns.Children.Count == 0)
                    {
                        WriteRaw(IndentLevel, EndDirective);
                    }
                    else
                    {
                        if (ns.Children.Count > 1 && ns.Children.All(c => c.OnStem && c.Stem.Children.Count == 1))
                        {
                            var ChildNames = ns.Children.Select(c => c.Stem.Name).Distinct().ToList();
                            if (ChildNames.Count == 1)
                            {
                                var ChildName = GetLiteral(ChildNames.Single(), true).SingleLine;
                                WriteRaw(IndentLevel + 1, List + " " + ChildName);
                                foreach (var c in ns.Children)
                                {
                                    WriteNode(IndentLevel + 2, c.Stem.Children.Single());
                                }
                                return;
                            }
                        }

                        foreach (var c in ns.Children)
                        {
                            WriteNode(IndentLevel + 1, c);
                        }
                    }
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        private static Optional<string> TryGetNodeAsSingleLineString(Node Node)
        {
            var l = new List<string>();

            var n = Node;
            while (true)
            {
                switch (n._Tag)
                {
                    case NodeTag.Empty:
                        l.Add(Empty);
                        goto ExitWhile;
                    case NodeTag.Leaf:
                        {
                            var s = GetLiteral(n.Leaf, false);
                            if (s.OnMultiLine) return Optional<string>.Empty;
                            if (!s.OnSingleLine) throw new InvalidOperationException();
                            l.Add(s.SingleLine);
                            goto ExitWhile;
                        }
                    case NodeTag.Stem:
                        {
                            var ns = n.Stem;
                            if (ns.Children.Count != 1) return Optional<string>.Empty;
                            var s = GetLiteral(ns.Name, false);
                            if (s.OnMultiLine) return Optional<string>.Empty;
                            if (!s.OnSingleLine) throw new InvalidOperationException();
                            l.Add(s.SingleLine);
                            n = ns.Children.Single();
                            break;
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
            ExitWhile:

            return string.Join(" ", l);
        }

        private void WriteValue(int IndentLevel, string Value)
        {
            var s = GetLiteral(Value, false);

            switch (s._Tag)
            {
                case LiteralTag.SingleLine:
                    WriteRaw(IndentLevel, s.SingleLine);
                    break;
                case LiteralTag.MultiLine:
                    WriteRaw(IndentLevel, StringDirective);
                    foreach (var Line in s.MultiLine)
                    {
                        WriteRaw(IndentLevel + 1, Line);
                    }
                    if (s.MultiLine.Count > 0)
                    {
                        var LastLine = s.MultiLine.Last();
                        var Chars = LastLine.Distinct().ToList();
                        if (Chars.Count == 0 || (Chars.Count == 1 && Chars.Single() == ' '))
                        {
                            WriteRaw(IndentLevel, EndDirective);
                        }
                    }
                    break;
            }
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
