//==========================================================================
//
//  File:        ObjectSchemaWriter.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构写入器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Niveum.ObjectSchema
{
    public class ObjectSchemaWriter
    {
        public ObjectSchemaWriter()
        {
        }

        public String Write(List<TypeDef> Types, String Comment = "")
        {
            var f = WriteToForest(Types, Comment);

            String Compiled;
            using (var ms = Streams.CreateMemoryStream())
            {
                using (var tw = Txt.CreateTextWriter(ms.Partialize(0, Int64.MaxValue, 0).AsNewWriting(), TextEncoding.UTF8))
                {
                    var sw = new TreeFormatSyntaxWriter(tw);
                    sw.Write(f);
                }
                ms.Position = 0;
                using (var tr = Txt.CreateTextReader(ms.Partialize(0, ms.Length).AsNewReading(), TextEncoding.UTF8))
                {
                    Compiled = tr.ReadToEnd();
                }
            }
            return Compiled;
        }

        private Syntax.Forest WriteToForest(List<TypeDef> Types, String Comment)
        {
            var MultiNodesList = new List<Syntax.MultiNodes>();
            if (Comment != "")
            {
                var mlc = new Syntax.MultiLineComment { SingleLineComment = TreeFormat.Optional<Syntax.SingleLineComment>.Empty, Content = new Syntax.FreeContent { Text = Comment }, EndDirective = TreeFormat.Optional<Syntax.EndDirective>.Empty };
                MultiNodesList.Add(Syntax.MultiNodes.CreateNode(Syntax.Node.CreateMultiLineComment(mlc)));
            }
            foreach (var t in Types)
            {
                var LineTokens = new List<List<String>>();
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    LineTokens.AddRange(p.GenericParameters.Select(v => WriteToTokens(v, true)));
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    LineTokens.AddRange(a.GenericParameters.Select(v => WriteToTokens(v, true)));
                    LineTokens.Add(new List<String> { GetTypeString(a.Type) });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    LineTokens.AddRange(r.GenericParameters.Select(v => WriteToTokens(v, true)));
                    LineTokens.AddRange(r.Fields.Select(v => WriteToTokens(v, false)));
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    LineTokens.AddRange(tu.GenericParameters.Select(v => WriteToTokens(v, true)));
                    LineTokens.AddRange(tu.Alternatives.Select(v => WriteToTokens(v, false)));
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    LineTokens.AddRange(e.Literals.Select(l => WriteToTokens(l)));
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    LineTokens.AddRange(cc.OutParameters.Select(v => WriteToTokens(v, false)));
                    LineTokens.Add(new List<String> { ">" });
                    LineTokens.AddRange(cc.InParameters.Select(v => WriteToTokens(v, false)));
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    LineTokens.AddRange(sc.OutParameters.Select(v => WriteToTokens(v, false)));
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var LineLiterals = LineTokens.Select(l => (l.Count == 1 && l.Single() == ">") ? l : l.Select(Token => TreeFormatLiteralWriter.GetLiteral(Token, true, false).SingleLine).ToList()).ToList();
                var NumColumn = 0;
                foreach (var l in LineLiterals)
                {
                    NumColumn = Math.Max(NumColumn, l.Count);
                }
                var ColumnWidths = new List<int>();
                for (int k = 0; k <= NumColumn - 2; k += 1)
                {
                    int Width = 28;
                    foreach (var l in LineLiterals)
                    {
                        if (k < l.Count)
                        {
                            var Column = l[k];
                            Width = Math.Max(Width, CalculateCharWidth(Column).CeilToMultipleOf(4) + 4);
                        }
                    }
                    ColumnWidths.Add(Width);
                }
                var Lines = new List<Syntax.TextLine>();
                foreach (var l in LineLiterals)
                {
                    var Line = new List<String>();
                    for (int k = 0; k < l.Count; k += 1)
                    {
                        var Column = l[k];
                        if ((k != l.Count - 1) && (k < ColumnWidths.Count))
                        {
                            var Width = ColumnWidths[k];
                            Line.Add(Column + new String(' ', Width - CalculateCharWidth(Column)));
                        }
                        else
                        {
                            Line.Add(Column);
                        }
                    }
                    var LineText = String.Join("", Line.ToArray());
                    Lines.Add(new Syntax.TextLine { Text = LineText, Range = new Syntax.TextRange { Start = new Syntax.TextPosition { Row = 1, Column = 1, CharIndex = 0 }, End = new Syntax.TextPosition { Row = 1, Column = 1 + LineText.Length, CharIndex = LineText.Length } } });
                }
                var fn = WriteToFunctionNodes(t._Tag.ToString(), t.VersionedName(), t.Description(), Lines);
                MultiNodesList.Add(Syntax.MultiNodes.CreateFunctionNodes(fn));
            }
            return new Syntax.Forest { MultiNodesList = MultiNodesList };
        }

        private Syntax.FunctionNodes WriteToFunctionNodes(String Directive, String Name, String Description, List<Syntax.TextLine> Lines)
        {
            var Parameters = new List<Syntax.Token>();
            Parameters.Add(Syntax.Token.CreateSingleLineLiteral(Name));
            if (Description != "")
            {
                Parameters.Add(Syntax.Token.CreateSingleLineLiteral(Description));
            }
            var fn = new Syntax.FunctionNodes
            {
                FunctionDirective = new Syntax.FunctionDirective { Text = Directive },
                Parameters = Parameters,
                SingleLineComment = TreeFormat.Optional<Syntax.SingleLineComment>.Empty,
                Content = new Syntax.FunctionContent { IndentLevel = 0, Lines = Lines },
                EndDirective = TreeFormat.Optional<Syntax.EndDirective>.Empty
            };
            return fn;
        }

        private List<String> WriteToTokens(VariableDef v, bool IsGenericParameter)
        {
            var l = new List<String>();
            if (IsGenericParameter)
            {
                l.Add("'" + v.Name);
            }
            else
            {
                l.Add(v.Name);
            }
            l.Add(GetTypeString(v.Type));
            if (v.Description != "")
            {
                l.Add(v.Description);
            }
            return l;
        }

        private List<String> WriteToTokens(LiteralDef Literal)
        {
            var l = new List<String>();
            l.Add(Literal.Name);
            l.Add(Literal.Value.ToInvariantString());
            if (Literal.Description != "")
            {
                l.Add(Literal.Description);
            }
            return l;
        }

        private String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                return Type.TypeRef.VersionedName();
            }
            else if (Type.OnGenericParameterRef)
            {
                return "'" + Type.GenericParameterRef;
            }
            else if (Type.OnTuple)
            {
                return "Tuple<" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t))) + ">";
            }
            else if (Type.OnGenericTypeSpec)
            {
                return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ">";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private int CalculateCharWidth(String s)
        {
            return s.ToUTF32().Select(c => c.IsHalfWidth() ? 1 : 2).Sum();
        }
    }
}
