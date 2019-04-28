//==========================================================================
//
//  File:        RelationSchemaDiffWriter.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构差异写入器
//  Version:     2019.04.28.
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
        public static void Write(StreamWriter sw, List<EntityMapping> l)
        {
            Write(sw, new EntityMappingDiff { Mappings = l, DeletedEntities = new HashSet<String>(), DeletedFields = new Dictionary<String, HashSet<String>>() });
        }
        public static void Write(StreamWriter sw, EntityMappingDiff d)
        {
            var Lines = new List<Syntax.TextLine>();
            Action<List<String>> AddLine = Tokens =>
            {
                var Text = String.Join(" ", Tokens.Select(t => t == null ? "$Empty" : TreeFormatLiteralWriter.GetLiteral(t, true, false).SingleLine));
                Lines.Add(new Syntax.TextLine { Text = Text, Range = new Syntax.TextRange { Start = new Syntax.TextPosition { Row = 1, Column = 1, CharIndex = 0 }, End = new Syntax.TextPosition { Row = 1, Column = 1 + Text.Length, CharIndex = Text.Length } } });
            };
            Action<List<String>> AddCommentLine = Tokens =>
            {
                var Text = "//" + String.Join(" ", Tokens.Select(t => t == null ? "$Empty" : TreeFormatLiteralWriter.GetLiteral(t, true, false).SingleLine));
                Lines.Add(new Syntax.TextLine { Text = Text, Range = new Syntax.TextRange { Start = new Syntax.TextPosition { Row = 1, Column = 1, CharIndex = 0 }, End = new Syntax.TextPosition { Row = 1, Column = 1 + Text.Length, CharIndex = Text.Length } } });
            };

            foreach (var m in d.Mappings)
            {
                if (m.Method.OnNew)
                {
                    AddLine(new List<String> { "Entity", m.EntityName, "New" });
                }
                else if (m.Method.OnCopy)
                {
                    AddLine(new List<String> { "Entity", m.EntityName, "From", m.Method.Copy });
                }
                else if (m.Method.OnField)
                {
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var m in d.Mappings)
            {
                if (m.Method.OnNew)
                {
                }
                else if (m.Method.OnCopy)
                {
                }
                else if (m.Method.OnField)
                {
                    var f = m.Method.Field;
                    if (f.Method.OnNew)
                    {
                        AddLine(new List<String> { "Entity", m.EntityName, "Field", f.FieldName, "New", GetPrimitiveValString(f.Method.New) });
                    }
                    else if (f.Method.OnCopy)
                    {
                        AddLine(new List<String> { "Entity", m.EntityName, "Field", f.FieldName, "From", f.Method.Copy });
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var de in d.DeletedEntities)
            {
                AddCommentLine(new List<String> { "Entity", de, "Delete" });
            }
            foreach (var de in d.DeletedFields)
            {
                foreach (var df in de.Value)
                {
                    AddCommentLine(new List<String> { "Entity", de.Key, "Field", df, "Delete" });
                }
            }

            var AlterNode = Syntax.MultiNodes.CreateFunctionNodes(new Syntax.FunctionNodes { FunctionDirective = new Syntax.FunctionDirective { Text = "Map" }, Parameters = new List<Syntax.Token> { }, Content = new Syntax.FunctionContent { IndentLevel = 0, Lines = Lines } });

            TreeFile.WriteRaw(sw, new Syntax.Forest { MultiNodesList = new List<Syntax.MultiNodes> { AlterNode } });
        }

        private static String GetPrimitiveValString(Optional<PrimitiveVal> v)
        {
            if (v.OnSome)
            {
                return GetPrimitiveValString(v.Some);
            }
            else
            {
                return null;
            }
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
