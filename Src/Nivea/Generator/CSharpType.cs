//==========================================================================
//
//  File:        CSharpType.cs
//  Location:    Nivea <Visual C#>
//  Description: C#类型代码生成
//  Version:     2018.12.01.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nivea.Template.Semantics;

namespace Nivea.Generator.CSharpType
{
    internal partial class Templates
    {
        private HashSet<String> Keywords = new HashSet<String> { "abstract", "event", "new", "struct", "as", "explicit", "null", "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw", "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float", "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected", "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe", "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal", "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate", "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock", "stackalloc", "else", "long", "static", "enum", "namespace", "string", "get", "partial", "set", "value", "where", "yield" };
        private Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String> { { "Unit", "Unit" }, { "Boolean", "System.Boolean" }, { "String", "System.String" }, { "Int", "System.Int32" }, { "Real", "System.Double" }, { "Byte", "System.Byte" }, { "UInt8", "System.Byte" }, { "UInt16", "System.UInt16" }, { "UInt32", "System.UInt32" }, { "UInt64", "System.UInt64" }, { "Int8", "System.SByte" }, { "Int16", "System.Int16" }, { "Int32", "System.Int32" }, { "Int64", "System.Int64" }, { "Float32", "System.Single" }, { "Float64", "System.Double" }, { "Type", "System.Type" }, { "Optional", "Optional" }, { "List", "System.Collections.Generic.List" }, { "Set", "System.Collections.Generic.HashSet" }, { "Map", "System.Collections.Generic.Dictionary" } };
        private Dictionary<String, String> PrimitiveMappingEnum = new Dictionary<String, String> { { "Int", "int" }, { "Byte", "byte" }, { "UInt8", "byte" }, { "UInt16", "ushort" }, { "UInt32", "uint" }, { "UInt64", "ulong" }, { "Int8", "sbyte" }, { "Int16", "short" }, { "Int32", "int" }, { "Int64", "long" } };
        private Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
        public String GetEscapedIdentifier(String Identifier)
        {
            return rIdentifierPart.Replace(Identifier, m =>
            {
                var IdentifierPart = m.Value;
                if (Keywords.Contains(IdentifierPart))
                {
                    return "@" + IdentifierPart;
                }
                else
                {
                    return IdentifierPart;
                }
            });
        }
        public String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (Type.TypeRef.Name.Count == 1)
                {
                    var SingleName = Type.TypeRef.Name.Single();
                    if (PrimitiveMapping.ContainsKey(SingleName))
                    {
                        var PlatformName = PrimitiveMapping[SingleName];
                        if (PlatformName.StartsWith("System.Collections.Generic."))
                        {
                            return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                        }
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
                return "Tuple<" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t))) + ">";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ">";
                }
                else
                {
                    return GetEscapedIdentifier(Type.TypeFriendlyName());
                }
            }
            else if (Type.OnArray)
            {
                return GetTypeString(Type.Array) + "[]";
            }
            else if (Type.OnMember)
            {
                return GetTypeString(Type.Member.Parent) + "." + GetTypeString(Type.Member.Child);
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
            if (!Type.OnTypeRef)
            {
                throw new InvalidOperationException();
            }
            if ((Type.TypeRef.Name.Count == 1) && (Type.TypeRef.Version == "") && PrimitiveMappingEnum.ContainsKey(Type.TypeRef.Name.Single()))
            {
                return PrimitiveMappingEnum[Type.TypeRef.Name.Single()];
            }
            return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
        }
        public String GetGenericParameters(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0)
            {
                return "";
            }
            else
            {
                return "<" + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + ">";
            }
        }
    }
}
