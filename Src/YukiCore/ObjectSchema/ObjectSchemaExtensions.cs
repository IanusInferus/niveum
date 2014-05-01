//==========================================================================
//
//  File:        ObjectSchemaExtensions.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构扩展
//  Version:     2013.12.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema
{
    public class SchemaClosure
    {
        public List<TypeDef> TypeDefs;
        public List<TypeSpec> TypeSpecs;
    }
    public interface ISchemaClosureGenerator
    {
        void Mark(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs);
        SchemaClosure GetClosure(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs);
        Schema GetSubSchema(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs);
    }

    public static class ObjectSchemaExtensions
    {
        public static IEnumerable<KeyValuePair<String, TypeDef>> GetMap(this Schema s)
        {
            return s.TypeRefs.Concat(s.Types).Select(t => CollectionOperations.CreatePair(t.VersionedName(), t));
        }

        private static ThreadLocal<BinarySerializer> bs = new ThreadLocal<BinarySerializer>
        (
            () =>
            {
                return BinarySerializerWithString.Create();
            }
        );

        public static Byte[] GetUnifiedBinaryRepresentation(this Schema s)
        {
            var Types = s.GetMap().OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Value).ToArray();
            var TypesWithoutDescription = Types.Select(t => MapWithoutDescription(t)).ToArray();

            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Value.Write(TypesWithoutDescription, ms);
                ms.Position = 0;

                var Bytes = ms.Read((int)(ms.Length));
                return Bytes;
            }
        }

        public static UInt64 Hash(this Schema s)
        {
            var sha = new SHA1CryptoServiceProvider();
            var Bytes = GetUnifiedBinaryRepresentation(s);
            var result = sha.ComputeHash(Bytes);
            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(result.Skip(result.Length - 8).ToArray());
                ms.Position = 0;

                return ms.ReadUInt64B();
            }
        }

        public static TypeDef MapType(this TypeDef d, Func<TypeDef, TypeDef> MapTypeDefKernel)
        {
            return MapType(d, MapTypeDefKernel, s => s, v => v, l => l);
        }
        public static TypeDef MapType(this TypeDef d, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel)
        {
            return MapType(d, MapTypeDefKernel, MapTypeSpecKernel, v => v, l => l);
        }
        public static TypeSpec MapType(this TypeSpec s, Func<TypeDef, TypeDef> MapTypeDefKernel)
        {
            return MapType(s, MapTypeDefKernel, ss => ss, v => v, l => l);
        }
        public static TypeSpec MapType(this TypeSpec s, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel)
        {
            return MapType(s, MapTypeDefKernel, MapTypeSpecKernel, v => v, l => l);
        }
        public static TypeDef MapType(this TypeDef d, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel, Func<VariableDef, VariableDef> MapVariableKernel, Func<LiteralDef, LiteralDef> MapLiteralDefKernel)
        {
            var t = MapTypeDefKernel(d);
            if (t.OnPrimitive)
            {
                var p = t.Primitive;
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Description = p.Description });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Type = MapType(a.Type, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Description = a.Description });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Fields = r.Fields.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Description = r.Description });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Alternatives = tu.Alternatives.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Description = tu.Description });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = MapType(e.UnderlyingType, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Literals = e.Literals.Select(l => MapLiteralDefKernel(l)).ToArray(), Description = e.Description });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), InParameters = cc.InParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Description = cc.Description });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray(), Description = sc.Description });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public static TypeSpec MapType(this TypeSpec s, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel, Func<VariableDef, VariableDef> MapVariableKernel, Func<LiteralDef, LiteralDef> MapLiteralDefKernel)
        {
            var t = MapTypeSpecKernel(s);
            if (t.OnTypeRef)
            {
                return TypeSpec.CreateTypeRef(new TypeRef { Name = t.TypeRef.Name, Version = t.TypeRef.Version });
            }
            else if (t.OnGenericParameterRef)
            {
                return t;
            }
            else if (t.OnTuple)
            {
                return TypeSpec.CreateTuple(new TupleDef { Types = t.Tuple.Types.Select(tt => MapType(tt, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray() });
            }
            else if (t.OnGenericTypeSpec)
            {
                var gts = t.GenericTypeSpec;
                return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = MapType(gts.TypeSpec, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), GenericParameterValues = gts.GenericParameterValues.Select(gpv => MapType(gpv, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToArray() });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static VariableDef MapType(VariableDef v, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel, Func<VariableDef, VariableDef> MapVariableKernel, Func<LiteralDef, LiteralDef> MapLiteralDefKernel)
        {
            var vv = MapVariableKernel(v);
            return new VariableDef { Name = vv.Name, Type = MapType(vv.Type, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Description = vv.Description };
        }
        private static GenericParameterValue MapType(GenericParameterValue gpv, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel, Func<VariableDef, VariableDef> MapVariableKernel, Func<LiteralDef, LiteralDef> MapLiteralDefKernel)
        {
            if (gpv.OnLiteral)
            {
                return gpv;
            }
            else if (gpv.OnTypeSpec)
            {
                return GenericParameterValue.CreateTypeSpec(MapType(gpv.TypeSpec, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TypeDef MapWithoutDescription(TypeDef d)
        {
            Func<TypeDef, TypeDef> MapTypeDefKernel = t =>
            {
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters, Description = "" });
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters, Type = a.Type, Description = "" });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters, Fields = r.Fields, Description = "" });
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Description = "" });
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Description = "" });
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Description = "" });
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters, Description = "" });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };

            Func<VariableDef, VariableDef> MapVariableDefKernel = v =>
            {
                return new VariableDef { Name = v.Name, Type = v.Type, Description = "" };
            };

            Func<LiteralDef, LiteralDef> MapLiteralDefKernel = l =>
            {
                return new LiteralDef { Name = l.Name, Value = l.Value, Description = "" };
            };

            return MapType(d, MapTypeDefKernel, s => s, MapVariableDefKernel, MapLiteralDefKernel);
        }

        public static Schema GetNonversioned(this Schema s)
        {
            var Types = s.Types.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => "") }).ToArray();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => "") }).ToArray();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            var TypePaths = s.TypePaths.Select(tp => new TypePath { Name = Dict[tp.Name], Path = tp.Path }).ToArray();
            return new Schema { Types = Types.Select(t => t.Current).ToArray(), TypeRefs = TypeRefs.Select(t => t.Current).ToArray(), Imports = s.Imports.ToArray(), TypePaths = TypePaths };
        }

        public static Schema GetTypesVersioned(this Schema s, String NewVersion)
        {
            var TypeRefNames = new HashSet<String>(s.TypeRefs.Select(t => t.VersionedName()), StringComparer.OrdinalIgnoreCase);
            var Types = s.Types.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => TypeRefNames.Contains((new TypeRef { Name = Name, Version = Version }).VersionedName()) ? Version : NewVersion) }).ToArray();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => Version) }).ToArray();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            var TypePaths = s.TypePaths.Select(tp => new TypePath { Name = Dict[tp.Name], Path = tp.Path }).ToArray();
            return new Schema { Types = Types.Select(t => t.Current).ToArray(), TypeRefs = TypeRefs.Select(t => t.Current).ToArray(), Imports = s.Imports.ToArray(), TypePaths = TypePaths };
        }
        private static TypeDef MapWithVersion(TypeDef d, Func<String, String, String> GetVersionFromNameAndVersion)
        {
            Func<TypeDef, TypeDef> MapTypeDefKernel = t =>
            {
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters, Description = p.Description });
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = GetVersionFromNameAndVersion(a.Name, a.Version), GenericParameters = a.GenericParameters, Type = a.Type, Description = a.Description });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = GetVersionFromNameAndVersion(r.Name, r.Version), GenericParameters = r.GenericParameters, Fields = r.Fields, Description = r.Description });
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = GetVersionFromNameAndVersion(tu.Name, tu.Version), GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Description = tu.Description });
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = GetVersionFromNameAndVersion(e.Name, e.Version), UnderlyingType = e.UnderlyingType, Literals = e.Literals, Description = e.Description });
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = GetVersionFromNameAndVersion(cc.Name, cc.Version), OutParameters = cc.OutParameters, InParameters = cc.InParameters, Description = cc.Description });
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = GetVersionFromNameAndVersion(sc.Name, sc.Version), OutParameters = sc.OutParameters, Description = sc.Description });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };

            Func<TypeSpec, TypeSpec> MapTypeSpecKernel = t =>
            {
                if (t.OnTypeRef)
                {
                    return TypeSpec.CreateTypeRef(new TypeRef { Name = t.TypeRef.Name, Version = GetVersionFromNameAndVersion(t.TypeRef.Name, t.TypeRef.Version) });
                }
                else if (t.OnGenericParameterRef)
                {
                    return t;
                }
                else if (t.OnTuple)
                {
                    return t;
                }
                else if (t.OnGenericTypeSpec)
                {
                    return t;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };

            return MapType(d, MapTypeDefKernel, MapTypeSpecKernel, v => v, l => l);
        }

        public static TypeDef MakeGenericType(this TypeDef d, String Name, GenericParameterValue[] GenericParameterValues)
        {
            var gpvMap = d.GenericParameters().Zip(GenericParameterValues, (gp, gpv) => new { gp = gp, gpv = gpv }).Where(z => z.gpv.OnTypeSpec).ToDictionary(z => z.gp.Name, z => z.gpv.TypeSpec);
            Func<TypeSpec, TypeSpec> MapTypeSpecKernel = s =>
            {
                if (s.OnGenericParameterRef)
                {
                    var ParameterName = s.GenericParameterRef.Value;
                    if (gpvMap.ContainsKey(ParameterName))
                    {
                        return gpvMap[ParameterName];
                    }
                }
                return s;
            };

            var t = d.MapType(dd => dd, MapTypeSpecKernel);
            if (t.OnPrimitive)
            {
                var p = t.Primitive;
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = Name, GenericParameters = new VariableDef[] { }, Description = p.Description });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = Name, Version = a.Version, GenericParameters = new VariableDef[] { }, Type = a.Type, Description = a.Description });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = Name, Version = r.Version, GenericParameters = new VariableDef[] { }, Fields = r.Fields, Description = r.Description });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = Name, Version = tu.Version, GenericParameters = new VariableDef[] { }, Alternatives = tu.Alternatives, Description = tu.Description });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Description = e.Description });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = Name, Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Description = cc.Description });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = Name, Version = sc.Version, OutParameters = sc.OutParameters, Description = sc.Description });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private class SchemaClosureGenerator : ISchemaClosureGenerator
        {
            private Schema s;
            private Dictionary<String, TypeDef> Types;
            public SchemaClosureGenerator(Schema s)
            {
                this.s = s;
                Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
            }

            private class Marker
            {
                public Dictionary<String, TypeDef> SchemaTypes;
                public List<TypeDef> TypeDefs = new List<TypeDef>();
                public HashSet<TypeDef> TypeDefSet = new HashSet<TypeDef>();
                public List<TypeSpec> TypeSpecs = new List<TypeSpec>();
                public HashSet<String> TypeSpecSet = new HashSet<String>();
                public void Mark(TypeDef t)
                {
                    if (TypeDefSet.Contains(t)) { return; }
                    TypeDefs.Add(t);
                    TypeDefSet.Add(t);
                    switch (t._Tag)
                    {
                        case TypeDefTag.Primitive:
                            foreach (var gp in t.Primitive.GenericParameters)
                            {
                                MarkAndGetTypeString(gp.Type);
                            }
                            break;
                        case TypeDefTag.Alias:
                            foreach (var gp in t.Alias.GenericParameters)
                            {
                                MarkAndGetTypeString(gp.Type);
                            }
                            MarkAndGetTypeString(t.Alias.Type);
                            break;
                        case TypeDefTag.Record:
                            foreach (var gp in t.Record.GenericParameters)
                            {
                                MarkAndGetTypeString(gp.Type);
                            }
                            foreach (var f in t.Record.Fields)
                            {
                                MarkAndGetTypeString(f.Type);
                            }
                            break;
                        case TypeDefTag.TaggedUnion:
                            foreach (var gp in t.TaggedUnion.GenericParameters)
                            {
                                MarkAndGetTypeString(gp.Type);
                            }
                            foreach (var a in t.TaggedUnion.Alternatives)
                            {
                                MarkAndGetTypeString(a.Type);
                            }
                            break;
                        case TypeDefTag.Enum:
                            MarkAndGetTypeString(t.Enum.UnderlyingType);
                            break;
                        case TypeDefTag.ClientCommand:
                            foreach (var p in t.ClientCommand.InParameters)
                            {
                                MarkAndGetTypeString(p.Type);
                            }
                            foreach (var p in t.ClientCommand.OutParameters)
                            {
                                MarkAndGetTypeString(p.Type);
                            }
                            break;
                        case TypeDefTag.ServerCommand:
                            foreach (var p in t.ServerCommand.OutParameters)
                            {
                                MarkAndGetTypeString(p.Type);
                            }
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
                public String MarkAndGetTypeString(TypeSpec t)
                {
                    String TypeString;
                    switch (t._Tag)
                    {
                        case TypeSpecTag.TypeRef:
                            var VersionedName = t.TypeRef.VersionedName();
                            if (SchemaTypes.ContainsKey(VersionedName))
                            {
                                Mark(SchemaTypes[VersionedName]);
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format("TypeNotExist: {0}", VersionedName));
                            }
                            TypeString = VersionedName;
                            break;
                        case TypeSpecTag.GenericParameterRef:
                            TypeString = t.GenericParameterRef.Value;
                            break;
                        case TypeSpecTag.Tuple:
                            TypeString = "Tuple<" + String.Join(", ", t.Tuple.Types.Select(tt => MarkAndGetTypeString(tt)).ToArray()) + ">";
                            break;
                        case TypeSpecTag.GenericTypeSpec:
                            TypeString = MarkAndGetTypeString(t.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", t.GenericTypeSpec.GenericParameterValues.Select(p => MarkAndGetTypeString(p)).ToArray()) + ">";
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                    if (!TypeSpecSet.Contains(TypeString))
                    {
                        TypeSpecs.Add(t);
                        TypeSpecSet.Add(TypeString);
                    }
                    return TypeString;
                }
                public String MarkAndGetTypeString(GenericParameterValue Value)
                {
                    if (Value.OnLiteral)
                    {
                        return Value.Literal.Replace(@"\", @"\\").Replace("<", @"\<").Replace(">", @"\>").Replace(",", @"\,");
                    }
                    else if (Value.OnTypeSpec)
                    {
                        return MarkAndGetTypeString(Value.TypeSpec);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            private Marker CreateMarker(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
            {
                var m = new Marker { SchemaTypes = Types };
                foreach (var t in TypeDefs)
                {
                    if (!(Types.ContainsKey(t.VersionedName()) && Types[t.VersionedName()] == t))
                    {
                        throw new InvalidOperationException("TypeDefNotInSchema: " + t.VersionedName());
                    }
                    m.Mark(t);
                }
                foreach (var t in TypeSpecs)
                {
                    m.MarkAndGetTypeString(t);
                }
                return m;
            }
            public void Mark(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
            {
                CreateMarker(TypeDefs, TypeSpecs);
            }
            public SchemaClosure GetClosure(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
            {
                var m = CreateMarker(TypeDefs, TypeSpecs);
                return new SchemaClosure { TypeDefs = m.TypeDefs.ToList(), TypeSpecs = m.TypeSpecs.ToList() };
            }
            public Schema GetSubSchema(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
            {
                var m = CreateMarker(TypeDefs, TypeSpecs);
                var TypeDefSet = m.TypeDefSet;
                var MarkedNames = new HashSet<String>(m.TypeDefs.Select(t => t.VersionedName()));
                return new Schema { Types = s.Types.Where(t => TypeDefSet.Contains(t)).ToArray(), TypeRefs = s.TypeRefs.Where(t => TypeDefSet.Contains(t)).ToArray(), Imports = s.Imports, TypePaths = s.TypePaths.Where(tp => MarkedNames.Contains(tp.Name)).ToArray() };
            }
        }
        public static ISchemaClosureGenerator GetSchemaClosureGenerator(this Schema s)
        {
            return new SchemaClosureGenerator(s);
        }
        public static Schema GetSubSchema(this Schema s, IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
        {
            return s.GetSchemaClosureGenerator().GetSubSchema(TypeDefs, TypeSpecs);
        }

        public static Schema Reduce(this Schema s)
        {
            return GetSubSchema(s, s.TypeRefs.Concat(s.Types).Where(t => t.OnClientCommand || t.OnServerCommand), new TypeSpec[] { });
        }

        public static void Verify(this Schema s)
        {
            VerifyDuplicatedNames(s);
            VerifyTypes(s);
        }

        public static void VerifyDuplicatedNames(this Schema s)
        {
            CheckDuplicatedNames(s.TypePaths, tp => tp.Name, tp => String.Format("DuplicatedName {0}: at {1}", tp.Name, tp.Path));

            var PathDict = s.TypePaths.ToDictionary(tp => tp.Name, tp => tp.Path);

            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                switch (t._Tag)
                {
                    case TypeDefTag.Record:
                        {
                            var r = t.Record;
                            CheckDuplicatedNames(r.Fields, rf => rf.Name, rf => String.Format("DuplicatedField {0}: record {1}, at {2}", rf.Name, r.VersionedName(), PathDict[r.VersionedName()]));
                        }
                        break;
                    case TypeDefTag.TaggedUnion:
                        {
                            var tu = t.TaggedUnion;
                            CheckDuplicatedNames(tu.Alternatives, tua => tua.Name, tua => String.Format("DuplicatedAlternative {0}: tagged union {1}, at {2}", tua.Name, tu.VersionedName(), PathDict[tu.VersionedName()]));
                        }
                        break;
                    case TypeDefTag.Enum:
                        {
                            var e = t.Enum;
                            CheckDuplicatedNames(e.Literals, el => el.Name, el => String.Format("DuplicatedLiteral {0}: enum {1}, at {2}", el.Name, e.VersionedName(), PathDict[e.VersionedName()]));
                        }
                        break;
                    case TypeDefTag.ClientCommand:
                        {
                            var cc = t.ClientCommand;
                            CheckDuplicatedNames(cc.OutParameters, op => op.Name, op => String.Format("DuplicatedOutParameter {0}: client command {1}, at {2}", op.Name, cc.VersionedName(), PathDict[cc.VersionedName()]));
                            CheckDuplicatedNames(cc.InParameters, op => op.Name, op => String.Format("DuplicatedInParameter {0}: client command {1}, at {2}", op.Name, cc.VersionedName(), PathDict[cc.VersionedName()]));
                        }
                        break;
                    case TypeDefTag.ServerCommand:
                        {
                            var sc = t.ServerCommand;
                            CheckDuplicatedNames(sc.OutParameters, op => op.Name, op => String.Format("DuplicatedOutParameter {0}: server command {1}, at {2}", op.Name, sc.VersionedName(), PathDict[sc.VersionedName()]));
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public static void VerifyTypes(this Schema s)
        {
            var Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);

            var Gen = new SchemaClosureGenerator(s);
            Gen.Mark(s.Types, new TypeSpec[] { });
        }

        private static void CheckDuplicatedNames<T>(IEnumerable<T> Values, Func<T, String> NameSelector, Func<T, String> ErrorMessageSelector)
        {
            var TypeNames = Values.Select(NameSelector).Distinct(StringComparer.OrdinalIgnoreCase);
            var DuplicatedNames = new HashSet<String>(Values.GroupBy(NameSelector, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);

            if (DuplicatedNames.Count > 0)
            {
                var l = new List<String>();
                foreach (var tp in Values.Where(p => DuplicatedNames.Contains(NameSelector(p))))
                {
                    l.Add(ErrorMessageSelector(tp));
                }
                var Message = String.Concat(l.Select(Line => Line + Environment.NewLine));
                throw new AggregateException(Message);
            }
        }

        public static String Name(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Name;
                case TypeDefTag.Alias:
                    return t.Alias.Name;
                case TypeDefTag.Record:
                    return t.Record.Name;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.Name;
                case TypeDefTag.Enum:
                    return t.Enum.Name;
                case TypeDefTag.ClientCommand:
                    return t.ClientCommand.Name;
                case TypeDefTag.ServerCommand:
                    return t.ServerCommand.Name;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String Version(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return "";
                case TypeDefTag.Alias:
                    return t.Alias.Version;
                case TypeDefTag.Record:
                    return t.Record.Version;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.Version;
                case TypeDefTag.Enum:
                    return t.Enum.Version;
                case TypeDefTag.ClientCommand:
                    return t.ClientCommand.Version;
                case TypeDefTag.ServerCommand:
                    return t.ServerCommand.Version;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String VersionedName(this PrimitiveDef t)
        {
            var Name = t.Name;
            var Version = "";
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this AliasDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this RecordDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TaggedUnionDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this EnumDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this ClientCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this ServerCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TypeDef t)
        {
            var Name = t.Name();
            var Version = t.Version();
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TypeRef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }

        public static String Description(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Description;
                case TypeDefTag.Alias:
                    return t.Alias.Description;
                case TypeDefTag.Record:
                    return t.Record.Description;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.Description;
                case TypeDefTag.Enum:
                    return t.Enum.Description;
                case TypeDefTag.ClientCommand:
                    return t.ClientCommand.Description;
                case TypeDefTag.ServerCommand:
                    return t.ServerCommand.Description;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static VariableDef[] GenericParameters(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.GenericParameters;
                case TypeDefTag.Alias:
                    return t.Alias.GenericParameters;
                case TypeDefTag.Record:
                    return t.Record.GenericParameters;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.GenericParameters;
                case TypeDefTag.Enum:
                    return new VariableDef[] { };
                case TypeDefTag.ClientCommand:
                    return new VariableDef[] { };
                case TypeDefTag.ServerCommand:
                    return new VariableDef[] { };
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String TypeFriendlyName(this PrimitiveDef t)
        {
            var Name = t.Name;
            var Version = "";
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this AliasDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this RecordDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TaggedUnionDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this EnumDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this ClientCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this ServerCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeDef t)
        {
            var Name = t.Name();
            var Version = t.Version();
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeRef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeSpec t)
        {
            return TypeFriendlyName(t, gpr => gpr.Value);
        }
        public static String TypeFriendlyName(this TypeSpec t, Func<GenericParameterRef, String> EvaluateGenericParameterRef)
        {
            return TypeFriendlyName(t, EvaluateGenericParameterRef, TypeFriendlyName);
        }
        public static String TypeFriendlyName(this TypeSpec Type, Func<GenericParameterRef, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<GenericParameterRef, String>, String> Kernel)
        {
            switch (Type._Tag)
            {
                case TypeSpecTag.TypeRef:
                    return Type.TypeRef.TypeFriendlyName();
                case TypeSpecTag.GenericParameterRef:
                    return EvaluateGenericParameterRef(Type.GenericParameterRef);
                case TypeSpecTag.Tuple:
                    return "TupleOf" + String.Join("And", Type.Tuple.Types.Select(t => Kernel(t, EvaluateGenericParameterRef)).ToArray());
                case TypeSpecTag.GenericTypeSpec:
                    return Kernel(Type.GenericTypeSpec.TypeSpec, EvaluateGenericParameterRef) + "Of" + String.Join("And", Type.GenericTypeSpec.GenericParameterValues.Select(t => TypeFriendlyName(t, EvaluateGenericParameterRef, Kernel)).ToArray());
                default:
                    throw new InvalidOperationException();
            }
        }
        private static Regex rNonRegularChars = new Regex(@"[\u0000-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007F]");
        public static String TypeFriendlyName(this GenericParameterValue Value, Func<GenericParameterRef, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<GenericParameterRef, String>, String> Kernel)
        {
            switch (Value._Tag)
            {
                case GenericParameterValueTag.Literal:
                    var l = Value.Literal;
                    return rNonRegularChars.Replace(l, "_");
                case GenericParameterValueTag.TypeSpec:
                    return Kernel(Value.TypeSpec, EvaluateGenericParameterRef);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
