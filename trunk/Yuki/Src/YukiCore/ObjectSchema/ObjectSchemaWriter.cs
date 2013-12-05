//==========================================================================
//
//  File:        ObjectSchemaWriter.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构写入器
//  Version:     2013.12.05.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;

namespace Yuki.ObjectSchema
{
    public class ObjectSchemaWriter
    {
        public ObjectSchemaWriter()
        {
        }

        public String Write(TypeDef[] Types)
        {
            var f = WriteToForest(Types);

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

        private Syntax.Forest WriteToForest(TypeDef[] Types)
        {
            var MultiNodesList = new List<Syntax.MultiNodes>();
            foreach (var t in Types)
            {
                var LineTokens = new List<String[]>();
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    LineTokens.AddRange(p.GenericParameters.Select(v => WriteToTokens(v, true)));
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    LineTokens.Add(new String[] { GetTypeString(a.Type) });
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
                    LineTokens.Add(new String[] { ">" });
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
                var LineLiterals = LineTokens.Select(l => l.Select(Token => TreeFormatLiteralWriter.GetLiteral(Token, true, false).SingleLine).ToArray()).ToArray();
                var NumColumn = 0;
                foreach (var l in LineLiterals)
                {
                    NumColumn = Math.Max(NumColumn, l.Length);
                }
                var ColumnWidths = new List<int>();
                for (int k = 0; k <= NumColumn - 2; k += 1)
                {
                    int Width = 28;
                    foreach (var l in LineLiterals)
                    {
                        if (k < l.Length)
                        {
                            var Column = l[k];
                            Width = Math.Max(Width, CalculateCharWidth(Column).CeilToMultipleOf(4) + 4);
                        }
                    }
                    ColumnWidths.Add(Width);
                }
                var Lines = new List<Syntax.TextLine>();
                foreach (var l in LineTokens)
                {
                    var Line = new List<String>();
                    for (int k = 0; k < l.Length; k += 1)
                    {
                        var Column = l[k];
                        if ((k != l.Length - 1) && (k < ColumnWidths.Count))
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
                var fn = WriteToFunctionNodes(t._Tag.ToString(), t.VersionedName(), t.Description(), Lines.ToArray());
                MultiNodesList.Add(Syntax.MultiNodes.CreateFunctionNodes(fn));
            }
            return new Syntax.Forest { MultiNodesList = MultiNodesList.ToArray() };
        }

        private Syntax.FunctionNodes WriteToFunctionNodes(String Directive, String Name, String Description, Syntax.TextLine[] Lines)
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
                Parameters = Parameters.ToArray(),
                SingleLineComment = Opt<Syntax.SingleLineComment>.Empty,
                Content = new Syntax.FunctionContent { IndentLevel = 0, Lines = Lines },
                EndDirective = Opt<Syntax.EndDirective>.Empty
            };
            return fn;
        }

        private String[] WriteToTokens(VariableDef v, bool IsGenericParameter)
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
            return l.ToArray();
        }

        private String[] WriteToTokens(LiteralDef Literal)
        {
            var l = new List<String>();
            l.Add(Literal.Name);
            l.Add(Literal.Value.ToInvariantString());
            if (Literal.Description != "")
            {
                l.Add(Literal.Description);
            }
            return l.ToArray();
        }

        private String GetTypeString(TypeSpec Type)
        {
            switch (Type._Tag)
            {
                case TypeSpecTag.TypeRef:
                    return Type.TypeRef.VersionedName();
                case TypeSpecTag.GenericParameterRef:
                    return "'" + Type.GenericParameterRef.Value;
                case TypeSpecTag.Tuple:
                    return "Tuple<" + String.Join(", ", Type.Tuple.Types.Select(t => GetTypeString(t)).ToArray()) + ">";
                case TypeSpecTag.GenericTypeSpec:
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => GetTypeString(p.TypeSpec)).ToArray()) + ">";
                default:
                    throw new InvalidOperationException();
            }
        }

        private int CalculateCharWidth(String s)
        {
            return s.ToUTF32().Select(c => c.IsHalfWidth() ? 1 : 2).Sum();
        }
    }
}
