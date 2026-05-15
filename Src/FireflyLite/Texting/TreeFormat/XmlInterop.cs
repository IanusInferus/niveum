using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat.Semantics;
using Syntax = Firefly.Texting.TreeFormat.Syntax;

namespace Firefly.Texting.TreeFormat
{
    public sealed class XmlInterop
    {
        private XmlInterop()
        {
        }

        public static XElement TreeToXml(TreeFormatResult EvaluateResult)
        {
            var txt = new TreeToXmlTranslator(EvaluateResult);
            return txt.Translate();
        }
        public static XElement TreeToXml(Forest Tree)
        {
            var txt = new TreeToXmlTranslator(Tree);
            return txt.Translate();
        }

        public static TreeFormatResult XmlToTree(XElement x)
        {
            var xtt = new XmlToTreeTranslator(x);
            return xtt.Translate();
        }

        public static TreeFormatParseResult XmlToTreeRaw(XElement x)
        {
            var xtt = new XmlToTreeRawTranslator(x);
            return xtt.Translate();
        }

        private class TreeToXmlTranslator
        {
            private Forest Value;
            private Dictionary<object, Syntax.FileTextRange> Positions;

            public TreeToXmlTranslator(TreeFormatResult EvaluateResult)
            {
                Value = EvaluateResult.Value;
                Positions = EvaluateResult.Positions;
            }
            public TreeToXmlTranslator(Forest Value)
            {
                this.Value = Value;
                Positions = null;
            }

            public XElement Translate()
            {
                var i = GetFileLocationInformation(Value);

                if (Value.Nodes.Count != 1) throw new InvalidTextFormatException("NotTree", i ?? new FileLocationInformation());
                var Root = Value.Nodes.Single();
                if (!Root.OnStem) throw new InvalidTextFormatException("NotTree", i ?? new FileLocationInformation());

                var ns = Root.Stem;
                var n = TryGetXName(ns.Name, null);
                if (n == null) throw new InvalidTextFormatException("NamingError", i ?? new FileLocationInformation());
                var x = new XElementEx(n);
                if (i != null) x.SetLineInfo(i);
                FillElement(ns, x);
                return x;
            }

