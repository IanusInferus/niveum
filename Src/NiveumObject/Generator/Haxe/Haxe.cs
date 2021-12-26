//==========================================================================
//
//  File:        Haxe.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构Haxe代码生成器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Haxe
{
    public static class CodeGenerator
    {
        public static Dictionary<String, String> CompileToHaxe(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Files = t.GetPackageFiles(Schema, PackageName).ToDictionary(p => p.Key + ".hx", p => String.Join("\r\n", p.Value.Select(Line => Line.TrimEnd(' '))));
            return Files;
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

            EnumDict = Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).ToDictionary(c => c.VersionedName(), c => c.Enum, StringComparer.OrdinalIgnoreCase);
        }

        private Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
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
            });
        }
        public String LowercaseCamelize(String PascalName)
        {
            var l = new List<Char>();
            foreach (var c in PascalName)
            {
                if (Char.IsLower(c))
                {
                    break;
                }

                l.Add(Char.ToLower(c));
            }

            return new String(l.ToArray()) + PascalName.Substring(l.Count);
        }
        public String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\"' ? "\"\"" : c == '\r' ? "\" + Microsoft.VisualBasic.ChrW(13) + \"" : c == '\n' ? "\" + Microsoft.VisualBasic.ChrW(10) + \"" : new String(c, 1)).ToArray()) + "\"";
        }
        private Dictionary<String, EnumDef> EnumDict = new Dictionary<String, EnumDef>();
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    var Name = Type.TypeRef.VersionedName();
                    if (Name.Equals("Optional", StringComparison.OrdinalIgnoreCase) || Name.Equals("List", StringComparison.OrdinalIgnoreCase) || Name.Equals("Set", StringComparison.OrdinalIgnoreCase) || Name.Equals("Map", StringComparison.OrdinalIgnoreCase))
                    {
                        var PlatformName = PrimitiveMapping[Name];
                        return PlatformName;
                    }
                }
                else if (EnumDict.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    return GetTypeString(EnumDict[Type.TypeRef.VersionedName()].UnderlyingType, NamespaceName);
                }
                var Ref = Type.TypeRef;
                if ((Ref.NamespaceName() == NamespaceName) || NamespaceName.StartsWith(Ref.NamespaceName() + ".") || (Ref.NamespaceName() == ""))
                {
                    return GetEscapedIdentifier(Ref.SimpleName(Ref.NamespaceName()));
                }
                else
                {
                    return GetEscapedIdentifier(String.Join(".", Ref.NamespaceName().Split('.').Select(NamespacePart => LowercaseCamelize(NamespacePart))) + "." + Ref.SimpleName(Ref.NamespaceName()));
                }
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return GetEscapedIdentifier(Type.SimpleName(NamespaceName));
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p, NamespaceName)).ToList()) + ">";
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
                return "<" + String.Join(", ", GenericParameters.Select(gp => gp.Name)) + ">";
            }
        }

        public Dictionary<String, IEnumerable<String>> GetPackageFiles(Schema Schema, String NamespaceName)
        {
            var NamespaceToClasses = new Dictionary<String, List<KeyValuePair<String, List<String>>>>();
            void AddClass(String ClassNamespaceName, String ClassName, IEnumerable<String> ClassContent)
            {
                if (!NamespaceToClasses.ContainsKey(ClassNamespaceName))
                {
                    NamespaceToClasses.Add(ClassNamespaceName, new List<KeyValuePair<String, List<String>>>());
                }
                NamespaceToClasses[ClassNamespaceName].Add(new KeyValuePair<String, List<String>>(ClassName, ClassContent.ToList()));
            }

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    if (c.VersionedName() == "Unit")
                    {
                        AddClass(c.NamespaceName(), "Unit", Primitive_Unit());
                    }
                    else if (c.VersionedName() == "Set")
                    {
                        AddClass(c.NamespaceName(), "Set", Primitive_Set());
                    }
                    else
                    {
                        var p = c.Primitive;
                        if (PrimitiveMapping.ContainsKey(p.VersionedName()))
                        {
                            var Name = p.VersionedName();
                            var PlatformName = PrimitiveMapping[Name];
                            if (Name != PlatformName && p.GenericParameters.Count() == 0 && PlatformName != "Error")
                            {
                                AddClass(c.NamespaceName(), Name, Primitive(Name, PlatformName));
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
                else if (c.OnAlias)
                {
                    AddClass(c.NamespaceName(), c.DefinitionName(), Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    AddClass(c.NamespaceName(), c.DefinitionName(), Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    AddClass(c.NamespaceName(), c.DefinitionName(), TaggedUnion(c.TaggedUnion));
                }
                else if (c.OnEnum)
                {
                    AddClass(c.NamespaceName(), c.DefinitionName(), Enum(c.Enum));
                }
                else if (c.OnClientCommand)
                {
                    var tc = c.ClientCommand;
                    var RequestRef = GetSuffixedTypeRef(tc.Name, tc.Version, "Request");
                    var Request = new RecordDef { Name = RequestRef.Name, Version = RequestRef.Version, GenericParameters = new List<VariableDef> { }, Fields = tc.OutParameters, Attributes = tc.Attributes, Description = tc.Description };
                    var ReplyRef = GetSuffixedTypeRef(tc.Name, tc.Version, "Reply");
                    var Reply = new TaggedUnionDef { Name = ReplyRef.Name, Version = ReplyRef.Version, GenericParameters = new List<VariableDef> { }, Alternatives = tc.InParameters, Attributes = tc.Attributes, Description = tc.Description };
                    AddClass(c.NamespaceName(), Request.DefinitionName(), Record(Request));
                    AddClass(c.NamespaceName(), Reply.DefinitionName(), TaggedUnion(Reply));
                }
                else if (c.OnServerCommand)
                {
                    var tc = c.ServerCommand;
                    var EventRef = GetSuffixedTypeRef(tc.Name, tc.Version, "Event");
                    var Event = new RecordDef { Name = EventRef.Name, Version = EventRef.Version, GenericParameters = new List<VariableDef> { }, Fields = tc.OutParameters, Attributes = tc.Attributes, Description = tc.Description };
                    AddClass(c.NamespaceName(), Event.DefinitionName(), Record(Event));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            var scg = Schema.GetSchemaClosureGenerator();
            var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            foreach (var t in Tuples)
            {
                AddClass(NamespaceName, t.SimpleName(NamespaceName), Tuple(t, NamespaceName));
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
            if (Commands.Count > 0)
            {
                AddClass(NamespaceName, "IApplicationClient", IApplicationClient(Commands, NamespaceName));
            }

            return NamespaceToClasses.SelectMany(p => p.Value.Select(v => new KeyValuePair<String, IEnumerable<String>>(String.Join("/", p.Key.Split('.').Where(NamespacePart => NamespacePart != "").Select(NamespacePart => LowercaseCamelize(NamespacePart)).Concat(new String[] { v.Key })), WrapModule(p.Key, Schema.Imports, v.Value)))).ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
