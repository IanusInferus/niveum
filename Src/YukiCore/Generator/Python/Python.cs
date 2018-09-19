﻿//==========================================================================
//
//  File:        Python.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Python3代码生成器
//  Version:     2017.07.18.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yuki.ObjectSchema.Python
{
    public static class CodeGenerator
    {
        public static String CompileToPython(this Schema Schema)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
    }

    public partial class Templates
    {
        public Templates(Schema Schema)
        {
            foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
            {
                if (!t.OnPrimitive && (t.GenericParameters().Count() > 0))
                {
                    throw new InvalidOperationException(String.Format("GenericParametersNotSupported: {0}", t.VersionedName()));
                }
                if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && PrimitiveMapping.ContainsKey(gp.Type.TypeRef.Name) && gp.Type.TypeRef.Name == "Type"))
                {
                    throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                }
            }

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
        }

        private Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
        public String GetEscapedIdentifier(String Identifier)
        {
            return rIdentifierPart.Replace(Identifier, m =>
            {
                var IdentifierPart = m.Value;
                if (Keywords.Contains(IdentifierPart))
                {
                    return IdentifierPart + "_";
                }
                else
                {
                    return IdentifierPart;
                }
            }).Replace("<", "[").Replace(">", "]");
        }
        public String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                var TypeName = Type.TypeFriendlyName();
                if (PrimitiveMapping.ContainsKey(TypeName))
                {
                    return GetEscapedIdentifier(TypeName);
                }
                return "'" + GetEscapedIdentifier(TypeName) + "'";
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "Tuple[" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t))) + "]";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "[" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + "]";
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

            var d = Description;

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
        public String GetGenericParameters(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0)
            {
                return "";
            }
            else
            {
                return "[" + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + "]";
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
                    if (Name != PlatformName)
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

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    continue;
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
                }
                else if (c.OnServerCommand)
                {
                }
                else
                {
                    throw new InvalidOperationException();
                }
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