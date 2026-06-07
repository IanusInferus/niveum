//==========================================================================
//
//  File:        Rust.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构Rust代码生成器
//  Version:     2026.06.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Rust
{
    public static class CodeGenerator
    {
        public static String CompileToRust(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\n", Lines);
        }
        public static String CompileToRust(this Schema Schema)
        {
            return CompileToRust(Schema, "");
        }
    }

    public partial class Templates
    {
        private Schema Schema;
        public Templates(Schema Schema)
        {
            this.Schema = Schema;
            foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
            {
                if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.NameMatches(Name => PrimitiveMapping.ContainsKey(Name) && Name == "Type")))
                {
                    throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                }
            }

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.NameMatches(Name => Name == "Unit")).Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.NameMatches(Name => Name == "Boolean")).Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
        }

        private Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
        public String GetEscapedIdentifier(String Identifier)
        {
            return rIdentifierPart.Replace(Identifier, m =>
            {
                var IdentifierPart = m.Value;
                if (Keywords.Contains(IdentifierPart))
                {
                    return "r#" + IdentifierPart;
                }
                else
                {
                    return IdentifierPart;
                }
            }).Replace(".", "::");
        }
        public String GetEscapedStringLiteral(String s)
        {
            var Escaped = s.SelectMany(c => c switch
            {
                '\\' => "\\\\",   // Backslash
                '\"' => "\\\"",   // Double quote
                '\r' => "\\r",    // Carriage return
                '\n' => "\\n",    // Newline
                '\t' => "\\t",    // Tab
                '\0' => "\\0",    // Null byte

                // Catch any other hidden ASCII control characters (0x00 to 0x1F)
                _ when c < 0x20 => $"\\x{(int)c:x2}",

                // Everything else (including valid unicode like emojis) stays raw
                _ => c.ToString()
            });

            return "\"" + new String(Escaped.ToArray()) + "\"";
        }

        private String GetTypeString(TypeRef Ref, String NamespaceName)
        {
            if ((Ref.NamespaceName() == NamespaceName) || NamespaceName.StartsWith(Ref.NamespaceName() + ".") || (Ref.NamespaceName() == ""))
            {
                return GetEscapedIdentifier(Ref.SimpleName(Ref.NamespaceName()));
            }
            else
            {
                return GetEscapedIdentifier(Ref.NamespaceName().Replace(".", "::") + "::" + Ref.SimpleName(Ref.NamespaceName()));
            }
        }
        public String GetBoxedTypeString(TypeSpec Type, String NamespaceName, String EnclosingVersionedName)
        {
            var ts = GetTypeString(Type, NamespaceName);
            var scg = Schema.GetSchemaClosureGenerator();
            var Closure = scg.GetClosure(new List<TypeDef> { }, new List<TypeSpec> { Type });
            if (Closure.TypeDefs.Any(td => td.VersionedName() == EnclosingVersionedName))
            {
                return "Box<" + ts + ">";
            }
            return ts;
        }
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.VersionedName()];
                    return PlatformName;
                }
                else
                {
                    return GetTypeString(Type.TypeRef, NamespaceName);
                }
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "(" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t, NamespaceName))) + ")";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    var TypeString = GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName);
                    return TypeString + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p, NamespaceName))) + ">";
                }
                else
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public IEnumerable<String> GetDocComment(String Description)
        {
            if (Description == "") { return new List<String> { }; }

            var d = Description.Replace("`", "&grave;");

            return d.Replace("\r\n", "\n").Split('\n').Select(line => "/// " + line).ToList();
        }
        public String GetEnumTypeString(TypeSpec Type, String NamespaceName)
        {
            return GetTypeString(Type, NamespaceName);
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
        public List<String> GetGenericParameterLine(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0) { return new List<String> { }; }
            return new List<String> { "" };
        }

        public List<String> GetTypes(Schema Schema, String NamespaceName)
        {
            var NamespaceToClasses = new List<KeyValuePair<String, List<List<String>>>>();
            void AddClass(String ClassNamespaceName, IEnumerable<String> ClassContent)
            {
                if ((NamespaceToClasses.Count > 0) && (NamespaceToClasses[NamespaceToClasses.Count - 1].Key == ClassNamespaceName))
                {
                    NamespaceToClasses[NamespaceToClasses.Count - 1].Value.Add(ClassContent.ToList());
                }
                else
                {
                    NamespaceToClasses.Add(new KeyValuePair<String, List<List<String>>>(ClassNamespaceName, new List<List<String>> { ClassContent.ToList() }));
                }
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    continue;
                }
                else if (c.OnEnum)
                {
                    AddClass(c.NamespaceName(), Enum(c.Enum));
                }
                else if (c.OnAlias)
                {
                    AddClass(c.NamespaceName(), Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    AddClass(c.NamespaceName(), Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    AddClass(c.NamespaceName(), TaggedUnion(c.TaggedUnion));
                }
                else
                {
                    continue;
                }
            }

            var Classes = NamespaceToClasses.Select(p => WrapNamespace(p.Key, p.Value.Join(new String[] { "" })));

            return Classes.Join(new String[] { "" }).ToList();
        }

        public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
        {
            var c = Contents;
            if (Namespace != "")
            {
                foreach (var nn in Namespace.Split('.').Reverse())
                {
                    c = WrapNamespacePart(nn, c).ToList();
                }
            }
            return c;
        }
    }
}