            private void FillElement(Stem t, XElement x)
            {
                var i = GetFileLocationInformation(t);

                var XmlnsAttributes = new List<Node>();
                var Attributes = new List<Node>();
                var Elements = new List<Node>();
                var Values = new List<Node>();
                foreach (var n in t.Children)
                {
                    switch (n._Tag)
                    {
                        case NodeTag.Empty:
                        case NodeTag.Leaf:
                            Values.Add(n);
                            break;
                        case NodeTag.Stem:
                            {
                                var Name = n.Stem.Name;
                                if (Name.StartsWith("@xml:") || Name.StartsWith("@xmlns:"))
                                {
                                    XmlnsAttributes.Add(n);
                                    continue;
                                }
                                if (Name.StartsWith("@"))
                                {
                                    Attributes.Add(n);
                                    continue;
                                }
                                Elements.Add(n);
                                break;
                            }
                        default:
                            throw new ArgumentException();
                    }
                }
                if (Elements.Count > 0 && Values.Count > 0) throw new InvalidTextFormatException("NotTree", i ?? new FileLocationInformation());
                if (Values.Count > 1) throw new InvalidTextFormatException("NotTree", i ?? new FileLocationInformation());
                foreach (var a in XmlnsAttributes)
                {
                    var xa = GetAttribute(a, x);
                    x.SetAttributeValue(xa.Name, xa.Value);
                }
                foreach (var a in Attributes)
                {
                    var xa = GetAttribute(a, x);
                    x.SetAttributeValue(xa.Name, xa.Value);
                }
                foreach (var e in Elements)
                {
                    var ns = e.Stem;
                    var ei = GetFileLocationInformation(ns);
                    var n = TryGetXName(ns.Name, null);
                    if (n == null) throw new InvalidTextFormatException("NamingError", ei ?? new FileLocationInformation());
                    var ex = new XElementEx(n);
                    if (ei != null) ex.SetLineInfo(ei);
                    x.Add(ex);
                    FillElement(ns, ex);
                }
                if (Values.Count == 1)
                {
                    var c = Values.Single();
                    switch (c._Tag)
                    {
                        case NodeTag.Empty:
                            break;
                        case NodeTag.Leaf:
                            x.Value = c.Leaf;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
                else if (Elements.Count == 0 && Values.Count == 0)
                {
                    x.Value = "";
                }
            }

            private XAttribute GetAttribute(Node t, XElement Parent)
            {
                var i = GetFileLocationInformation(t);
                if (!t.OnStem) throw new InvalidTextFormatException("NotAttribute", i ?? new FileLocationInformation());
                var ns = t.Stem;
                if (ns.Children.Count != 1) throw new InvalidTextFormatException("NotAttribute", i ?? new FileLocationInformation());
                var c = ns.Children.Single();
                if (!c.OnLeaf) throw new InvalidTextFormatException("NotAttribute", i ?? new FileLocationInformation());
                var xa = new XAttribute(TryGetXName(ns.Name.Substring(1), Parent), c.Leaf);
                return xa;
            }

            private static XName TryGetXName(string Name, XElement Node)
            {
                var NameParts = Name.Split(':');
                if (NameParts.Length > 2) return null;
                if (NameParts.Length <= 1)
                {
                    if (Node == null)
                    {
                        return Name;
                    }
                    else
                    {
                        return Node.GetDefaultNamespace().GetName(Name);
                    }
                }
                else
                {
                    var NamespacePrefix = NameParts[0];
                    var LocalName = NameParts[1];
                    if (Node == null)
                    {
                        switch (NamespacePrefix)
                        {
                            case "xml":
                                return XNamespace.Xml.GetName(LocalName);
                            case "xmlns":
                                return XNamespace.Xmlns.GetName(LocalName);
                            default:
                                return null;
                        }
                    }
                    else
                    {
                        return Node.GetNamespaceOfPrefix(NamespacePrefix).GetName(LocalName);
                    }
                }
            }

            private FileLocationInformation GetFileLocationInformation(object Obj)
            {
                if (Positions == null) return null;
                if (!Positions.ContainsKey(Obj)) return null;
                var r = Positions[Obj];
                if (!r.Range.OnSome) return new FileLocationInformation { Path = r.Text.Path };
                return new FileLocationInformation { Path = r.Text.Path, LineNumber = r.Range.Value.Start.Row, ColumnNumber = r.Range.Value.Start.Column };
            }
        }

        private class XElementEx : XElement, IXmlLineInfo, IFileLocationInformationProvider
        {
            public XElementEx(XName Name)
                : base(Name)
            {
            }
            public XElementEx(XName Name, object Content)
                : base(Name, Content)
            {
            }

            bool IXmlLineInfo.HasLineInfo()
            {
                return (this.Annotation<FileLocationInformation>() != null);
            }

            int IXmlLineInfo.LineNumber
            {
                get
                {
                    var annotation = this.Annotation<FileLocationInformation>();
                    if ((annotation != null))
                    {
                        return annotation.LineNumber;
                    }
                    return 0;
                }
            }

            int IXmlLineInfo.LinePosition
            {
                get
                {
                    var annotation = this.Annotation<FileLocationInformation>();
                    if ((annotation != null))
                    {
                        return annotation.ColumnNumber;
                    }
                    return 0;
                }
            }

            public void SetLineInfo(FileLocationInformation i)
            {
                this.AddAnnotation(i);
            }

            FileLocationInformation IFileLocationInformationProvider.FileLocationInformation
            {
                get
                {
                    var annotation = this.Annotation<FileLocationInformation>();
                    if ((annotation != null))
                    {
                        return annotation;
                    }
                    return new FileLocationInformation();
                }
            }
        }

        private class XmlToTreeTranslator
        {
            private XElement Value;
            private Dictionary<object, Syntax.FileTextRange> Positions;

            public XmlToTreeTranslator(XElement Value)
            {
                this.Value = Value;
                Positions = new Dictionary<object, Syntax.FileTextRange>();
            }

            public TreeFormatResult Translate()
            {
                var e = TranslateElement(Value);
                return new TreeFormatResult { Value = new Forest { Nodes = new List<Node> { e } }, Positions = Positions };
            }

            private Node TranslateElement(XElement xe)
            {
                var Attributes = new List<Node>();
                var Elements = new List<Node>();

                foreach (var a in xe.Attributes())
                {
                    Attributes.Add(TranslateAttribute(a, xe));
                }

                foreach (var e in xe.Elements())
                {
                    Elements.Add(TranslateElement(e));
                }

                if (Elements.Count > 0)
                {
                    return MakeStemNode(GetNameString(xe), Attributes.Concat(Elements).ToList(), xe);
                }

                if (Attributes.Count > 0)
                {
                    if (xe.IsEmpty)
                    {
                        return MakeStemNode(GetNameString(xe), Attributes.Concat(new[] { MakeEmptyNode(xe) }).ToList(), xe);
                    }
                    else
                    {
                        return MakeStemNode(GetNameString(xe), Attributes.Concat(new[] { MakeLeafNode(xe.Value, xe) }).ToList(), xe);
                    }
                }

                if (xe.IsEmpty)
                {
                    return MakeStemNode(GetNameString(xe), new[] { MakeEmptyNode(xe) }.ToList(), xe);
                }
                else
                {
                    return MakeStemNode(GetNameString(xe), new[] { MakeLeafNode(xe.Value, xe) }.ToList(), xe);
                }
            }

            private Node TranslateAttribute(XAttribute xa, XElement xe)
            {
                return MakeStemNode("@" + GetNameString(xa.Name, xe), new[] { MakeLeafNode(xa.Value, xa) }.ToList(), xa);
            }

            private static string GetNameString(XName Name, XElement Node)
            {
                var NamespacePrefix = Node.GetPrefixOfNamespace(Name.Namespace);
                if (NamespacePrefix == null) return Name.LocalName;
                return NamespacePrefix + ":" + Name.LocalName;
            }
            private static string GetNameString(XElement Node)
            {
                return GetNameString(Node.Name, Node);
            }

            private T Mark<T>(T Obj, Optional<Syntax.FileTextRange> Range)
            {
                if (Range.OnSome) Positions.Add(Obj, Range.Value);
                return Obj;
            }
            private Node MakeEmptyNode(XObject x)
            {
                var Range = GetFileTextRange(x);
                var n = Mark(Node.CreateEmpty(), Range);
                return n;
            }
            private Node MakeLeafNode(string Value, XObject x)
            {
                var Range = GetFileTextRange(x);
                var n = Mark(Node.CreateLeaf(Value), Range);
                return n;
            }
            private Node MakeStemNode(string Name, List<Node> Children, XObject x)
            {
                var Range = GetFileTextRange(x);
                var s = Mark(new Stem { Name = Name, Children = Children }, Range);
                var n = Mark(Node.CreateStem(s), Range);
                return n;
            }

            private Optional<Syntax.FileTextRange> GetFileTextRange(XObject x)
            {
                var i = new FileLocationInformation();
                var flip = x as IFileLocationInformationProvider;
                if (flip != null)
                {
                    i = flip.FileLocationInformation;
                }
                else
                {
                    var li = (IXmlLineInfo)x;
                    if (li.HasLineInfo())
                    {
                        i.LineNumber = li.LineNumber;
                        i.ColumnNumber = li.LinePosition;
                    }
                }
                var Start = new Syntax.TextPosition { CharIndex = 1, Row = i.LineNumber, Column = i.ColumnNumber };
                var Range = new Syntax.TextRange { Start = Start, End = Start };
                return new Syntax.FileTextRange { Text = new Syntax.Text { Path = "", Lines = new List<Syntax.TextLine> { } }, Range = Range };
            }
        }

        private class XmlToTreeRawTranslator
        {
            private XElement Value;
            private Dictionary<object, Syntax.TextRange> Positions;

            public XmlToTreeRawTranslator(XElement Value)
            {
                this.Value = Value;
                Positions = new Dictionary<object, Syntax.TextRange>();
            }

            public TreeFormatParseResult Translate()
            {
                var e = TranslateElement(Value);
                var Range = GetFileTextRange(Value);
                return new TreeFormatParseResult { Value = new Syntax.Forest { MultiNodesList = new List<Syntax.MultiNodes> { Mark(Syntax.MultiNodes.CreateNode(e), Range) } }, Positions = Positions };
            }

            private Syntax.Node TranslateElement(XElement xe)
            {
                var Attributes = new List<Syntax.Node>();
                var Elements = new List<Syntax.Node>();
                var Children = new List<Syntax.Node>();

                foreach (var a in xe.Attributes())
                {
                    var ta = TranslateAttribute(a, xe);
                    Attributes.Add(ta);
                    Children.Add(ta);
                }

                if (xe.Attributes().Count() == 0 && (!xe.Nodes().Any(n => n.NodeType == XmlNodeType.Comment)) && xe.Elements().Count() > 1)
                {
                    if (xe.Elements().All(c => c.Attributes().Count() == 0 && (!c.Nodes().Any(n => n.NodeType == XmlNodeType.Comment)) && (c.Elements().Count() == 0 || c.Elements().Count() == 1)))
                    {
                        var ChildNames = xe.Elements().Select(c => c.Name.LocalName).Distinct().ToList();
                        if (ChildNames.Count == 1)
                        {
                            var ChildElements = new List<Syntax.Node>();
                            foreach (var n in xe.Nodes())
                            {
                                if (n.NodeType == XmlNodeType.Element)
                                {
                                    var e = (XElement)n;
                                    Syntax.Node tce;
                                    if (e.Elements().Count() == 0)
                                    {
                                        if (e.IsEmpty)
                                        {
                                            tce = MakeEmptyNode(e);
                                        }
                                        else
                                        {
                                            tce = MakeLeafNode(e.Value, e);
                                        }
                                    }
                                    else
                                    {
                                        tce = TranslateElement(e.Elements().Single());
                                    }
                                    ChildElements.Add(tce);
                                }
                            }
                            return MakeStemNodeOfList(GetNameString(xe), ChildNames.Single(), ChildElements, xe);
                        }
                    }
                }

                foreach (var n in xe.Nodes())
                {
                    if (n.NodeType == XmlNodeType.Element)
                    {
                        var e = (XElement)n;
                        var te = TranslateElement(e);
                        Elements.Add(te);
                        Children.Add(te);
                    }
                    else if (n.NodeType == XmlNodeType.Comment)
                    {
                        var c = (XComment)n;
                        var tc = TranslateComment(c);
                        Children.Add(tc);
                    }
                }

                if (Elements.Count > 0)
                {
                    return MakeStemNode(GetNameString(xe), Children, xe);
                }

                if (Attributes.Count > 0)
                {
                    if (xe.IsEmpty)
                    {
                        return MakeStemNode(GetNameString(xe), Attributes.Concat(new[] { MakeEmptyNode(xe) }).ToList(), xe);
                    }
                    else
                    {
                        return MakeStemNode(GetNameString(xe), Attributes.Concat(new[] { MakeLeafNode(xe.Value, xe) }).ToList(), xe);
                    }
                }

                if (xe.IsEmpty)
                {
                    return MakeStemNode(GetNameString(xe), new[] { MakeEmptyNode(xe) }.ToList(), xe);
                }
                else
                {
                    return MakeStemNode(GetNameString(xe), new[] { MakeLeafNode(xe.Value, xe) }.ToList(), xe);
                }
            }

            private Syntax.Node TranslateAttribute(XAttribute xa, XElement xe)
            {
                return MakeStemNode("@" + GetNameString(xa.Name, xe), new[] { MakeLeafNode(xa.Value, xa) }.ToList(), xa);
            }

            private Syntax.Node TranslateComment(XComment xc)
            {
                var Value = xc.Value;
                var Range = GetFileTextRange(xc);
                var Literal = TreeFormatLiteralWriter.GetLiteral(Value, false, false);
                if (Literal.OnSingleLine)
                {
                    var n = Mark(Syntax.Node.CreateSingleLineComment(Mark(new Syntax.SingleLineComment { Content = Mark(new Syntax.FreeContent { Text = Value }, Range) }, Range)), Range);
                    return n;
                }
                else if (Literal.OnMultiLine)
                {
                    var n = Mark(Syntax.Node.CreateMultiLineComment(Mark(new Syntax.MultiLineComment { SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Content = new Syntax.FreeContent { Text = Value }, EndDirective = Optional<Syntax.EndDirective>.Empty }, Range)), Range);
                    return n;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private static string GetNameString(XName Name, XElement Node)
            {
                var NamespacePrefix = Node.GetPrefixOfNamespace(Name.Namespace);
                if (NamespacePrefix == null) return Name.LocalName;
                return NamespacePrefix + ":" + Name.LocalName;
            }
            private static string GetNameString(XElement Node)
            {
                return GetNameString(Node.Name, Node);
            }

            private T Mark<T>(T Obj, Optional<Syntax.FileTextRange> Range)
            {
                if (Range.OnSome && Range.Value.Range.OnSome) Positions.Add(Obj, Range.Value.Range.Value);
                return Obj;
            }
            private Syntax.Node MakeEmptyNode(XObject x)
            {
                var Range = GetFileTextRange(x);
                var EmptyNode = Mark(Syntax.SingleLineNode.CreateEmptyNode(Mark(new Syntax.EmptyNode(), Range)), Range);
                var n = Mark(Syntax.Node.CreateSingleLineNodeLine(Mark(new Syntax.SingleLineNodeLine { SingleLineNode = EmptyNode, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty }, Range)), Range);
                return n;
            }
            private Syntax.Node MakeLeafNode(string Value, XObject x)
            {
                var Range = GetFileTextRange(x);
                var Literal = TreeFormatLiteralWriter.GetLiteral(Value, false, false);
                if (Literal.OnSingleLine)
                {
                    var LeafNode = Mark(Syntax.SingleLineNode.CreateSingleLineLiteral(Mark(new Syntax.SingleLineLiteral { Text = Value }, Range)), Range);
                    var n = Mark(Syntax.Node.CreateSingleLineNodeLine(Mark(new Syntax.SingleLineNodeLine { SingleLineNode = LeafNode, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty }, Range)), Range);
                    return n;
                }
                else if (Literal.OnMultiLine)
                {
                    var n = Mark(Syntax.Node.CreateMultiLineLiteral(Mark(new Syntax.MultiLineLiteral { SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Content = new Syntax.FreeContent { Text = Value }, EndDirective = Optional<Syntax.EndDirective>.Empty }, Range)), Range);
                    return n;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            private Syntax.Node MakeStemNode(string Name, List<Syntax.Node> Children, XObject x)
            {
                var Range = GetFileTextRange(x);
                var NameLiteral = Mark(new Syntax.SingleLineLiteral { Text = Name }, Range);
                if (Children.Count == 0)
                {
                    var n = Mark(Syntax.Node.CreateMultiLineNode(Mark(new Syntax.MultiLineNode { Head = NameLiteral, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Children = new List<Syntax.MultiNodes> { }, EndDirective = Mark(new Syntax.EndDirective { EndSingleLineComment = Optional<Syntax.SingleLineComment>.Empty }, Range) }, Range)), Range);
                    return n;
                }
                else if (Children.Count == 1 && Children.Single().OnSingleLineNodeLine)
                {
                    var SingleLineNode = Mark(Syntax.SingleLineNode.CreateSingleLineNodeWithParameters(Mark(new Syntax.SingleLineNodeWithParameters { Head = NameLiteral, Children = new List<Syntax.ParenthesisNode> { }, LastChild = Children.Single().SingleLineNodeLine.SingleLineNode }, Range)), Range);
                    var n = Mark(Syntax.Node.CreateSingleLineNodeLine(Mark(new Syntax.SingleLineNodeLine { SingleLineNode = SingleLineNode, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty }, Range)), Range);
                    return n;
                }
                else
                {
                    var ChildrenNodes = Children.Select(c => Mark(Syntax.MultiNodes.CreateNode(c), Range)).ToList();
                    var n = Mark(Syntax.Node.CreateMultiLineNode(Mark(new Syntax.MultiLineNode { Head = NameLiteral, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Children = ChildrenNodes, EndDirective = Optional<Syntax.EndDirective>.Empty }, Range)), Range);
                    return n;
                }
            }
            private Syntax.Node MakeStemNodeOfList(string Name, string ListName, List<Syntax.Node> Children, XObject x)
            {
                var Range = GetFileTextRange(x);
                var NameLiteral = Mark(new Syntax.SingleLineLiteral { Text = Name }, Range);
                var ListNameLiteral = Mark(new Syntax.SingleLineLiteral { Text = ListName }, Range);
                var ChildrenNodes = Children.Select(c => Mark(Syntax.MultiNodes.CreateNode(c), Range)).ToList();
                var ListNode = Mark(Syntax.MultiNodes.CreateListNodes(Mark(new Syntax.ListNodes { ChildHead = ListNameLiteral, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Children = ChildrenNodes, EndDirective = Optional<Syntax.EndDirective>.Empty }, Range)), Range);
                var n = Mark(Syntax.Node.CreateMultiLineNode(Mark(new Syntax.MultiLineNode { Head = NameLiteral, SingleLineComment = Optional<Syntax.SingleLineComment>.Empty, Children = new List<Syntax.MultiNodes> { ListNode }, EndDirective = Optional<Syntax.EndDirective>.Empty }, Range)), Range);
                return n;
            }

            private Optional<Syntax.FileTextRange> GetFileTextRange(XObject x)
            {
                var i = new FileLocationInformation();
                var flip = x as IFileLocationInformationProvider;
                if (flip != null)
                {
                    i = flip.FileLocationInformation;
                }
                else
                {
                    var li = (IXmlLineInfo)x;
                    if (li.HasLineInfo())
                    {
                        i.LineNumber = li.LineNumber;
                        i.ColumnNumber = li.LinePosition;
                    }
                }
                var Start = new Syntax.TextPosition { CharIndex = 1, Row = i.LineNumber, Column = i.ColumnNumber };
                var Range = new Syntax.TextRange { Start = Start, End = Start };
                return new Syntax.FileTextRange { Text = new Syntax.Text { Path = "", Lines = new List<Syntax.TextLine> { } }, Range = Range };
            }
        }
    }
}
