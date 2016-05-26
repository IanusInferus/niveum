//==========================================================================
//
//  File:        RelationSchemaDiffLoader.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异加载器
//  Version:     2016.05.23.
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
using TreeFormat = Firefly.Texting.TreeFormat;
using Yuki.RelationSchema;

namespace Yuki.RelationSchemaDiff
{
    public sealed class RelationSchemaDiffLoader
    {
        private Dictionary<String, Dictionary<String, VariableDef>> EntityFields;
        private List<Semantics.Node> EntityMappings;
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
            EntityMappings = new List<Semantics.Node>();
            Positions = new Dictionary<Object, Syntax.FileTextRange>();
            this.tfpo = OuterParsingSetting;
            this.tfeo = OuterEvaluateSetting;
        }

        public List<EntityMapping> GetResult()
        {
            var tfr = new TreeFormatResult { Value = new Semantics.Forest { Nodes = new List<Semantics.Node> { MakeStemNode("ListOfEntityMapping", EntityMappings.ToArray()) } }, Positions = Positions };

            var x = XmlInterop.TreeToXml(tfr);
            var l = xs.Read<List<EntityMapping>>(x);

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
            var s = new Semantics.Stem { Name = Name, Children = Children.ToList() };
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
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new List<Syntax.TextLine> { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
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
                EntityMappings.AddRange(n.Stem.Children);
            }
            foreach (var p in t.Positions)
            {
                Positions.Add(p.Key, p.Value);
            }
        }

