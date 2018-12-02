//==========================================================================
//
//  File:        ObjectSchemaExtensions.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构扩展
//  Version:     2018.08.17.
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
using Firefly.Texting.TreeFormat.Syntax;

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
            var Types = s.GetMap().OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Value).ToList();
            var TypesWithoutDescription = Types.Select(t => MapWithoutDescription(t)).ToList();

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
            var sha = new SHA256CryptoServiceProvider();
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
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Attributes = p.Attributes, Description = p.Description });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Type = MapType(a.Type, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Attributes = a.Attributes, Description = a.Description });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Fields = r.Fields.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Attributes = r.Attributes, Description = r.Description });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Alternatives = tu.Alternatives.Select(gp => MapType(gp, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Attributes = tu.Attributes, Description = tu.Description });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = MapType(e.UnderlyingType, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Literals = e.Literals.Select(l => MapLiteralDefKernel(l)).ToList(), Attributes = e.Attributes, Description = e.Description });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), InParameters = cc.InParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Attributes = cc.Attributes, Description = cc.Description });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters.Select(p => MapType(p, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList(), Attributes = sc.Attributes, Description = sc.Description });
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
                return TypeSpec.CreateTuple(t.Tuple.Select(tt => MapType(tt, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList());
            }
            else if (t.OnGenericTypeSpec)
            {
                var gts = t.GenericTypeSpec;
                return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = MapType(gts.TypeSpec, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), ParameterValues = gts.ParameterValues.Select(gpv => MapType(gpv, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel)).ToList() });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static VariableDef MapType(VariableDef v, Func<TypeDef, TypeDef> MapTypeDefKernel, Func<TypeSpec, TypeSpec> MapTypeSpecKernel, Func<VariableDef, VariableDef> MapVariableKernel, Func<LiteralDef, LiteralDef> MapLiteralDefKernel)
        {
            var vv = MapVariableKernel(v);
            return new VariableDef { Name = vv.Name, Type = MapType(vv.Type, MapTypeDefKernel, MapTypeSpecKernel, MapVariableKernel, MapLiteralDefKernel), Attributes = vv.Attributes, Description = vv.Description };
        }

        private static TypeDef MapWithoutDescription(TypeDef d)
        {
            Func<TypeDef, TypeDef> MapTypeDefKernel = t =>
            {
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters, Attributes = p.Attributes, Description = "" });
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters, Type = a.Type, Attributes = a.Attributes, Description = "" });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters, Fields = r.Fields, Attributes = r.Attributes, Description = "" });
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Attributes = tu.Attributes, Description = "" });
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Attributes = e.Attributes, Description = "" });
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Attributes = cc.Attributes, Description = "" });
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters, Attributes = sc.Attributes, Description = "" });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };

            Func<VariableDef, VariableDef> MapVariableDefKernel = v =>
            {
                return new VariableDef { Name = v.Name, Type = v.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
            };

            Func<LiteralDef, LiteralDef> MapLiteralDefKernel = l =>
            {
                return new LiteralDef { Name = l.Name, Value = l.Value, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
            };

            return MapType(d, MapTypeDefKernel, s => s, MapVariableDefKernel, MapLiteralDefKernel);
        }

        public static Schema GetNonversioned(this Schema s)
        {
            var Types = s.Types.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => "") }).ToList();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => "") }).ToList();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            var Nonversioned = new Schema { Types = Types.Select(t => t.Current).ToList(), TypeRefs = TypeRefs.Select(t => t.Current).ToList(), Imports = s.Imports.ToList() };
            var oslr = new ObjectSchemaLoaderResult { Schema = Nonversioned, Positions = new Dictionary<object, FileTextRange> { } };
            oslr.Verify();
            return Nonversioned;
        }
        public static Schema GetNonattributed(this Schema s)
        {
            var Types = s.Types.Select(t => new { Original = t, Current = MapAttributes(t, l => l.Count == 0 ? l : new List<KeyValuePair<String, List<String>>> { }) }).ToList();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapAttributes(t, l => l.Count == 0 ? l : new List<KeyValuePair<String, List<String>>> { }) }).ToList();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            return new Schema { Types = Types.Select(t => t.Current).ToList(), TypeRefs = TypeRefs.Select(t => t.Current).ToList(), Imports = s.Imports.ToList() };
        }

        public static Schema GetTypesVersioned(this Schema s, String NewVersion)
        {
            var TypeRefNames = new HashSet<String>(s.TypeRefs.Select(t => t.VersionedName()), StringComparer.OrdinalIgnoreCase);
            var Types = s.Types.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => TypeRefNames.Contains((new TypeRef { Name = Name, Version = Version }).VersionedName()) ? Version : NewVersion) }).ToList();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapWithVersion(t, (Name, Version) => Version) }).ToList();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            return new Schema { Types = Types.Select(t => t.Current).ToList(), TypeRefs = TypeRefs.Select(t => t.Current).ToList(), Imports = s.Imports.ToList() };
        }
        private static TypeDef MapWithVersion(TypeDef d, Func<String, String, String> GetVersionFromNameAndVersion)
        {
            Func<TypeDef, TypeDef> MapTypeDefKernel = t =>
            {
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters, Attributes = p.Attributes, Description = p.Description });
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = GetVersionFromNameAndVersion(a.Name, a.Version), GenericParameters = a.GenericParameters, Type = a.Type, Attributes = a.Attributes, Description = a.Description });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = GetVersionFromNameAndVersion(r.Name, r.Version), GenericParameters = r.GenericParameters, Fields = r.Fields, Attributes = r.Attributes, Description = r.Description });
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = GetVersionFromNameAndVersion(tu.Name, tu.Version), GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Attributes = tu.Attributes, Description = tu.Description });
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = GetVersionFromNameAndVersion(e.Name, e.Version), UnderlyingType = e.UnderlyingType, Literals = e.Literals, Attributes = e.Attributes, Description = e.Description });
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = GetVersionFromNameAndVersion(cc.Name, cc.Version), OutParameters = cc.OutParameters, InParameters = cc.InParameters, Attributes = cc.Attributes, Description = cc.Description });
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = GetVersionFromNameAndVersion(sc.Name, sc.Version), OutParameters = sc.OutParameters, Attributes = sc.Attributes, Description = sc.Description });
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
        private static TypeDef MapAttributes(TypeDef d, Func<List<KeyValuePair<String, List<String>>>, List<KeyValuePair<String, List<String>>>> m)
        {
            Func<TypeDef, TypeDef> MapTypeDefKernel = t =>
            {
                if (t.OnPrimitive)
                {
                    var p = t.Primitive;
                    return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters, Attributes = m(p.Attributes), Description = p.Description });
                }
                else if (t.OnAlias)
                {
                    var a = t.Alias;
                    return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters, Type = a.Type, Attributes = m(a.Attributes), Description = a.Description });
                }
                else if (t.OnRecord)
                {
                    var r = t.Record;
                    return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters, Fields = r.Fields, Attributes = m(r.Attributes), Description = r.Description });
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Attributes = m(tu.Attributes), Description = tu.Description });
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Attributes = m(e.Attributes), Description = e.Description });
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Attributes = m(cc.Attributes), Description = cc.Description });
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters, Attributes = m(sc.Attributes), Description = sc.Description });
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
                    return TypeSpec.CreateTypeRef(new TypeRef { Name = t.TypeRef.Name, Version = t.TypeRef.Version });
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

        public static TypeDef MakeGenericType(this TypeDef d, String Name, List<TypeSpec> ParameterValues)
        {
            var gpvMap = d.GenericParameters().Zip(ParameterValues, (gp, gpv) => new { gp = gp, gpv = gpv }).ToDictionary(z => z.gp.Name, z => z.gpv);
            Func<TypeSpec, TypeSpec> MapTypeSpecKernel = s =>
            {
                if (s.OnGenericParameterRef)
                {
                    var ParameterName = s.GenericParameterRef;
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
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = Name, GenericParameters = new List<VariableDef> { }, Attributes = p.Attributes, Description = p.Description });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = Name, Version = a.Version, GenericParameters = new List<VariableDef> { }, Type = a.Type, Attributes = a.Attributes, Description = a.Description });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = Name, Version = r.Version, GenericParameters = new List<VariableDef> { }, Fields = r.Fields, Attributes = r.Attributes, Description = r.Description });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = Name, Version = tu.Version, GenericParameters = new List<VariableDef> { }, Alternatives = tu.Alternatives, Attributes = tu.Attributes, Description = tu.Description });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Attributes = e.Attributes, Description = e.Description });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = Name, Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Attributes = cc.Attributes, Description = cc.Description });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = Name, Version = sc.Version, OutParameters = sc.OutParameters, Attributes = sc.Attributes, Description = sc.Description });
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
            private Dictionary<Object, FileTextRange> Positions;
            public SchemaClosureGenerator(Schema s)
            {
                this.s = s;
                Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
                Positions = new Dictionary<Object, FileTextRange>();
            }
            public SchemaClosureGenerator(ObjectSchemaLoaderResult oslr)
            {
                this.s = oslr.Schema;
                Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
                Positions = oslr.Positions;
            }

            private class Marker
            {
                public Func<String, Object, String> GetPositionedMessage;
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
                    if (t.OnPrimitive)
                    {
                        foreach (var gp in t.Primitive.GenericParameters)
                        {
                            MarkAndGetTypeString(gp.Type);
                        }
                    }
                    else if (t.OnAlias)
                    {
                        foreach (var gp in t.Alias.GenericParameters)
                        {
                            MarkAndGetTypeString(gp.Type);
                        }
                        MarkAndGetTypeString(t.Alias.Type);
                    }
                    else if (t.OnRecord)
                    {
                        foreach (var gp in t.Record.GenericParameters)
                        {
                            MarkAndGetTypeString(gp.Type);
                        }
                        foreach (var f in t.Record.Fields)
                        {
                            MarkAndGetTypeString(f.Type);
                        }
                    }
                    else if (t.OnTaggedUnion)
                    {
                        foreach (var gp in t.TaggedUnion.GenericParameters)
                        {
                            MarkAndGetTypeString(gp.Type);
                        }
                        foreach (var a in t.TaggedUnion.Alternatives)
                        {
                            MarkAndGetTypeString(a.Type);
                        }
                    }
                    else if (t.OnEnum)
                    {
                        MarkAndGetTypeString(t.Enum.UnderlyingType);
                    }
                    else if (t.OnClientCommand)
                    {
                        foreach (var p in t.ClientCommand.InParameters)
                        {
                            MarkAndGetTypeString(p.Type);
                        }
                        foreach (var p in t.ClientCommand.OutParameters)
                        {
                            MarkAndGetTypeString(p.Type);
                        }
                    }
                    else if (t.OnServerCommand)
                    {
                        foreach (var p in t.ServerCommand.OutParameters)
                        {
                            MarkAndGetTypeString(p.Type);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                public String MarkAndGetTypeString(TypeSpec t)
                {
                    String TypeString;
                    if (t.OnTypeRef)
                    {
                        var VersionedName = t.TypeRef.VersionedName();
                        if (SchemaTypes.ContainsKey(VersionedName))
                        {
                            Mark(SchemaTypes[VersionedName]);
                        }
                        else
                        {
                            throw new InvalidOperationException(GetPositionedMessage(String.Format("TypeNotExist: {0}", VersionedName), t));
                        }
                        TypeString = VersionedName;
                    }
                    else if (t.OnGenericParameterRef)
                    {
                        TypeString = t.GenericParameterRef;
                    }
                    else if (t.OnTuple)
                    {
                        TypeString = "Tuple<" + String.Join(", ", t.Tuple.Select(tt => MarkAndGetTypeString(tt))) + ">";
                    }
                    else if (t.OnGenericTypeSpec)
                    {
                        TypeString = MarkAndGetTypeString(t.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", t.GenericTypeSpec.ParameterValues.Select(p => MarkAndGetTypeString(p))) + ">";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    if (!TypeSpecSet.Contains(TypeString))
                    {
                        TypeSpecs.Add(t);
                        TypeSpecSet.Add(TypeString);
                    }
                    return TypeString;
                }
            }

            private Marker CreateMarker(IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
            {
                Func<String, Object, String> GetPositionedMessage = (Message, o) =>
                {
                    if (Positions.ContainsKey(o))
                    {
                        var ftr = Positions[o];
                        if (ftr.Range.OnHasValue)
                        {
                            var r = ftr.Range.Value;
                            return String.Format("{0}({1},{2},{3},{4}): {5}", ftr.Text.Path, r.Start.Row, r.Start.Column, r.End.Row, r.End.Column, Message);
                        }
                        else
                        {
                            return String.Format("{0}: {1}", ftr.Text.Path, Message);
                        }
                    }
                    else
                    {
                        return Message;
                    }
                };

                var m = new Marker { SchemaTypes = Types, GetPositionedMessage = GetPositionedMessage };
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
                return new Schema { Types = s.Types.Where(t => TypeDefSet.Contains(t)).ToList(), TypeRefs = s.TypeRefs.Where(t => TypeDefSet.Contains(t)).ToList(), Imports = s.Imports };
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
            return GetSubSchema(s, s.TypeRefs.Concat(s.Types).Where(t => t.OnClientCommand || t.OnServerCommand), new List<TypeSpec> { });
        }

        public static void Verify(this ObjectSchemaLoaderResult oslr)
        {
            VerifyDuplicatedNames(oslr);
            VerifyTypes(oslr);
        }

        public static void VerifyDuplicatedNames(this ObjectSchemaLoaderResult oslr)
        {
            var s = oslr.Schema;
            Func<String, Object, String> GetPositionedMessage = (Message, o) =>
            {
                if (oslr.Positions.ContainsKey(o))
                {
                    var ftr = oslr.Positions[o];
                    if (ftr.Range.OnHasValue)
                    {
                        var r = ftr.Range.Value;
                        return String.Format("{0}({1},{2},{3},{4}): {5}", ftr.Text.Path, r.Start.Row, r.Start.Column, r.End.Row, r.End.Column, Message);
                    }
                    else
                    {
                        return String.Format("{0}: {1}", ftr.Text.Path, Message);
                    }
                }
                else
                {
                    return Message;
                }
            };
            CheckDuplicatedNames(s.TypeRefs.Concat(s.Types), t => t.VersionedName(), t => GetPositionedMessage(String.Format("DuplicatedName: {0}", t.VersionedName()), t));

            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnRecord)
                {
                    var r = t.Record;
                    CheckDuplicatedNames(r.Fields, rf => rf.Name, rf => GetPositionedMessage(String.Format("DuplicatedField: {0}.{1}", r.VersionedName(), rf.Name), rf));
                }
                else if (t.OnTaggedUnion)
                {
                    var tu = t.TaggedUnion;
                    CheckDuplicatedNames(tu.Alternatives, tua => tua.Name, tua => GetPositionedMessage(String.Format("DuplicatedAlternative: {0}.{1}", tu.VersionedName(), tua.Name), tua));
                }
                else if (t.OnEnum)
                {
                    var e = t.Enum;
                    CheckDuplicatedNames(e.Literals, el => el.Name, el => GetPositionedMessage(String.Format("DuplicatedLiteral: {0}.{1}", e.VersionedName(), el.Name), el));
                }
                else if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    CheckDuplicatedNames(cc.OutParameters, op => op.Name, op => GetPositionedMessage(String.Format("DuplicatedOutParameter: {0}.{1}", cc.VersionedName(), op.Name), op));
                    CheckDuplicatedNames(cc.InParameters, op => op.Name, op => GetPositionedMessage(String.Format("DuplicatedInParameter: {0}.{1}", cc.VersionedName(), op.Name), op));
                }
                else if (t.OnServerCommand)
                {
                    var sc = t.ServerCommand;
                    CheckDuplicatedNames(sc.OutParameters, op => op.Name, op => GetPositionedMessage(String.Format("DuplicatedOutParameter: {0}.{1}", sc.VersionedName(), op.Name), op));
                }
            }
        }

        public static void VerifyTypes(this ObjectSchemaLoaderResult oslr)
        {
            var Gen = new SchemaClosureGenerator(oslr);
            Gen.Mark(oslr.Schema.Types, new List<TypeSpec> { });
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
            if (t.OnPrimitive)
            {
                return t.Primitive.Name;
            }
            else if (t.OnAlias)
            {
                return t.Alias.Name;
            }
            else if (t.OnRecord)
            {
                return t.Record.Name;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Name;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Name;
            }
            else if (t.OnClientCommand)
            {
                return t.ClientCommand.Name;
            }
            else if (t.OnServerCommand)
            {
                return t.ServerCommand.Name;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static String Version(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return "";
            }
            else if (t.OnAlias)
            {
                return t.Alias.Version;
            }
            else if (t.OnRecord)
            {
                return t.Record.Version;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Version;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Version;
            }
            else if (t.OnClientCommand)
            {
                return t.ClientCommand.Version;
            }
            else if (t.OnServerCommand)
            {
                return t.ServerCommand.Version;
            }
            else
            {
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
            if (t.OnPrimitive)
            {
                return t.Primitive.Description;
            }
            else if (t.OnAlias)
            {
                return t.Alias.Description;
            }
            else if (t.OnRecord)
            {
                return t.Record.Description;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Description;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Description;
            }
            else if (t.OnClientCommand)
            {
                return t.ClientCommand.Description;
            }
            else if (t.OnServerCommand)
            {
                return t.ServerCommand.Description;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static List<VariableDef> GenericParameters(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return t.Primitive.GenericParameters;
            }
            else if (t.OnAlias)
            {
                return t.Alias.GenericParameters;
            }
            else if (t.OnRecord)
            {
                return t.Record.GenericParameters;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.GenericParameters;
            }
            else if (t.OnEnum)
            {
                return new List<VariableDef> { };
            }
            else if (t.OnClientCommand)
            {
                return new List<VariableDef> { };
            }
            else if (t.OnServerCommand)
            {
                return new List<VariableDef> { };
            }
            else
            {
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
            return TypeFriendlyName(t, gpr => gpr);
        }
        public static String TypeFriendlyName(this TypeSpec t, Func<String, String> EvaluateGenericParameterRef)
        {
            return TypeFriendlyName(t, EvaluateGenericParameterRef, TypeFriendlyName);
        }
        public static String TypeFriendlyName(this TypeSpec Type, Func<String, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<String, String>, String> Kernel)
        {
            if (Type.OnTypeRef)
            {
                return Type.TypeRef.TypeFriendlyName();
            }
            else if (Type.OnGenericParameterRef)
            {
                return EvaluateGenericParameterRef(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "TupleOf" + String.Join("And", Type.Tuple.Select(t => Kernel(t, EvaluateGenericParameterRef)));
            }
            else if (Type.OnGenericTypeSpec)
            {
                return Kernel(Type.GenericTypeSpec.TypeSpec, EvaluateGenericParameterRef) + "Of" + String.Join("And", Type.GenericTypeSpec.ParameterValues.Select(t => TypeFriendlyName(t, EvaluateGenericParameterRef, Kernel)));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static String TypeString(this PrimitiveDef t)
        {
            var Name = t.Name;
            var Version = "";
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this AliasDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this RecordDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this TaggedUnionDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this EnumDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this ClientCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this ServerCommandDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this TypeDef t)
        {
            var Name = t.Name();
            var Version = t.Version();
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this TypeRef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String TypeString(this TypeSpec t)
        {
            return TypeString(t, gpr => "'" + gpr);
        }
        public static String TypeString(this TypeSpec t, Func<String, String> EvaluateGenericParameterRef)
        {
            return TypeString(t, EvaluateGenericParameterRef, TypeString);
        }
        public static String TypeString(this TypeSpec Type, Func<String, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<String, String>, String> Kernel)
        {
            if (Type.OnTypeRef)
            {
                return Type.TypeRef.TypeString();
            }
            else if (Type.OnGenericParameterRef)
            {
                return EvaluateGenericParameterRef(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "Tuple<" + String.Join(", ", Type.Tuple.Select(t => Kernel(t, EvaluateGenericParameterRef))) + ">";
            }
            else if (Type.OnGenericTypeSpec)
            {
                return Kernel(Type.GenericTypeSpec.TypeSpec, EvaluateGenericParameterRef) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(t => TypeString(t, EvaluateGenericParameterRef, Kernel))) + ">";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
