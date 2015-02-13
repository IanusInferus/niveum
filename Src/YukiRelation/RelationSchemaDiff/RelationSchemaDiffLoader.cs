//==========================================================================
//
//  File:        RelationSchemaDiffLoader.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异加载器
//  Version:     2015.02.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using Semantics = Firefly.Texting.TreeFormat.Semantics;
using Yuki.RelationSchema;

namespace Yuki.RelationSchemaDiff
{
    // 差异文件支持的语法
    // Create id
    // Delete id
    // Rename id id
    // Alter id Create id literal
    // Alter id Delete id
    // Alter id Rename id id
    // Alter id ChangeType id

    public sealed class RelationSchemaDiffLoader
    {
        private Dictionary<String, Dictionary<String, VariableDef>> EntityFields;
        private List<Semantics.Node> Alters;
        private Dictionary<Object, Syntax.FileTextRange> Positions;
        private TreeFormatParseSetting tfpo = null;
        private TreeFormatEvaluateSetting tfeo = null;
        private XmlSerializer xs = new XmlSerializer();

        public RelationSchemaDiffLoader(RelationSchema.Schema SchemaNew)
            : this(SchemaNew, null, null)
        {
        }

        public RelationSchemaDiffLoader(RelationSchema.Schema SchemaNew, TreeFormatParseSetting OuterParsingSetting, TreeFormatEvaluateSetting OuterEvaluateSetting)
        {
            EntityFields = SchemaNew.GetMap().Where(p => p.Value.OnEntity).ToDictionary(p => p.Key, p => p.Value.Entity.Fields.ToDictionary(f => f.Name));
            Alters = new List<Semantics.Node>();
            Positions = new Dictionary<Object, Syntax.FileTextRange>();
            this.tfpo = OuterParsingSetting;
            this.tfeo = OuterEvaluateSetting;
        }

        public List<AlterEntity> GetResult()
        {
            var tfr = new TreeFormatResult { Value = new Semantics.Forest { Nodes = new Semantics.Node[] { MakeStemNode("ListOfAlter", Alters.ToArray()) } }, Positions = Positions };

            var x = XmlInterop.TreeToXml(tfr);
            var l = xs.Read<List<AlterEntity>>(x);

            return l;
        }
        private Semantics.Node MakeEmptyNode()
        {
            var n = Semantics.Node.CreateEmpty();
            return n;
        }
        private Semantics.Node MakeLeafNode(String Value)
        {
            var n = Semantics.Node.CreateLeaf(Value);
            return n;
        }
        private Semantics.Node MakeStemNode(String Name, params Semantics.Node[] Children)
        {
            var s = new Semantics.Stem { Name = Name, Children = Children };
            var n = Semantics.Node.CreateStem(s);
            return n;
        }

