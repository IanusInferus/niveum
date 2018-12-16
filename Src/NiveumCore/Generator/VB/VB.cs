//==========================================================================
//
//  File:        VB.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构VB.Net代码生成器
//  Version:     2016.10.03.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.VB
{
    public static class CodeGenerator
    {
        public static String CompileToVB(this Schema Schema, String NamespaceName, Boolean WithFirefly)
        {
            var t = new Templates(Schema);
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
        public Templates(Schema Schema)
        {
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
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.VersionedName()];
                    if (PlatformName.StartsWith("System.Collections.Generic."))
                    {
                        return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                    }
                }
                var Ref = Type.TypeRef;
                if ((Ref.NamespaceName() == NamespaceName) || NamespaceName.StartsWith(Ref.NamespaceName() + ".") || (Ref.NamespaceName() == ""))
                {
                    return GetEscapedIdentifier(Ref.SimpleName(Ref.NamespaceName()));
                }
                else
                {
                    return GetEscapedIdentifier(Ref.NamespaceName() + "." + Ref.SimpleName(Ref.NamespaceName()));
                }
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "Tuple(Of " + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t, NamespaceName))) + ")";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName) + "(Of " + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p, NamespaceName))) + ")";
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
        public TypeRef GetSuffixedTypeRef(List<String> Name, String Version, String Suffix)
        {
            return new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" };
        }
        public String GetSuffixedTypeString(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            var ts = TypeSpec.CreateTypeRef(new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" });
            return GetTypeString(ts, NamespaceName);
        }
        public String GetSuffixedTypeName(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            var ts = TypeSpec.CreateTypeRef(new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" });
            return ts.SimpleName(NamespaceName);
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
                return "(Of " + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + ")";
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
                    if ((Name != PlatformName) && (p.GenericParameters.Count() == 0))
                    {
                        l.AddRange(Primitive(Name, PlatformName));
                    }
                }
            }
            return l;
        }
        public List<String> GetTypes(Schema Schema, String NamespaceName)
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
                    if (c.VersionedName() == "Unit")
                    {
                        AddClass(c.NamespaceName(), Primitive_Unit());
                    }
                    else if (c.VersionedName() == "Optional")
                    {
                        AddClass(c.NamespaceName(), Primitive_Optional());
                    }
                    else
                    {
                        continue;
                    }
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
                    AddClass(c.NamespaceName(), ClientCommand(c.ClientCommand));
                }
                else if (c.OnServerCommand)
                {
                    AddClass(c.NamespaceName(), ServerCommand(c.ServerCommand));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToList();
            if (Commands.Count > 0)
            {
                AddClass(NamespaceName, IApplicationServer(Commands, NamespaceName));
                AddClass(NamespaceName, IApplicationClient(Commands, NamespaceName));
                AddClass(NamespaceName, IEventPump(Commands, NamespaceName));
            }

            var Classes = NamespaceToClasses.Select(p => WrapNamespace(p.Key, p.Value.Join(new String[] { "" })));

            return (new List<List<String>> { Primitives }).Concat(Classes).Join(new String[] { "" }).ToList();
        }
    }
}
