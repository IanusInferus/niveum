﻿//==========================================================================
//
//  File:        Python.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构Python3代码生成器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Python
{
    public static class CodeGenerator
    {
        public static String CompileToPython(this Schema Schema)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema).Select(Line => Line.TrimEnd(' '));
            return String.Join("\n", Lines);
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
        public String GetTypeString(TypeSpec Type, String NamespaceName, bool ForceNoQuote = false)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    return GetEscapedIdentifier(Type.TypeRef.VersionedName());
                }
                var Ref = Type.TypeRef;
                if ((Ref.NamespaceName() == NamespaceName) || NamespaceName.StartsWith(Ref.NamespaceName() + ".") || (Ref.NamespaceName() == ""))
                {
                    return (ForceNoQuote ? "" : "'") + GetEscapedIdentifier(Ref.SimpleName(Ref.NamespaceName())) + (ForceNoQuote ? "" : "'");
                }
                else
                {
                    throw new NotSupportedException("PythonMultipleNamespace"); //Python不支持nested class import
                    //return "'" + GetEscapedIdentifier(Ref.NamespaceName() + "." + Ref.SimpleName(Ref.NamespaceName())) + "'";
                }
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "Tuple[" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t, NamespaceName, ForceNoQuote))) + "]";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName, ForceNoQuote) + "[" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p, NamespaceName, ForceNoQuote))) + "]";
                }
                else
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName, ForceNoQuote);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public TypeRef GetSuffixedTypeRef(List<String> Name, String Version, String Suffix)
        {
            return new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" };
        }
        public String GetSuffixedTypeString(List<String> Name, String Version, String Suffix, String NamespaceName, bool ForceNoQuote = false)
        {
            var ts = TypeSpec.CreateTypeRef(new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" });
            return GetTypeString(ts, NamespaceName, ForceNoQuote);
        }
        public String GetSuffixedTypeName(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            var ts = TypeSpec.CreateTypeRef(new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" });
            return ts.SimpleName(NamespaceName);
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
                if (PrimitiveMapping.ContainsKey(p.VersionedName()))
                {
                    var Name = p.DefinitionName();
                    var PlatformName = PrimitiveMapping[p.VersionedName()];
                    if (Name != PlatformName)
                    {
                        l.AddRange(Primitive(Name, PlatformName));
                    }
                }
            }
            return l;
        }

        public List<String> GetTypes(Schema Schema)
        {
            var Primitives = GetPrimitives(Schema);

            var NamespaceToClasses = new Dictionary<String, List<List<String>>>();
            void AddClass(String ClassNamespaceName, IEnumerable<String> ClassContent)
            {
                if (!NamespaceToClasses.ContainsKey(ClassNamespaceName))
                {
                    NamespaceToClasses.Add(ClassNamespaceName, new List<List<String>>());
                }
                NamespaceToClasses[ClassNamespaceName].Add(ClassContent.ToList());
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    continue;
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
                else if (c.OnEnum)
                {
                    AddClass(c.NamespaceName(), Enum(c.Enum));
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
            }

            if (NamespaceToClasses.Count > 1)
            {
                throw new NotSupportedException("PythonMultipleNamespace"); //Python不支持nested class import
            }

            var Classes = NamespaceToClasses.Select(p => p.Value.Join(new String[] { "" }));

            return (new List<List<String>> { Primitives }).Concat(Classes).Join(new String[] { "" }).ToList();
        }
    }
}
