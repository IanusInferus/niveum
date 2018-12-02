//==========================================================================
//
//  File:        Haxe.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构Haxe代码生成器
//  Version:     2016.10.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.Haxe
{
    public static class CodeGenerator
    {
        public static String CompileToHaxe(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, PackageName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToHaxe(this Schema Schema)
        {
            return CompileToHaxe(Schema, "");
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
        public String GetTypeString(TypeSpec Type)
        {
            if (Type.OnTypeRef)
            {
                if (PrimitiveMapping.ContainsKey(Type.TypeRef.Name))
                {
                    var Name = Type.TypeRef.Name;
                    if (Name.Equals("Optional", StringComparison.OrdinalIgnoreCase) || Name.Equals("List", StringComparison.OrdinalIgnoreCase) || Name.Equals("Set", StringComparison.OrdinalIgnoreCase) || Name.Equals("Map", StringComparison.OrdinalIgnoreCase))
                    {
                        var PlatformName = PrimitiveMapping[Type.TypeRef.Name];
                        return PlatformName;
                    }
                }
                else if (EnumDict.ContainsKey(Type.TypeRef.VersionedName()))
                {
                    return GetTypeString(EnumDict[Type.TypeRef.VersionedName()].UnderlyingType);
                }
                return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
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
                if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                {
                    return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p)).ToList()) + ">";
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
                return "<" + String.Join(", ", GenericParameters.Select(gp => gp.Name)) + ">";
            }
        }

        public List<String> GetTypes(Schema Schema)
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
                    else if (c.VersionedName() == "Set")
                    {
                        l.AddRange(Primitive_Set());
                    }
                    else
                    {
                        var p = c.Primitive;
                        if (PrimitiveMapping.ContainsKey(p.Name))
                        {
                            var Name = p.TypeFriendlyName();
                            var PlatformName = PrimitiveMapping[p.Name];
                            if (Name != PlatformName && p.GenericParameters.Count() == 0 && PlatformName != "Error")
                            {
                                l.AddRange(Primitive(Name, PlatformName));
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
            foreach (var t in Tuples)
            {
                l.AddRange(Tuple(t));
                l.Add("");
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
            if (Commands.Count > 0)
            {
                l.AddRange(IApplicationClient(Commands));
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