        public void LoadSchema(String TreePath)
        {
            using (var Reader = Txt.CreateTextReader(TreePath))
            {
                if (Debugger.IsAttached)
                {
                    LoadSchema(TreePath, Reader);
                }
                else
                {
                    try
                    {
                        LoadSchema(TreePath, Reader);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new Syntax.TextLine[] { } }, Range = Opt<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        public void LoadSchema(String TreePath, StreamReader Reader)
        {
            var t = TreeFile.ReadDirect(Reader, TreePath, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            LoadSchema(t);
        }
        public void LoadSchema(TreeFormatResult t)
        {
            foreach (var n in t.Value.Nodes)
            {
                Alters.AddRange(n.Stem.Children);
            }
            foreach (var p in t.Positions)
            {
                Positions.Add(p.Key, p.Value);
            }
        }

        public void LoadType(String TreePath)
        {
            Load(TreePath, Alters);
        }
        public void LoadType(String TreePath, StreamReader Reader)
        {
            Load(TreePath, Reader, Alters);
        }
        private void Load(String TreePath, List<Semantics.Node> Types)
        {
            using (var Reader = Txt.CreateTextReader(TreePath))
            {
                if (Debugger.IsAttached)
                {
                    Load(TreePath, Reader, Types);
                }
                else
                {
                    try
                    {
                        Load(TreePath, Reader, Types);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new Syntax.TextLine[] { } }, Range = Opt<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void Load(String TreePath, StreamReader Reader, List<Semantics.Node> Alters)
        {
            var Functions = new HashSet<String>() { "Alter" };
            var TableParameterFunctions = Functions;
            var TableContentFunctions = new HashSet<String>(Functions.Except(new List<String> { "Alter" }));
            TreeFormatParseSetting ps;
            if (tfpo == null)
            {
                ps = new TreeFormatParseSetting()
                {
                    IsTableParameterFunction = Name => TableParameterFunctions.Contains(Name),
                    IsTableContentFunction = Name => TableContentFunctions.Contains(Name)
                };
            }
            else
            {
                ps = new TreeFormatParseSetting()
                {
                    IsTableParameterFunction = Name =>
                    {
                        if (TableParameterFunctions.Contains(Name)) { return true; }
                        return tfpo.IsTableParameterFunction(Name);
                    },
                    IsTableContentFunction = Name =>
                    {
                        if (TableContentFunctions.Contains(Name)) { return true; }
                        return tfpo.IsTableContentFunction(Name);
                    },
                    IsTreeParameterFunction = tfpo.IsTreeParameterFunction,
                    IsTreeContentFunction = tfpo.IsTreeContentFunction
                };
            }
            var pr = TreeFile.ReadRaw(Reader, TreePath, ps);
            var Text = pr.Text;
            var TokenParser = new TreeFormatTokenParser(Text, pr.Positions);

            Func<int, Syntax.TextLine, ISemanticsNodeMaker, Semantics.Node[]> ParseAlterEntityAsSemanticsNodes = (IndentLevel, Line, nm) =>
            {
                var l = new List<Semantics.Node>();
                List<Semantics.Node> cl = null;
                Syntax.TextPosition clStart = default(Syntax.TextPosition);
                Syntax.TextPosition clEnd = default(Syntax.TextPosition);
                if (Line.Text.Length < IndentLevel * 4)
                {
                    return new Semantics.Node[] { };
                }
                var LineRange = new Syntax.TextRange { Start = Text.Calc(Line.Range.Start, IndentLevel * 4), End = Line.Range.End };
                var Range = LineRange;
                while (true)
                {
                    var tpr = TokenParser.ReadToken(Range);
                    if (!tpr.Token.HasValue)
                    {
                        break;
                    }

                    var v = tpr.Token.Value;
                    if (v.OnSingleLineComment) { break; }
                    if (v.OnLeftParentheses)
                    {
                        if (cl != null)
                        {
                            throw new Syntax.InvalidTokenException("DoubleLeftParentheses", new Syntax.FileTextRange { Text = Text, Range = Range }, "(");
                        }
                        cl = new List<Semantics.Node>();
                        clStart = Range.Start;
                        clEnd = Range.End;
                    }
                    else if (v.OnRightParentheses)
                    {
                        if (cl == null)
                        {
                            throw new Syntax.InvalidTokenException("DismatchedRightParentheses", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (cl.Count == 0)
                        {
                            throw new Syntax.InvalidTokenException("EmptyIndex", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (tpr.RemainingChars.HasValue)
                        {
                            clEnd = tpr.RemainingChars.Value.End;
                        }
                        l.Add(nm.MakeStemNode("", cl.ToArray(), new Syntax.TextRange { Start = clStart, End = clEnd }));
                        cl = null;
                        clStart = default(Syntax.TextPosition);
                        clEnd = default(Syntax.TextPosition);
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
                        throw new Syntax.InvalidTokenException("UnknownToken", new Syntax.FileTextRange { Text = Text, Range = Range }, Text.GetTextInLine(Range));
                    }

                    if (!tpr.RemainingChars.HasValue)
                    {
                        break;
                    }

                    Range = tpr.RemainingChars.Value;
                }
                if (cl != null)
                {
                    throw new Syntax.InvalidTokenException("DismatchedRightParentheses", new Syntax.FileTextRange { Text = Text, Range = Range }, "");
                }

                if (l.Count == 0) { return new Semantics.Node[] { }; }

                if (l.Count < 1)
                {
                    throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
                var FirstVerb = GetLeafNodeValue(l[0], nm, "InvalidVerb");
                if (FirstVerb == "Create")
                {
                    if (l.Count != 2)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var EntityName = GetLeafNodeValue(l[1], nm, "InvalidIdentifier");
                    return new Semantics.Node[]
                    {
                        MakeStemNode("AlterEntity",
                            MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                            MakeStemNode("Method", MakeStemNode("Create", MakeEmptyNode()))
                        )
                    };
                }
                else if (FirstVerb == "Delete")
                {
                    if (l.Count != 2)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var EntityName = GetLeafNodeValue(l[1], nm, "InvalidIdentifier");
                    return new Semantics.Node[]
                    {
                        MakeStemNode("AlterEntity",
                            MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                            MakeStemNode("Method", MakeStemNode("Delete", MakeEmptyNode()))
                        )
                    };
                }
                else if (FirstVerb == "Rename")
                {
                    if (l.Count != 3)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var EntityName = GetLeafNodeValue(l[1], nm, "InvalidIdentifier");
                    var EntityNameDestination = GetLeafNodeValue(l[2], nm, "InvalidIdentifier");
                    return new Semantics.Node[]
                    {
                        MakeStemNode("AlterEntity",
                            MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                            MakeStemNode("Method", MakeStemNode("Rename", MakeLeafNode(EntityNameDestination)))
                        )
                    };
                }
                else if (FirstVerb == "Alter")
                {
                    if (l.Count < 3)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var EntityName = GetLeafNodeValue(l[1], nm, "InvalidIdentifier");
                    var SecondVerb = GetLeafNodeValue(l[2], nm, "InvalidVerb");
                    if (SecondVerb == "Create")
                    {
                        if (l.Count != 5)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var FieldName = GetLeafNodeValue(l[3], nm, "InvalidIdentifier");
                        var Literal = l[4];
                        if (!EntityFields.ContainsKey(EntityName))
                        {
                            throw new Syntax.InvalidSyntaxException("EntityNotExist", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var e = EntityFields[EntityName];
                        if (!e.ContainsKey(FieldName))
                        {
                            throw new Syntax.InvalidSyntaxException("FieldNotExist", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var f = e[FieldName];
                        if (!f.Type.OnTypeRef)
                        {
                            throw new Syntax.InvalidSyntaxException("FieldTypeIncompatible", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        return new Semantics.Node[]
                        {
                            MakeStemNode("AlterEntity",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("Create", MakeStemNode(f.Type.TypeRef.Value + "Value", Literal)))
                                ))
                            )
                        };
                    }
                    else if (SecondVerb == "Delete")
                    {
                        if (l.Count != 4)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var FieldName = GetLeafNodeValue(l[3], nm, "InvalidIdentifier");
                        return new Semantics.Node[]
                        {
                            MakeStemNode("AlterEntity",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("Delete", MakeEmptyNode()))
                                ))
                            )
                        };
                    }
                    else if (SecondVerb == "Rename")
                    {
                        if (l.Count != 5)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var FieldName = GetLeafNodeValue(l[3], nm, "InvalidIdentifier");
                        var FieldNameDestination = GetLeafNodeValue(l[4], nm, "InvalidIdentifier");
                        return new Semantics.Node[]
                        {
                            MakeStemNode("AlterEntity",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("Rename", MakeLeafNode(FieldNameDestination)))
                                ))
                            )
                        };
                    }
                    else if (SecondVerb == "ChangeType")
                    {
                        if (l.Count != 4)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var FieldName = GetLeafNodeValue(l[3], nm, "InvalidIdentifier");
                        return new Semantics.Node[]
                        {
                            MakeStemNode("AlterEntity",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("ChangeType", MakeEmptyNode()))
                                ))
                            )
                        };
                    }
                    else
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                }
                else
                {
                    throw new Syntax.InvalidSyntaxException("InvalidAlter", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
            };

            var es = new TreeFormatEvaluateSetting()
            {
                FunctionCallEvaluator = (f, nm) =>
                {
                    if (f.Parameters.Length == 0)
                    {
                        if (f.Name.Text == "Alter")
                        {
                            var Nodes = f.Content.Value.LineContent.Lines.SelectMany(Line => ParseAlterEntityAsSemanticsNodes(f.Content.Value.LineContent.IndentLevel, Line, nm)).ToArray();
                            return new Semantics.Node[]
                            {
                                MakeStemNode("ListOfAlterEntity", Nodes)
                            };
                        }
                        else
                        {
                            if (tfeo != null)
                            {
                                return tfeo.FunctionCallEvaluator(f, nm);
                            }
                            throw new Syntax.InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                        }
                    }
                    else
                    {
                        throw new Syntax.InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f);
                    }
                },
                TokenParameterEvaluator = tfeo != null ? tfeo.TokenParameterEvaluator : null
            };

            var tfe = new TreeFormatEvaluator(es, pr);
            var t = tfe.Evaluate();
            foreach (var n in t.Value.Nodes)
            {
                Alters.AddRange(n.Stem.Children);
            }
            foreach (var p in t.Positions)
            {
                Positions.Add(p.Key, p.Value);
            }
        }

        private String GetLeafNodeValue(Semantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!n.OnLeaf) { throw new Syntax.InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }
    }
}
