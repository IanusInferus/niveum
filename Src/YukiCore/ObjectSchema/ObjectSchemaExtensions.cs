//==========================================================================
//
//  File:        ObjectSchemaExtensions.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构扩展
//  Version:     2012.04.15.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema
{
    public static class ObjectSchemaExtensions
    {
        public static IEnumerable<KeyValuePair<String, TypeDef>> GetMap(this Schema s)
        {
            return s.TypeRefs.Concat(s.Types).Select(t => CollectionOperations.CreatePair(t.VersionedName(), t));
        }

        public static UInt64 Hash(this Schema s)
        {
            var Types = s.GetMap().OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Value).ToArray();
            var TypesWithoutDescription = Types.Select(t => MapWithoutDescription(t)).ToArray();

            var bs = new BinarySerializer();
            bs.PutWriter((String str, IWritableStream ws) => ws.Write(TextEncoding.UTF16.GetBytes(str)));
            var sha = new SHA1CryptoServiceProvider();
            Byte[] result;

            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Write(TypesWithoutDescription, ms);
                ms.Position = 0;

                result = sha.ComputeHash(ms.ToUnsafeStream());
            }

            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(result.Reverse().Take(8).Reverse().ToArray());
                ms.Position = 0;

                return ms.ReadUInt64B();
            }
        }

        private static TypeDef MapWithoutVersion(TypeDef t)
        {
            if (t.OnPrimitive)
            {
                var p = t.Primitive;
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters.Select(gp => MapWithoutVersion(gp)).ToArray(), Description = p.Description });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = "", GenericParameters = a.GenericParameters.Select(gp => MapWithoutVersion(gp)).ToArray(), Type = MapWithoutVersion(a.Type), Description = a.Description });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = "", GenericParameters = r.GenericParameters.Select(gp => MapWithoutVersion(gp)).ToArray(), Fields = r.Fields.Select(gp => MapWithoutVersion(gp)).ToArray(), Description = r.Description });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = "", GenericParameters = tu.GenericParameters.Select(gp => MapWithoutVersion(gp)).ToArray(), Alternatives = tu.Alternatives.Select(gp => MapWithoutVersion(gp)).ToArray(), Description = tu.Description });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = "", UnderlyingType = MapWithoutVersion(e.UnderlyingType), Literals = e.Literals, Description = e.Description });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = "", OutParameters = cc.OutParameters.Select(p => MapWithoutVersion(p)).ToArray(), InParameters = cc.InParameters.Select(p => MapWithoutVersion(p)).ToArray(), Description = cc.Description });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = "", OutParameters = sc.OutParameters.Select(p => MapWithoutVersion(p)).ToArray(), Description = sc.Description });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static VariableDef MapWithoutVersion(VariableDef v)
        {
            return new VariableDef { Name = v.Name, Type = MapWithoutVersion(v.Type), Description = v.Description };
        }
        private static TypeSpec MapWithoutVersion(TypeSpec t)
        {
            if (t.OnTypeRef)
            {
                return TypeSpec.CreateTypeRef(new TypeRef { Name = t.TypeRef.Name, Version = "" });
            }
            else if (t.OnGenericParameterRef)
            {
                return t;
            }
            else if (t.OnTuple)
            {
                return TypeSpec.CreateTuple(new TupleDef { Types = t.Tuple.Types.Select(tt => MapWithoutVersion(tt)).ToArray() });
            }
            else if (t.OnGenericTypeSpec)
            {
                var gts = t.GenericTypeSpec;
                return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = MapWithoutVersion(gts.TypeSpec), GenericParameterValues = gts.GenericParameterValues.Select(gpv => MapWithoutVersion(gpv)).ToArray() });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static GenericParameterValue MapWithoutVersion(GenericParameterValue gpv)
        {
            if (gpv.OnLiteral)
            {
                return gpv;
            }
            else if (gpv.OnTypeSpec)
            {
                return GenericParameterValue.CreateTypeSpec(MapWithoutVersion(gpv.TypeSpec));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TypeDef MapWithoutDescription(TypeDef t)
        {
            if (t.OnPrimitive)
            {
                var p = t.Primitive;
                return TypeDef.CreatePrimitive(new PrimitiveDef { Name = p.Name, GenericParameters = p.GenericParameters.Select(gp => MapWithoutDescription(gp)).ToArray(), Description = "" });
            }
            else if (t.OnAlias)
            {
                var a = t.Alias;
                return TypeDef.CreateAlias(new AliasDef { Name = a.Name, Version = a.Version, GenericParameters = a.GenericParameters.Select(gp => MapWithoutDescription(gp)).ToArray(), Type = a.Type, Description = "" });
            }
            else if (t.OnRecord)
            {
                var r = t.Record;
                return TypeDef.CreateRecord(new RecordDef { Name = r.Name, Version = r.Version, GenericParameters = r.GenericParameters.Select(gp => MapWithoutDescription(gp)).ToArray(), Fields = r.Fields.Select(gp => MapWithoutDescription(gp)).ToArray(), Description = "" });
            }
            else if (t.OnTaggedUnion)
            {
                var tu = t.TaggedUnion;
                return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = tu.Name, Version = tu.Version, GenericParameters = tu.GenericParameters.Select(gp => MapWithoutDescription(gp)).ToArray(), Alternatives = tu.Alternatives.Select(gp => MapWithoutDescription(gp)).ToArray(), Description = "" });
            }
            else if (t.OnEnum)
            {
                var e = t.Enum;
                return TypeDef.CreateEnum(new EnumDef { Name = e.Name, Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals.Select(l => MapWithoutDescription(l)).ToArray(), Description = "" });
            }
            else if (t.OnClientCommand)
            {
                var cc = t.ClientCommand;
                return TypeDef.CreateClientCommand(new ClientCommandDef { Name = cc.Name, Version = cc.Version, OutParameters = cc.OutParameters.Select(p => MapWithoutDescription(p)).ToArray(), InParameters = cc.InParameters.Select(p => MapWithoutDescription(p)).ToArray(), Description = "" });
            }
            else if (t.OnServerCommand)
            {
                var sc = t.ServerCommand;
                return TypeDef.CreateServerCommand(new ServerCommandDef { Name = sc.Name, Version = sc.Version, OutParameters = sc.OutParameters.Select(p => MapWithoutDescription(p)).ToArray(), Description = "" });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static VariableDef MapWithoutDescription(VariableDef v)
        {
            return new VariableDef { Name = v.Name, Type = v.Type, Description = "" };
        }
        private static LiteralDef MapWithoutDescription(LiteralDef l)
        {
            return new LiteralDef { Name = l.Name, Value = l.Value, Description = "" };
        }

        public static Schema GetNonversioned(this Schema s)
        {
            var Types = s.Types.Select(t => new { Original = t, Current = MapWithoutVersion(t) }).ToArray();
            var TypeRefs = s.TypeRefs.Select(t => new { Original = t, Current = MapWithoutVersion(t) }).ToArray();
            var Dict = Types.Concat(TypeRefs).ToDictionary(t => t.Original.VersionedName(), t => t.Current.VersionedName(), StringComparer.OrdinalIgnoreCase);
            var TypePaths = s.TypePaths.Select(tp => new TypePath { Name = Dict[tp.Name], Path = tp.Path }).ToArray();
            return new Schema { Types = Types.Select(t => t.Current).ToArray(), TypeRefs = TypeRefs.Select(t => t.Current).ToArray(), Imports = s.Imports, TypePaths = TypePaths };
        }
        public static Schema GetSubSchema(this Schema s, IEnumerable<TypeDef> TypeDefs, IEnumerable<TypeSpec> TypeSpecs)
        {
            var Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);

            var m = new Marker { Types = Types };
            foreach (var t in TypeDefs)
            {
                if (!(Types.ContainsKey(t.VersionedName()) && Types[t.VersionedName()] == t))
                {
                    throw new InvalidOperationException("TypeDefNotInSchema");
                }
                m.Mark(t);
            }
            foreach (var t in TypeSpecs)
            {
                m.Mark(t);
            }

            var MarkedNames = new HashSet<String>(Types.Where(p => m.Marked.Contains(p.Value)).Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

            return new Schema { Types = s.Types.Where(t => m.Marked.Contains(t)).ToArray(), TypeRefs = s.TypeRefs.Where(t => m.Marked.Contains(t)).ToArray(), Imports = s.Imports, TypePaths = s.TypePaths.Where(tp => MarkedNames.Contains(tp.Name)).ToArray() };
        }

        public static Schema Reduce(this Schema s)
        {
            return GetSubSchema(s, s.TypeRefs.Concat(s.Types).Where(t => t.OnClientCommand || t.OnServerCommand), new TypeSpec[] { });
        }

        private class Marker
        {
            public Dictionary<String, TypeDef> Types;
            public HashSet<TypeDef> Marked = new HashSet<TypeDef>();
            public void Mark(TypeDef t)
            {
                if (Marked.Contains(t)) { return; }
                Marked.Add(t);
                switch (t._Tag)
                {
                    case TypeDefTag.Primitive:
                        foreach (var gp in t.Primitive.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        break;
                    case TypeDefTag.Alias:
                        foreach (var gp in t.Alias.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        Mark(t.Alias.Type);
                        break;
                    case TypeDefTag.Record:
                        foreach (var gp in t.Record.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        foreach (var f in t.Record.Fields)
                        {
                            Mark(f.Type);
                        }
                        break;
                    case TypeDefTag.TaggedUnion:
                        foreach (var gp in t.TaggedUnion.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        foreach (var a in t.TaggedUnion.Alternatives)
                        {
                            Mark(a.Type);
                        }
                        break;
                    case TypeDefTag.Enum:
                        Mark(t.Enum.UnderlyingType);
                        break;
                    case TypeDefTag.ClientCommand:
                        foreach (var p in t.ClientCommand.InParameters)
                        {
                            Mark(p.Type);
                        }
                        foreach (var p in t.ClientCommand.OutParameters)
                        {
                            Mark(p.Type);
                        }
                        break;
                    case TypeDefTag.ServerCommand:
                        foreach (var p in t.ServerCommand.OutParameters)
                        {
                            Mark(p.Type);
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            public void Mark(TypeSpec t)
            {
                switch (t._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        var VersionedName = t.TypeRef.VersionedName();
                        if (Types.ContainsKey(VersionedName))
                        {
                            Mark(Types[VersionedName]);
                        }
                        else
                        {
                            throw new InvalidOperationException(String.Format("TypeNotExist: {0}", VersionedName));
                        }
                        break;
                    case TypeSpecTag.GenericParameterRef:
                        break;
                    case TypeSpecTag.Tuple:
                        foreach (var ts in t.Tuple.Types)
                        {
                            Mark(ts);
                        }
                        break;
                    case TypeSpecTag.GenericTypeSpec:
                        Mark(t.GenericTypeSpec.TypeSpec);
                        foreach (var gpv in t.GenericTypeSpec.GenericParameterValues)
                        {
                            if (gpv.OnTypeSpec)
                            {
                                Mark(gpv.TypeSpec);
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
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

            var m = new Marker { Types = Types };
            foreach (var t in s.Types)
            {
                m.Mark(t);
            }
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
