//==========================================================================
//
//  File:        VB.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构VB.Net代码生成器
//  Version:     2016.09.10.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yuki.ObjectSchema.VB
{
    public static class CodeGenerator
    {
        public static String CompileToVB(this Schema Schema, String NamespaceName, Boolean WithFirefly)
        {
            var t = new Templates();
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToVB(this Schema Schema)
        {
            return CompileToVB(Schema, "", true);
        }
    }

    public partial class Templates
    {
        private Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
        public String GetEscapedIdentifier(String Identifier)
        {
            return rIdentifierPart.Replace(Identifier, m =>
            {
                var IdentifierPart = m.Value;
                if (Keywords.Contains(IdentifierPart))
                {
                    return "[" + IdentifierPart + "]";
                }
                else
                {
                    return IdentifierPart;
                }
            }).Replace("<", "(Of ").Replace(">", ")");
        }
        public String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\"' ? "\"\"" : c == '\r' ? "\" + Microsoft.VisualBasic.ChrW(13) + \"" : c == '\n' ? "\" + Microsoft.VisualBasic.ChrW(10) + \"" : new String(c, 1)).ToArray()) + "\"";
        }
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.Name))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.Name];
                    if (PlatformName.StartsWith("System.Collections.Generic."))
                    {
                        return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                    }
                }
                return GetEscapedIdentifier(Type.TypeFriendlyName());
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "Tuple(Of " + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t))) + ")";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "(Of " + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ")";
                }
                else
                {
                    return GetEscapedIdentifier(Type.TypeFriendlyName());
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public IEnumerable<String> GetXmlComment(String Description)
        {
            if (Description == "") { return new List<String> { }; }

            var d = Description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

            var Lines = d.Replace("\r\n", "\n").Split('\n').ToList();
            if (Lines.Count == 1)
            {
                return SingleLineXmlComment(d);
            }
            else
            {
                return MultiLineXmlComment(Lines);
            }
        }
        public String GetEnumTypeString(TypeSpec Type)
        {
            return GetTypeString(Type);
        }
        public String GetGenericParameters(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0)
            {
                return "";
            }
            else
            {
                return "(Of " + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + ")";
            }
        }

        public List<String> GetPrimitives(Schema Schema)
        {
            var l = new List<String>();

            foreach (var p in Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnPrimitive).Select(c => c.Primitive))
            {
                if (PrimitiveMapping.ContainsKey(p.Name))
                {
                    var Name = p.TypeFriendlyName();
                    var PlatformName = PrimitiveMapping[p.Name];
                    if ((Name != PlatformName) && (p.GenericParameters.Count() == 0))
                    {
                        l.AddRange(Primitive(Name, PlatformName));
                    }
                }
            }
            return l;
        }
        public List<String> GetComplexTypes(Schema Schema)
        {
            var l = new List<String>();

            List<TypeDef> cl = new List<TypeDef>();

            foreach (var c in Schema.Types)
            {
                if (!c.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                {
                    continue;
                }

                if (c.OnPrimitive)
                {
                    if (c.VersionedName() == "Unit")
                    {
                        l.AddRange(Primitive_Unit());
                    }
                    else if (c.VersionedName() == "Optional")
                    {
                        l.AddRange(Primitive_Optional());
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (c.OnAlias)
                {
                    l.AddRange(Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(TaggedUnion(c.TaggedUnion));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(Enum(c.Enum));
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(ClientCommand(c.ClientCommand));
                    cl.Add(c);
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(ServerCommand(c.ServerCommand));
                    cl.Add(c);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                l.Add("");
            }

            if (cl.Count > 0)
            {
                l.AddRange(IApplicationServer(cl));
                l.Add("");
                l.AddRange(IApplicationClient(cl));
                l.Add("");
                l.AddRange(IEventPump(cl));
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