        public void LoadType(String TreePath)
        {
            Load(TreePath, EntityMappings);
        }
        public void LoadType(String TreePath, StreamReader Reader)
        {
            Load(TreePath, Reader, EntityMappings);
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
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new List<Syntax.TextLine> { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void Load(String TreePath, StreamReader Reader, List<Semantics.Node> EntityMappings)
        {
            var Functions = new HashSet<String>() { "Map" };
            var TableParameterFunctions = Functions;
            var TableContentFunctions = new HashSet<String>(Functions.Except(new List<String> { "Map" }));
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

            Func<int, Syntax.TextLine, ISemanticsNodeMaker, Semantics.Node[]> ParseEntityMappingsAsSemanticsNodes = (IndentLevel, Line, nm) =>
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
                    var tpr = TreeFormatTokenParser.ReadToken(Text, pr.Positions, Range);
                    if (!tpr.OnHasValue)
                    {
                        break;
                    }

                    var v = tpr.Value.Token;
                    if (v.OnSingleLineComment) { break; }
                    if (v.OnLeftParenthesis)
                    {
                        if (cl != null)
                        {
                            throw new Syntax.InvalidTokenException("DoubleLeftParenthesis", new Syntax.FileTextRange { Text = Text, Range = Range }, "(");
                        }
                        cl = new List<Semantics.Node>();
                        clStart = Range.Start;
                        clEnd = Range.End;
                    }
                    else if (v.OnRightParenthesis)
                    {
                        if (cl == null)
                        {
                            throw new Syntax.InvalidTokenException("DismatchedRightParenthesis", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (cl.Count == 0)
                        {
                            throw new Syntax.InvalidTokenException("EmptyIndex", new Syntax.FileTextRange { Text = Text, Range = Range }, ")");
                        }
                        if (tpr.Value.RemainingChars.OnHasValue)
                        {
                            clEnd = tpr.Value.RemainingChars.Value.End;
                        }
                        l.Add(nm.MakeStemNode("", cl, new Syntax.TextRange { Start = clStart, End = clEnd }));
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
                    else if (v.OnPreprocessDirective && (v.PreprocessDirective == "Empty"))
                    {
                        if (cl != null)
                        {
                            cl.Add(nm.MakeEmptyNode(pr.Positions[v]));
                        }
                        else
                        {
                            l.Add(nm.MakeEmptyNode(pr.Positions[v]));
                        }
                    }
                    else
                    {
                        throw new Syntax.InvalidTokenException("UnknownToken", new Syntax.FileTextRange { Text = Text, Range = Range }, Text.GetTextInLine(Range));
                    }

                    if (!tpr.Value.RemainingChars.OnHasValue)
                    {
                        break;
                    }

                    Range = tpr.Value.RemainingChars.Value;
                }
                if (cl != null)
                {
                    throw new Syntax.InvalidTokenException("DismatchedRightParentheses", new Syntax.FileTextRange { Text = Text, Range = Range }, "");
                }

                if (l.Count == 0) { return new Semantics.Node[] { }; }

                if (l.Count < 3)
                {
                    throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
                var EntityToken = GetLeafNodeValue(l[0], nm, "InvalidEntityToken");
                if (EntityToken != "Entity")
                {
                    throw new Syntax.InvalidSyntaxException("InvalidEntityToken", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
                var EntityName = GetLeafNodeValue(l[1], nm, "InvalidEntityName");
                var EntityMethodToken = GetLeafNodeValue(l[2], nm, "InvalidEntityMethodToken");
                if (EntityMethodToken == "New")
                {
                    if (l.Count != 3)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    return new Semantics.Node[]
                    {
                        MakeStemNode("EntityMapping",
                            MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                            MakeStemNode("Method", MakeStemNode("New", MakeEmptyNode()))
                        )
                    };
                }
                else if (EntityMethodToken == "From")
                {
                    if (l.Count != 4)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var EntityNameSource = GetLeafNodeValue(l[3], nm, "InvalidEntityName");
                    return new Semantics.Node[]
                    {
                        MakeStemNode("EntityMapping",
                            MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                            MakeStemNode("Method", MakeStemNode("Copy", MakeLeafNode(EntityNameSource)))
                        )
                    };
                }
                else if (EntityMethodToken == "Field")
                {
                    if (l.Count < 6)
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                    var FieldName = GetLeafNodeValue(l[3], nm, "InvalidFieldName");
                    var FieldMethodToken = GetLeafNodeValue(l[4], nm, "InvalidFieldMethodToken");
                    if (FieldMethodToken == "New")
                    {
                        if (l.Count != 6)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        if (!EntityFields.ContainsKey(EntityName))
                        {
                            throw new Syntax.InvalidSyntaxException("EntityNotExist", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var e = EntityFields[EntityName];
                        if (!e.ContainsKey(FieldName))
                        {
                            throw new Syntax.InvalidSyntaxException("FieldNotExist", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var Literal = l[5];
                        Semantics.Node Value;
                        var f = e[FieldName];
                        if (f.Type.OnTypeRef)
                        {
                            if (Literal.OnEmpty)
                            {
                                throw new Syntax.InvalidSyntaxException("FieldTypeIncompatible", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                            }
                            else
                            {
                                Value = MakeStemNode("HasValue", MakeStemNode(f.Type.TypeRef.Value + "Value", Literal));
                            }
                        }
                        else if (f.Type.OnOptional)
                        {
                            if (Literal.OnEmpty)
                            {
                                Value = MakeStemNode("NotHasValue", MakeEmptyNode());
                            }
                            else
                            {
                                Value = MakeStemNode("HasValue", MakeStemNode(f.Type.Optional.Value + "Value", Literal));
                            }
                        }
                        else if (f.Type.OnList)
                        {
                            if (f.Type.List.Value == "Byte")
                            {
                                if (Literal.OnEmpty)
                                {
                                    throw new Syntax.InvalidSyntaxException("FieldTypeIncompatible", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                                }
                                else
                                {
                                    Value = MakeStemNode("HasValue", MakeStemNode("BinaryValue", Literal));
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException();
                            }
                        }
                        else
                        {
                            throw new Syntax.InvalidSyntaxException("FieldTypeIncompatible", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        return new Semantics.Node[]
                        {
                            MakeStemNode("EntityMapping",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("New", Value))
                                ))
                            )
                        };
                    }
                    else if (FieldMethodToken == "From")
                    {
                        if (l.Count != 6)
                        {
                            throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                        }
                        var FieldNameSource = GetLeafNodeValue(l[5], nm, "InvalidFieldName");
                        return new Semantics.Node[]
                        {
                            MakeStemNode("EntityMapping",
                                MakeStemNode("EntityName", MakeLeafNode(EntityName)),
                                MakeStemNode("Method", MakeStemNode("Field",
                                    MakeStemNode("FieldName", MakeLeafNode(FieldName)),
                                    MakeStemNode("Method", MakeStemNode("Copy", MakeLeafNode(FieldNameSource)))
                                ))
                            )
                        };
                    }
                    else
                    {
                        throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                    }
                }
                else
                {
                    throw new Syntax.InvalidSyntaxException("InvalidEntityMapping", new Syntax.FileTextRange { Text = Text, Range = LineRange });
                }
            };

            var es = new TreeFormatEvaluateSetting()
            {
                FunctionCallEvaluator = (f, nm) =>
                {
                    if (f.Parameters.Count == 0)
                    {
                        if (f.Name.Text == "Map")
                        {
                            var Nodes = f.Content.Value.LineContent.Lines.SelectMany(Line => ParseEntityMappingsAsSemanticsNodes(f.Content.Value.LineContent.IndentLevel, Line, nm)).ToArray();
                            return new List<Semantics.Node>
                            {
                                MakeStemNode("ListOfEntityMapping", Nodes)
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
                EntityMappings.AddRange(n.Stem.Children);
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
