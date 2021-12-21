//==========================================================================
//
//  File:        Java.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构Java代码生成器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Java
{
    public static class CodeGenerator
    {
        public static Dictionary<String, String> CompileToJava(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Files = t.GetPackageFiles(Schema, PackageName).ToDictionary(p => p.Key + ".java", p => String.Join("\r\n", p.Value.Select(Line => Line.TrimEnd(' '))));
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
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.NameMatches(Name => Name == "Int")).Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }

            EnumDict = Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).ToDictionary(c => c.VersionedName(), c => c.Enum);
        }

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
            return "\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        private Dictionary<String, EnumDef> EnumDict = new Dictionary<String, EnumDef>();
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.VersionedName()];
                    return PlatformName;
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
                if (Type.GenericTypeSpec.ParameterValues.Count() == 1 && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional"))
                {
                    return GetEscapedIdentifier("Opt" + Type.GenericTypeSpec.ParameterValues.Single().SimpleName(NamespaceName));
                }
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec, NamespaceName) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => MapToReferenceType(GetTypeString(p, NamespaceName)))) + ">";
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
        private String MapToReferenceType(String TypeName)
        {
            return ReferenceTypeMapping.ContainsKey(TypeName) ? ReferenceTypeMapping[TypeName] : TypeName;
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
        public IEnumerable<String> GetXmlComment(String Description, TypeSpec Type)
        {
            if (Type.OnTypeRef && EnumDict.ContainsKey(Type.TypeRef.VersionedName()))
            {
                if (Description == "")
                {
                    return GetXmlComment(String.Format("Type: {0}", Type.TypeRef.VersionedName()));
                }
                else
                {
                    return GetXmlComment(String.Format("{0}\r\nType: {1}", Description, Type.TypeRef.VersionedName()));
                }
            }
            return GetXmlComment(Description);
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

            AddClass("niveum.lang", "Record", Attribute_Record());
            AddClass("niveum.lang", "Alias", Attribute_Alias());
            AddClass("niveum.lang", "TaggedUnion", Attribute_TaggedUnion());
            AddClass("niveum.lang", "Tag", Attribute_Tag());
            AddClass("niveum.lang", "Tuple", Attribute_Tuple());
            AddClass("niveum.lang", "Unit", Primitive_Unit());

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    continue;
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
                    var tu = c.TaggedUnion;
                    var TagName = GetSuffixedTypeName(tu.Name, tu.Version, "Tag", tu.NamespaceName());
                    AddClass(c.NamespaceName(), TagName, TaggedUnionTag(c.TaggedUnion));
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
                    var ReplyTagName = GetSuffixedTypeName(Reply.Name, Reply.Version, "Tag", tc.NamespaceName());
                    AddClass(c.NamespaceName(), Request.DefinitionName(), Record(Request));
                    AddClass(c.NamespaceName(), ReplyTagName, TaggedUnionTag(Reply));
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
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();
            foreach (var t in Tuples)
            {
                AddClass(NamespaceName, t.SimpleName(NamespaceName), Tuple(t, NamespaceName));
            }

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.NameMatches("Optional")).ToList();
            if (GenericOptionalTypes.Count > 0)
            {
                var GenericOptionalType = new TaggedUnionDef
                {
                    Name = new List<String> { "Optional" },
                    Version = "",
                    GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } },
                    Alternatives = new List<VariableDef>
                    {
                        new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String> { "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                        new VariableDef { Name = "Some", Type = TypeSpec.CreateGenericParameterRef("T"), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
                    },
                    Attributes = new List<KeyValuePair<String, List<String>>> { },
                    Description = ""
                };
                {
                    var TagName = GetSuffixedTypeName(GenericOptionalType.Name, GenericOptionalType.Version, "Tag", GenericOptionalType.NamespaceName());
                    AddClass(NamespaceName, TagName, TaggedUnionTag(GenericOptionalType));
                }
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        var ElementType = gts.GenericTypeSpec.ParameterValues.Single();
                        var Name = "Opt" + ElementType.SimpleName(NamespaceName);
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Attributes = a.Attributes, Description = a.Description }).ToList();
                        var tu = new TaggedUnionDef { Name = new List<String> { Name }, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Attributes = GenericOptionalType.Attributes, Description = GenericOptionalType.Description };
                        AddClass(NamespaceName, Name, TaggedUnion(tu, "OptionalTag"));
                    }
                }
            }

            return NamespaceToClasses.SelectMany(p => p.Value.Select(v => new KeyValuePair<String, IEnumerable<String>>(String.Join("/", p.Key.Split('.').Where(NamespacePart => NamespacePart != "").Select(NamespacePart => LowercaseCamelize(NamespacePart)).Concat(new String[] { v.Key })), WrapModule(p.Key, Schema.Imports, v.Value)))).ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
