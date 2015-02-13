//==========================================================================
//
//  File:        RelationSchemaDiffWriter.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异写入器
//  Version:     2015.02.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using Yuki.RelationSchema;
using Yuki.RelationValue;

namespace Yuki.RelationSchemaDiff
{
    public sealed class RelationSchemaDiffWriter
    {
        public static void Write(StreamWriter sw, List<AlterEntity> l)
        {
            var Lines = new List<Syntax.TextLine>();
            Action<String[]> AddLine = Tokens =>
            {
                var Text = String.Join(" ", Tokens.Select(t => TreeFormatLiteralWriter.GetLiteral(t, true, false).SingleLine));
                Lines.Add(new Syntax.TextLine { Text = Text, Range = new Syntax.TextRange { Start = new Syntax.TextPosition { Row = 1, Column = 1, CharIndex = 0 }, End = new Syntax.TextPosition { Row = 1, Column = 1 + Text.Length, CharIndex = Text.Length } } });
            };

            foreach (var ae in l)
            {
                if (ae.Method.OnCreate)
                {
                    AddLine(new String[] { "Create", ae.EntityName });
                }
                else if (ae.Method.OnDelete)
                {
                    AddLine(new String[] { "Delete", ae.EntityName });
                }
                else if (ae.Method.OnRename)
                {
                    AddLine(new String[] { "Rename", ae.EntityName, ae.Method.Rename });
                }
                else if (ae.Method.OnField)
                {
                    var f = ae.Method.Field;
                    if (f.Method.OnCreate)
                    {
                        AddLine(new String[] { "Alter", ae.EntityName, "Create", f.FieldName, GetPrimitiveValString(f.Method.Create) });
                    }
                    else if (f.Method.OnDelete)
                    {
                        AddLine(new String[] { "Alter", ae.EntityName, "Delete", f.FieldName });
                    }
                    else if (f.Method.OnRename)
                    {
                        AddLine(new String[] { "Alter", ae.EntityName, "Rename", f.FieldName, f.Method.Rename });
                    }
                    else if (f.Method.OnChangeType)
                    {
                        AddLine(new String[] { "Alter", ae.EntityName, "ChangeType", f.FieldName });
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            var AlterNode = Syntax.MultiNodes.CreateFunctionNodes(new Syntax.FunctionNodes { FunctionDirective = new Syntax.FunctionDirective { Text = "Alter" }, Parameters = new Syntax.Token[] { }, Content = new Syntax.FunctionContent { IndentLevel = 0, Lines = Lines.ToArray() } });

            TreeFile.WriteRaw(sw, new Syntax.Forest { MultiNodesList = new Syntax.MultiNodes[] { AlterNode } });
        }

        private static String GetPrimitiveValString(PrimitiveVal v)
        {
            if (v.OnBooleanValue)
            {
                return v.BooleanValue.ToInvariantString();
            }
            else if (v.OnStringValue)
            {
                return v.StringValue;
            }
            else if (v.OnIntValue)
            {
                return v.IntValue.ToInvariantString();
            }
            else if (v.OnRealValue)
            {
                return v.RealValue.ToInvariantString();
            }
            else if (v.OnBinaryValue)
            {
                var ByteString = String.Join(" ", v.BinaryValue.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).ToArray());
                return ByteString;
            }
            else if (v.OnInt64Value)
            {
                return v.Int64Value.ToInvariantString();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
