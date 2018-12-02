//==========================================================================
//
//  File:        Cpp.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C++代码生成器
//  Version:     2018.08.20.
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
                if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && PrimitiveMapping.ContainsKey(gp.Type.TypeRef.Name) && gp.Type.TypeRef.Name == "Type"))
                {
                    throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                }
            }

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }

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
            return "L\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        private HashSet<String> AliasSet = new HashSet<String>();
        private HashSet<String> EnumSet = new HashSet<String>();
        public String GetTypeString(TypeSpec Type, Boolean ForceAsValue = false)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.Name))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.Name];
                    if (Type.TypeRef.Name == "List" || Type.TypeRef.Name == "Set" || Type.TypeRef.Name == "Map")
                    {
                        return PlatformName;
                    }
                    else
                    {
                        return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
                    }
                }
                else if (AliasSet.Contains(Type.TypeRef.VersionedName()))
                {
                    return "class " + GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
                }
                else if (EnumSet.Contains(Type.TypeRef.VersionedName()))
                {
                    return "_ENUM_CLASS_ " + GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
                }
                if (ForceAsValue)
                {
                    return "class " + GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
                }
                return "std::shared_ptr<class " + GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName()) + ">";
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "std::tuple<" + String.Join(", ", Type.Tuple.Select(t => GetTypeString(t))) + ">";
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    var TypeString = GetTypeString(Type.GenericTypeSpec.TypeSpec, true) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ">";
                    if (ForceAsValue)
                    {
                        return TypeString;
                    }
                    if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return TypeString;
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && Type.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        return TypeString;
                    }
                    return "std::shared_ptr<" + TypeString + ">";
                }
                else
                {
                    if (ForceAsValue)
                    {
                        return "class " + GetEscapedIdentifier(Type.TypeFriendlyName());
                    }
                    return "std::shared_ptr<class " + GetEscapedIdentifier(Type.TypeFriendlyName()) + ">";
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
                return "<" + String.Join(", ", GenericParameters.Select(gp => GetEscapedIdentifier(gp.Name))) + ">";
            }
        }
        public List<String> GetGenericParameterLine(List<VariableDef> GenericParameters)
        {
            if (GenericParameters.Count == 0) { return new List<String> { }; }
            return new List<String> { "template<" + String.Join(", ", GenericParameters.Select(gp => "typename " + gp.Name)) + ">" };
        }
        public IEnumerable<String> GetTypePredefinition(TypeDef t)
        {
            var Name = t.TypeFriendlyName();
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
                return TypePredefinition(Name + "Request", MetaType, t.GenericParameters()).Concat(TypePredefinition(Name + "Reply", MetaType, t.GenericParameters()));
            }
            else if (t.OnServerCommand)
            {
                return TypePredefinition(Name + "Event", MetaType, t.GenericParameters());
            }
            else
            {
                return TypePredefinition(Name, MetaType, t.GenericParameters());
            }
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
                    else if (c.VersionedName() == "Optional")
                    {
                        l.AddRange(Primitive_Optional());
                    }
                    else if (c.Primitive.GenericParameters.Count == 0)
                    {
                        var Name = c.Primitive.Name;
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

        public List<String> GetSimpleTypes(Schema Schema)
        {
            var l = new List<String>();

            foreach (var c in Schema.Types)
            {
                if (c.OnEnum)
                {
                    l.AddRange(Enum(c.Enum));
                }
                else
                {
                    continue;
                }
                l.Add("");
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

                l.AddRange(GetTypePredefinition(c));
            }
            l.Add("");

            foreach (var c in Schema.Types)
            {
                if (c.OnAlias)
                {
                    l.AddRange(Alias(c.Alias));
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

        public List<String> GetEnumFunctors(Schema Schema, String NamespaceName)
        {
            var l = new List<String>();

            foreach (var c in Schema.Types)
            {
                if (c.OnEnum)
                {
                    l.AddRange(EnumFunctor(c.Enum, NamespaceName));
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
                    continue;
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
                    continue;
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(ClientCommand(c.ClientCommand));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(ServerCommand(c.ServerCommand));
                }
                else
                {
                    throw new InvalidOperationException();
                }
                l.Add("");
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToList();
            if (Commands.Count > 0)
            {
                l.AddRange(IApplicationServer(Commands));
                l.Add("");
                l.AddRange(IApplicationClient(Commands));
                l.Add("");
                l.AddRange(IEventPump(Commands));
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public List<String> WrapContents(String Namespace, List<String> Contents)
        {
            if (Contents.Count == 0) { return Contents; }
            var c = Contents;
            if (Namespace != "")
            {
                foreach (var nn in Namespace.Split('.').Reverse())
                {
                    c = WrapNamespace(nn, c).ToList();
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
