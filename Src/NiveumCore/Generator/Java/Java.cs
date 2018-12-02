//==========================================================================
//
//  File:        Java.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构Java代码生成器
//  Version:     2016.10.10.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Java
{
    public static class CodeGenerator
    {
        public static String CompileToJava(this Schema Schema, String ClassName, String PackageName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, ClassName, PackageName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToJava(this Schema Schema, String ClassName)
        {
            return CompileToJava(Schema, ClassName, "");
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
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }

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
        public String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        private Dictionary<String, EnumDef> EnumDict = new Dictionary<String, EnumDef>();
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.Name))
                {
                    var PlatformName = PrimitiveMapping[Type.TypeRef.Name];
                    return PlatformName;
                }
                else if (EnumDict.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    return GetTypeString(EnumDict[Type.TypeRef.VersionedName()].UnderlyingType);
                }
                return GetEscapedIdentifier(Type.TypeFriendlyName());
            }
            else if (Type.OnGenericParameterRef)
            {
                return GetEscapedIdentifier(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return GetEscapedIdentifier(Type.TypeFriendlyName());
            }
            else if (Type.OnGenericTypeSpec)
            {
                if (Type.GenericTypeSpec.ParameterValues.Count() == 1 && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                {
                    return GetEscapedIdentifier("Opt" + Type.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName());
                }
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => MapToReferenceType(GetTypeString(p)))) + ">";
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
        private String MapToReferenceType(String TypeName)
        {
            return ReferenceTypeMapping.ContainsKey(TypeName) ? ReferenceTypeMapping[TypeName] : TypeName;
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

        public List<String> GetPrimitives(Schema Schema)
        {
            var l = new List<String>();

            foreach (var p in Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnPrimitive).Select(c => c.Primitive))
            {
                if (PrimitiveMapping.ContainsKey(p.Name))
                {
                    var Name = p.TypeFriendlyName();
                    var PlatformName = PrimitiveMapping[p.Name];
                    if ((Name != PlatformName) && (p.GenericParameters.Count() == 0))
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
                    if (c.VersionedName() == "Unit")
                    {
                        l.AddRange(Primitive_Unit());
                    }
                    else
                    {
                        continue;
                    }
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

            var scg = Schema.GetSchemaClosureGenerator();
            var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();
            foreach (var t in Tuples)
            {
                l.AddRange(Tuple(t));
                l.Add("");
            }

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
            if (GenericOptionalTypes.Count > 0)
            {
                var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        var ElementType = gts.GenericTypeSpec.ParameterValues.Single();
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Attributes = a.Attributes, Description = a.Description }).ToList();
                        l.AddRange(TaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Attributes = GenericOptionalType.Attributes, Description = GenericOptionalType.Description }));
                        l.Add("");
                    }
                }
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
