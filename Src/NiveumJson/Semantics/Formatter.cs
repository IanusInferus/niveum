//==========================================================================
//
//  File:        Formatter.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 格式化器
//  Version:     2023.04.04.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace Niveum.Json.Semantics
{
    public static class Formatter
    {
        public static String ToString(JToken Token, Formatting Formatting)
        {
            var Output = new StringBuilder();
            Write(Token, Formatting, Output, 0, true);
            return Output.ToString();
        }
        private static void Write(JToken Token, Formatting Formatting, StringBuilder Output, int Level, bool FirstInLine)
        {
            if (FirstInLine)
            {
                if (Formatting == Formatting.Indented)
                {
                    for (int k = 0; k < Level; k += 1)
                    {
                        Output.Append("    ");
                    }
                }
            }
            if (Token.Type == JTokenType.Null)
            {
                Output.Append("null");
            }
            else if (Token.Type == JTokenType.Boolean)
            {
                if ((bool)((Token as JValue).Value))
                {
                    Output.Append("true");
                }
                else
                {
                    Output.Append("false");
                }
            }
            else if (Token.Type == JTokenType.Number)
            {
                Output.Append(((double)((Token as JValue).Value)).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (Token.Type == JTokenType.String)
            {
                WriteString((String)((Token as JValue).Value), Output);
            }
            else if (Token.Type == JTokenType.Object)
            {
                Output.Append("{");
                if (Formatting == Formatting.Indented)
                {
                    Output.Append("\r\n");
                }
                var Count = ((JObject)(Token)).Count;
                var i = 0;
                foreach (var p in (JObject)(Token))
                {
                    if (Formatting == Formatting.Indented)
                    {
                        for (int k = 0; k < Level + 1; k += 1)
                        {
                            Output.Append("    ");
                        }
                    }
                    WriteString(p.Key, Output);
                    Output.Append(":");
                    if (Formatting == Formatting.Indented)
                    {
                        Output.Append(" ");
                    }
                    Write(p.Value, Formatting, Output, Level + 1, false);
                    if (i != Count - 1)
                    {
                        Output.Append(",");
                    }
                    if (Formatting == Formatting.Indented)
                    {
                        Output.Append("\r\n");
                    }
                    i += 1;
                }
                if (Formatting == Formatting.Indented)
                {
                    for (int k = 0; k < Level; k += 1)
                    {
                        Output.Append("    ");
                    }
                }
                Output.Append("}");
            }
            else if (Token.Type == JTokenType.Array)
            {
                Output.Append("[");
                if (Formatting == Formatting.Indented)
                {
                    Output.Append("\r\n");
                }
                var Count = ((JArray)(Token)).Count;
                var i = 0;
                foreach (var v in (JArray)(Token))
                {
                    if (Formatting == Formatting.Indented)
                    {
                        for (int k = 0; k < Level + 1; k += 1)
                        {
                            Output.Append("    ");
                        }
                    }
                    Write(v, Formatting, Output, Level + 1, false);
                    if (i != Count - 1)
                    {
                        Output.Append(",");
                    }
                    if (Formatting == Formatting.Indented)
                    {
                        Output.Append("\r\n");
                    }
                    i += 1;
                }
                if (Formatting == Formatting.Indented)
                {
                    for (int k = 0; k < Level; k += 1)
                    {
                        Output.Append("    ");
                    }
                }
                Output.Append("]");
            }
        }
        private static void WriteString(String s, StringBuilder Output)
        {
            Output.Append("\"");
            foreach (var c in s)
            {
                if (c == '"')
                {
                    Output.Append("\\\"");
                }
                else if (c == '\\')
                {
                    Output.Append(@"\\");
                }
                else if (c == '\b')
                {
                    Output.Append(@"\b");
                }
                else if (c == '\f')
                {
                    Output.Append(@"\f");
                }
                else if (c == '\n')
                {
                    Output.Append(@"\n");
                }
                else if (c == '\r')
                {
                    Output.Append(@"\r");
                }
                else if (c == '\t')
                {
                    Output.Append(@"\t");
                }
                else if ((c >= '\0') && (c <= '\u001F'))
                {
                    // the control characters (U+0000 through U+001F) https://www.rfc-editor.org/rfc/rfc7159
                    Output.Append("\\u" + String.Format("{0:X4}", (UInt16)(c)));
                }
                else
                {
                    Output.Append(c);
                }
            }
            Output.Append("\"");
        }
    }
}
