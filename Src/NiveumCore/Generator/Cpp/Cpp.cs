//==========================================================================
//
//  File:        Cpp.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C++代码生成器
//  Version:     2019.08.23.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Cpp
{
    public static class CodeGenerator
    {
        public static String CompileToCpp(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCpp(this Schema Schema)
        {
            return CompileToCpp(Schema, "");
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

            AliasSet = new HashSet<String>(Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnAlias).Select(c => c.VersionedName()).Distinct());
            EnumSet = new HashSet<String>(Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).Select(c => c.VersionedName()).Distinct());
        }

        private Regex rIdentifierPart = new Regex(@"typename |class |struct |union |enum struct |enum class |enum |[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
        public String GetEscapedIdentifier(String Identifier)
        {
            return rIdentifierPart.Replace(Identifier, m =>
            {
                var IdentifierPart = m.Value;
                if (Keywords.Contains(IdentifierPart))
                {
                    return "_" + IdentifierPart;
                }
                else
                {
                    return IdentifierPart;
                }
            }).Replace(".", "::");
        }
        public String GetEscapedStringLiteral(String s)
        {
            return "u\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        private HashSet<String> AliasSet = new HashSet<String>();
        private HashSet<String> EnumSet = new HashSet<String>();
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
        public String GetTypeString(TypeSpec Type, String NamespaceName, Boolean NoElaboratedTypeSpecifier = false, Boolean ForceAsEnum = false, Boolean ForceAsValue = false)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.VersionedName()];
                    if (Type.TypeRef.NameMatches("Optional", "List", "Set", "Map"))
                    {
                        return PlatformName;
                    }
                    else
                    {
                        return GetTypeString(Type.TypeRef, NamespaceName);
                    }
                }
                else if (AliasSet.Contains(Type.TypeRef.VersionedName()))
                {
                    return (NoElaboratedTypeSpecifier ? "" : "class ") + GetTypeString(Type.TypeRef, NamespaceName);
                }
                else if (EnumSet.Contains(Type.TypeRef.VersionedName()))
                {
                    return (NoElaboratedTypeSpecifier ? "" : "enum ") + GetTypeString(Type.TypeRef, NamespaceName);
                }
                if (ForceAsEnum)
                {
                    return (NoElaboratedTypeSpecifier ? "" : "enum ") + GetTypeString(Type.TypeRef, NamespaceName);
                }
                else if (ForceAsValue)
                {
                    return (NoElaboratedTypeSpecifier ? "" : "class ") + GetTypeString(Type.TypeRef, NamespaceName);
                }
                return "std::shared_ptr<class " + GetTypeString(Type.TypeRef, NamespaceName) + ">";
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "std::tuple<" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t, NamespaceName))) + ">";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    var TypeString = GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName, ForceAsValue: true) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p, NamespaceName))) + ">";
                    if (ForceAsValue)
                    {
                        return TypeString;
                    }
                    if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("List") && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Set") && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Map") && Type.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        return TypeString;
                    }
                    return "std::shared_ptr<" + TypeString + ">";
                }
                else
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName, ForceAsValue: ForceAsValue);
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
        public String GetSuffixedTypeString(List<String> Name, String Version, String Suffix, String NamespaceName, Boolean NoElaboratedTypeSpecifier = false, Boolean ForceAsEnum = false, Boolean ForceAsValue = false)
        {
            var ts = TypeSpec.CreateTypeRef(new TypeRef { Name = Name.NameConcat((Version == "" ? "" : "At" + Version) + Suffix), Version = "" });
            return GetTypeString(ts, NamespaceName, NoElaboratedTypeSpecifier, ForceAsEnum, ForceAsValue);
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
                return "<" + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + ">";
            }
        }
        public List<String> GetGenericParameterLine(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0) { return new List<String> { }; }
            return new List<String> { "template<" + String.Join(", ", GenericParameters.Select(gp => "typename " + gp.Name)) + ">" };
        }

        public List<String> GetPrimitives(Schema Schema)
        {
            var l = new List<String>();

            foreach (var c in Schema.TypeRefs.Concat(Schema.Types))
            {
                if (c.OnPrimitive)
                {
                    if (c.VersionedName() == "Unit")
                    {
                        l.AddRange(Primitive_Unit());
                    }
                    else if (c.Primitive.GenericParameters.Count == 0)
                    {
                        var Name = c.Primitive.VersionedName();
                        if (PrimitiveMapping.ContainsKey(Name))
                        {
                            var PlatformName = PrimitiveMapping[Name];
                            if (Name != PlatformName)
                            {
                                l.AddRange(Primitive(Name, PlatformName));
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetTypePredefinition(TypeDef t)
        {
            var Name = t.DefinitionName();
            var GenericParameterLine = GetGenericParameterLine(t.GenericParameters());
            String MetaType = "class";
            if (t.OnPrimitive)
            {
                return new List<String> { };
            }
            else if (t.OnAlias || t.OnRecord || t.OnTaggedUnion || t.OnClientCommand || t.OnServerCommand)
            {
                MetaType = "class";
            }
            else if (t.OnEnum)
            {
                return new List<String> { };
            }
            if (t.OnClientCommand)
            {
                var c = t.ClientCommand;
                var RequestRef = GetSuffixedTypeRef(c.Name, c.Version, "Request");
                var ReplyRef = GetSuffixedTypeRef(c.Name, c.Version, "Reply");
                var Request = new RecordDef { Name = RequestRef.Name, Version = RequestRef.Version, GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description };
                var Reply = new TaggedUnionDef { Name = ReplyRef.Name, Version = ReplyRef.Version, GenericParameters = new List<VariableDef> { }, Alternatives = c.InParameters, Attributes = c.Attributes, Description = c.Description };
                return TypePredefinition(Request.DefinitionName(), MetaType, t.GenericParameters()).Concat(TypePredefinition(Reply.DefinitionName(), MetaType, t.GenericParameters()));
            }
            else if (t.OnServerCommand)
            {
                var c = t.ServerCommand;
                var EventRef = GetSuffixedTypeRef(c.Name, c.Version, "Event");
                var Event = new RecordDef { Name = EventRef.Name, Version = EventRef.Version, GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description };
                return TypePredefinition(Event.DefinitionName(), MetaType, t.GenericParameters());
            }
            else
            {
                return TypePredefinition(Name, MetaType, t.GenericParameters());
            }
        }

        public List<String> GetTypes(Schema Schema, String NamespaceName)
        {
            var Primitives = GetPrimitives(Schema);

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
                if (c.OnEnum)
                {
                    AddClass(c.NamespaceName(), Enum(c.Enum));
                }
                else
                {
                    continue;
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
                    continue;
                }

                AddClass(c.NamespaceName(), GetTypePredefinition(c));
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnAlias)
                {
                    AddClass(c.NamespaceName(), Alias(c.Alias));
                }
                else
                {
                    continue;
                }
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnEnum)
                {
                    AddClass("std", EnumFunctor(c.Enum));
                }
                else
                {
                    continue;
                }
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    continue;
                }
                else if (c.OnAlias)
                {
                    continue;
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
                    continue;
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

        public Boolean IsInclude(String s)
        {
            if (s.StartsWith("<") && s.EndsWith(">")) { return true; }
            if (s.StartsWith(@"""") && s.EndsWith(@"""")) { return true; }
            return false;
        }
    }
}
