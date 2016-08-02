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
        private Dictionary<String, String> PrimitiveMappings = new Dictionary<String, String> { { "Unit", "Nivea.Unit" }, { "Boolean", "System.Boolean" }, { "String", "System.String" }, { "Int", "System.Int32" }, { "Real", "System.Double" }, { "Byte", "System.Byte" }, { "UInt8", "System.Byte" }, { "UInt16", "System.UInt16" }, { "UInt32", "System.UInt32" }, { "UInt64", "System.UInt64" }, { "Int8", "System.SByte" }, { "Int16", "System.Int16" }, { "Int32", "System.Int32" }, { "Int64", "System.Int64" }, { "Float32", "System.Single" }, { "Float64", "System.Double" }, { "Type", "System.Type" }, { "Optional", "Nivea.Optional" }, { "List", "System.Collections.Generic.List" }, { "Set", "System.Collections.Generic.HashSet" }, { "Map", "System.Collections.Generic.Dictionary" } };
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
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                {
                    var PlatformName = PrimitiveMappings[Type.TypeRef.Name];
                    if (PlatformName.StartsWith("System.Collections.Generic."))
                    {
                        return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                    }
                }
                return Type.TypeFriendlyName();
            }
            else if (Type.OnGenericParameterRef)
            {
                return Type.GenericParameterRef;
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
                    return Type.TypeFriendlyName();
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
            if (!PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
            {
                return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
            }
            switch (PrimitiveMappings[Type.TypeRef.Name])
            {
                case "System.UInt8":
                    return "byte";
                case "System.UInt16":
                    return "ushort";
                case "System.UInt32":
                    return "uint";
                case "System.UInt64":
                    return "ulong";
                case "System.Int8":
                    return "sbyte";
                case "System.Int16":
                    return "short";
                case "System.Int32":
                    return "int";
                case "System.Int64":
                    return "long";
                default:
                    return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
            }
        }
    }
}
