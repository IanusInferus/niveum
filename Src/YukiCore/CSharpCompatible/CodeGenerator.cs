//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#通讯兼容代码生成器
//  Version:     2013.12.08.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CSharpCompatible
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpCompatible(this Schema Schema, String NamespaceName, String ClassName)
        {
            Writer w = new Writer(Schema, NamespaceName, ClassName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpCompatible(this Schema Schema, String ClassName)
        {
            return CompileToCSharpCompatible(Schema, "", ClassName);
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private Dictionary<String, TypeDef> VersionedNameToType;
            private String NamespaceName;
            private String ClassName;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpCompatible);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName, String ClassName)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.VersionedNameToType = Schema.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);
                this.NamespaceName = NamespaceName;
                this.ClassName = ClassName;

                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName, false);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var EventPump = GetEventPump();
                var Translators = GetTranslators();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("ClassName", ClassName).Substitute("EventPump", EventPump).Substitute("Translators", Translators)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("ClassName", ClassName).Substitute("EventPump", EventPump).Substitute("Translators", Translators)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public String[] GetEventPump()
            {
                var ServerCommandGroups = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).GroupBy(sc => sc.Name).Where(g => g.Any(sc => sc.Version == "")).ToList();
                var ServerCommands = ServerCommandGroups.SelectMany(g => GetTemplate("EventPump_ServerCommand").Substitute("Name", g.Key)).ToArray();
                var ServerCommandInitializers = ServerCommandGroups.SelectMany(g => GetEventPumpServerCommandInitializer(g.Key, g.ToArray())).ToArray();

                return GetTemplate("EventPump").Substitute("ServerCommands", ServerCommands).Substitute("ServerCommandInitializers", ServerCommandInitializers);
            }
            public String[] GetEventPumpServerCommandInitializer(String Name, ServerCommandDef[] ServerCommands)
            {
                if (ServerCommands.Length == 1)
                {
                    return GetTemplate("EventPump_ServerCommandInitializer_HeadOnly").Substitute("Name", Name);
                }

                var SortedServerCommands = ServerCommands.Where(sc => sc.Version != "").OrderByDescending(sc => new NumericString(sc.Version)).ToList();
                var Versions = SortedServerCommands.SelectMany(sc => GetTemplate("EventPump_ServerCommandInitializer_Multiple_Version").Substitute("VersionedTypeFriendlyName", sc.TypeFriendlyName()).Substitute("Version", sc.Version)).ToArray();
                return GetTemplate("EventPump_ServerCommandInitializer_Multiple").Substitute("Name", Name).Substitute("Versions", Versions);
            }
            public class NumericString : IComparable<NumericString>
            {
                private static Regex r = new Regex(@"\d+|.");
                private String[] Values;
                public NumericString(String s)
                {
                    Values = r.Matches(s).Cast<Match>().Select(m => m.Value).ToArray();
                }

                public int CompareTo(NumericString other)
                {
                    var CommonLength = Math.Min(Values.Length, other.Values.Length);
                    for (int k = 0; k < CommonLength; k += 1)
                    {
                        var Left = Values[k];
                        var Right = other.Values[k];
                        if ((Left.Length == 1) && (Right.Length == 1))
                        {
                            var vc = String.CompareOrdinal(Left, Right);
                            if (vc != 0) { return vc; }
                            continue;
                        }
                        var LeftIsNumeric = Left.All(c => Char.IsDigit(c));
                        var RightIsNumeric = Right.All(c => Char.IsDigit(c));
                        if (!(LeftIsNumeric && RightIsNumeric))
                        {
                            var vs = String.CompareOrdinal(Left, Right);
                            if (vs != 0) { return vs; }
                            continue;
                        }
                        var LeftNumeric = Left.TrimStart('0');
                        var RightNumeric = Right.TrimStart('0');
                        if (LeftNumeric.Length < RightNumeric.Length)
                        {
                            return -1;
                        }
                        else if (LeftNumeric.Length > RightNumeric.Length)
                        {
                            return 1;
                        }
                        var v = LeftNumeric.CompareTo(RightNumeric);
                        if (v != 0)
                        {
                            return v;
                        }
                        v = Left.CompareTo(Right);
                        if (v != 0)
                        {
                            return v;
                        }
                    }
                    if (Values.Length < other.Values.Length)
                    {
                        return -1;
                    }
                    else if (Values.Length < other.Values.Length)
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            public String[] GetTranslators()
            {
                List<String> l = new List<String>();

                foreach (var c in Schema.Types)
                {
                    if (c.GenericParameters().Count() != 0)
                    {
                        continue;
                    }
                    if (c.OnPrimitive)
                    {
                        continue;
                    }
                    if (c.Version() == "")
                    {
                        continue;
                    }
                    if (c.OnClientCommand || c.OnServerCommand)
                    {
                        var Translator = GetTranslator(c);
                        l.AddRange(Translator);
                        l.Add("");
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            private HashSet<String> TranslatedTypeFroms = new HashSet<String>();
            private HashSet<String> TranslatedTypeTos = new HashSet<String>();
            private HashSet<String> TranslatedTypeSpecFroms = new HashSet<String>();
            private HashSet<String> TranslatedTypeSpecTos = new HashSet<String>();
            public String[] GetTranslator(TypeDef t)
            {
                var l = new List<String>();
                var FromTypeDefs = new TypeDef[] { };
                var ToTypeDefs = new TypeDef[] { };
                var FromTypeSpecs = new TypeSpec[] { };
                var ToTypeSpecs = new TypeSpec[] { };
                if (t.OnClientCommand)
                {
                    FillTranslatorClientCommand(t.ClientCommand, l);
                    var ToTypeClosure = SchemaClosureGenerator.GetClosure(new TypeDef[] { }, t.ClientCommand.OutParameters.Select(p => p.Type));
                    var FromTypeClosure = SchemaClosureGenerator.GetClosure(new TypeDef[] { }, t.ClientCommand.InParameters.Select(p => p.Type));
                    ToTypeDefs = ToTypeClosure.TypeDefs.ToArray();
                    ToTypeSpecs = ToTypeClosure.TypeSpecs.ToArray();
                    FromTypeDefs = FromTypeClosure.TypeDefs.ToArray();
                    FromTypeSpecs = FromTypeClosure.TypeSpecs.ToArray();
                }
                else if (t.OnServerCommand)
                {
                    FillTranslatorServerCommand(t.ServerCommand, l);
                    var FromTypeClosure = SchemaClosureGenerator.GetClosure(new TypeDef[] { }, t.ServerCommand.OutParameters.Select(p => p.Type));
                    FromTypeDefs = FromTypeClosure.TypeDefs.ToArray();
                    FromTypeSpecs = FromTypeClosure.TypeSpecs.ToArray();
                }
                else
                {
                    throw new InvalidOperationException();
                }
                foreach (var td in FromTypeDefs)
                {
                    if (td.Version() == "") { continue; }
                    if (TranslatedTypeFroms.Contains(td.VersionedName())) { continue; }
                    TranslatedTypeFroms.Add(td.VersionedName());
                    FillTranslatorFrom(td, l);
                }
                foreach (var td in ToTypeDefs)
                {
                    if (td.Version() == "") { continue; }
                    if (TranslatedTypeTos.Contains(td.VersionedName())) { continue; }
                    TranslatedTypeTos.Add(td.VersionedName());
                    FillTranslatorTo(td, l);
                }
                foreach (var ts in FromTypeSpecs)
                {
                    if (IsSameType(ts, ts.Nonversioned(), false)) { continue; }
                    if (TranslatedTypeSpecFroms.Contains(ts.TypeFriendlyName())) { continue; }
                    TranslatedTypeSpecFroms.Add(ts.TypeFriendlyName());
                    if (ts.OnTuple)
                    {
                        FillTranslatorTupleFrom(ts, l);
                    }
                    else if (ts.OnGenericTypeSpec)
                    {
                        var gts = ts.GenericTypeSpec;
                        if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Optional" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorOptionalFrom(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "List" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorListFrom(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Set" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorSetFrom(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Map" && gts.GenericParameterValues.Length == 2 && gts.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                        {
                            FillTranslatorMapFrom(ts, l);
                        }
                        else
                        {
                            throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.TypeSpec.TypeRef.VersionedName()));
                        }
                    }
                }
                foreach (var ts in ToTypeSpecs)
                {
                    if (IsSameType(ts, ts.Nonversioned(), false)) { continue; }
                    if (TranslatedTypeSpecTos.Contains(ts.TypeFriendlyName())) { continue; }
                    TranslatedTypeSpecTos.Add(ts.TypeFriendlyName());
                    if (ts.OnTuple)
                    {
                        FillTranslatorTupleTo(ts, l);
                    }
                    else if (ts.OnGenericTypeSpec)
                    {
                        var gts = ts.GenericTypeSpec;
                        if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Optional" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorOptionalTo(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "List" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorListTo(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Set" && gts.GenericParameterValues.Length == 1 && gts.GenericParameterValues.Single().OnTypeSpec)
                        {
                            FillTranslatorSetTo(ts, l);
                        }
                        else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Map" && gts.GenericParameterValues.Length == 2 && gts.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                        {
                            FillTranslatorMapTo(ts, l);
                        }
                        else
                        {
                            throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.TypeSpec.TypeRef.VersionedName()));
                        }
                    }
                }
                return l.ToArray();
            }
            public void FillTranslatorFrom(TypeDef t, List<String> l)
            {
                if (t.OnAlias)
                {
                    FillTranslatorAliasFrom(t.Alias, l);
                }
                else if (t.OnRecord)
                {
                    FillTranslatorRecordFrom(t.Record, l);
                }
                else if (t.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionFrom(t.TaggedUnion, l);
                }
                else if (t.OnEnum)
                {
                    FillTranslatorEnumFrom(t.Enum, l);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            public void FillTranslatorTo(TypeDef t, List<String> l)
            {
                if (t.OnAlias)
                {
                    FillTranslatorAliasTo(t.Alias, l);
                }
                else if (t.OnRecord)
                {
                    FillTranslatorRecordTo(t.Record, l);
                }
                else if (t.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionTo(t.TaggedUnion, l);
                }
                else if (t.OnEnum)
                {
                    FillTranslatorEnumTo(t.Enum, l);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            private Boolean IsSameType(TypeSpec Left, TypeSpec Right, Boolean IgnoreVersion)
            {
                if (Left.OnTypeRef && Right.OnTypeRef)
                {
                    var LeftTypeRef = Left.TypeRef;
                    var RightTypeRef = Right.TypeRef;
                    if (LeftTypeRef.Name.Equals(RightTypeRef.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (IgnoreVersion)
                        {
                            return true;
                        }
                        else if (LeftTypeRef.Version.Equals(RightTypeRef.Version, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                else if (Left.OnGenericParameterRef && Right.OnGenericParameterRef)
                {
                    if (Left.GenericParameterRef.Value.Equals(Right.GenericParameterRef.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (Left.OnTuple && Right.OnTuple)
                {
                    var LeftTypes = Left.Tuple.Types;
                    var RightTypes = Right.Tuple.Types;
                    if (LeftTypes.Length != RightTypes.Length)
                    {
                        return false;
                    }
                    return LeftTypes.Zip(RightTypes, (l, r) => IsSameType(l, r, IgnoreVersion)).All(b => b);
                }
                else if (Left.OnGenericTypeSpec && Right.OnGenericTypeSpec)
                {
                    var LeftSpec = Left.GenericTypeSpec;
                    var RightSpec = Right.GenericTypeSpec;
                    if (!IsSameType(LeftSpec.TypeSpec, RightSpec.TypeSpec, IgnoreVersion))
                    {
                        return false;
                    }
                    if (LeftSpec.GenericParameterValues.Length != RightSpec.GenericParameterValues.Length)
                    {
                        return false;
                    }
                    return LeftSpec.GenericParameterValues.Zip(RightSpec.GenericParameterValues, (l, r) => (l.OnLiteral && r.OnLiteral && l.Literal.Equals(r.Literal, StringComparison.OrdinalIgnoreCase)) || (l.OnTypeSpec && r.OnTypeSpec && IsSameType(l.TypeSpec, r.TypeSpec, IgnoreVersion)) || false).All(b => b);
                }
                return false;
            }
            private Boolean IsExistentType(TypeSpec ts)
            {
                if (ts.OnTypeRef)
                {
                    var r = ts.TypeRef;
                    return VersionedNameToType.ContainsKey(r.VersionedName());
                }
                else if (ts.OnGenericParameterRef)
                {
                    return true;
                }
                else if (ts.OnTuple)
                {
                    return ts.Tuple.Types.All(t => IsExistentType(t));
                }
                else if (ts.OnGenericTypeSpec)
                {
                    var gts = ts.GenericTypeSpec;
                    return IsExistentType(gts.TypeSpec) && gts.GenericParameterValues.All(gpv => !gpv.OnTypeSpec || IsExistentType(gpv.TypeSpec));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public void FillTranslatorAliasFrom(AliasDef a, List<String> l)
            {
                var Name = a.Name;
                AliasDef aHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnAlias)
                    {
                        aHead = tHead.Alias;
                    }
                }
                var VersionedName = a.TypeFriendlyName();
                if (aHead == null)
                {
                    FillTranslatorRecordFrom(Name, VersionedName, new VariableDef[] { new VariableDef { Name = "Value", Type = a.Type, Description = "" } }, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorRecordFrom(Name, VersionedName, new VariableDef[] { new VariableDef { Name = "Value", Type = a.Type, Description = "" } }, new VariableDef[] { new VariableDef { Name = "Value", Type = aHead.Type, Description = "" } }, l, false);
                }
            }
            public void FillTranslatorAliasTo(AliasDef a, List<String> l)
            {
                var Name = a.Name;
                AliasDef aHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnAlias)
                    {
                        aHead = tHead.Alias;
                    }
                }
                var VersionedName = a.TypeFriendlyName();
                if (aHead == null)
                {
                    FillTranslatorRecordTo(Name, VersionedName, new VariableDef[] { new VariableDef { Name = "Value", Type = a.Type, Description = "" } }, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorRecordTo(Name, VersionedName, new VariableDef[] { new VariableDef { Name = "Value", Type = a.Type, Description = "" } }, new VariableDef[] { new VariableDef { Name = "Value", Type = aHead.Type, Description = "" } }, l, false);
                }
            }
            public void FillTranslatorRecordFrom(RecordDef r, List<String> l)
            {
                var Name = r.Name;
                RecordDef aHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnRecord)
                    {
                        aHead = tHead.Record;
                    }
                }
                var VersionedName = r.TypeFriendlyName();
                if (aHead == null)
                {
                    FillTranslatorRecordFrom(Name, VersionedName, r.Fields, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorRecordFrom(Name, VersionedName, r.Fields, aHead.Fields, l, false);
                }
            }
            public void FillTranslatorRecordFrom(String Name, String VersionedName, VariableDef[] Fields, VariableDef[] HeadFields, List<String> l, Boolean InitialHasError)
            {
                var FieldFroms = new List<String>();
                var d = HeadFields.ToDictionary(f => f.Name);
                var HasError = InitialHasError;
                foreach (var f in Fields)
                {
                    if (f.Type.OnTypeRef && (f.Type.TypeRef.Name == "Unit") && f.Type.TypeRef.Version == "")
                    {
                        FieldFroms.AddRange(GetTemplate("Translator_FieldFrom_Unit").Substitute("Name", f.Name));
                        continue;
                    }
                    if (d.ContainsKey(f.Name))
                    {
                        var fHead = d[f.Name];
                        if (IsSameType(f.Type, fHead.Type, false))
                        {
                            FieldFroms.AddRange(GetTemplate("Translator_FieldFrom_Identity").Substitute("Name", f.Name));
                            continue;
                        }
                        else if (IsSameType(f.Type, fHead.Type, true))
                        {
                            FieldFroms.AddRange(GetTemplate("Translator_FieldFrom_Function").Substitute("Name", f.Name).Substitute("TypeFriendlyName", f.Type.TypeFriendlyName()));
                            continue;
                        }
                    }
                    FieldFroms.AddRange(GetTemplate("Translator_FieldFrom_Identity").Substitute("Name", f.Name));
                    HasError = true;
                }
                var Result = GetTemplate("Translator_RecordFrom").Substitute("Name", Name).Substitute("VersionedName", VersionedName).Substitute("FieldFroms", FieldFroms.ToArray());
                if (HasError)
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorRecordTo(RecordDef r, List<String> l)
            {
                var Name = r.Name;
                RecordDef aHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnRecord)
                    {
                        aHead = tHead.Record;
                    }
                }
                var VersionedName = r.TypeFriendlyName();
                if (aHead == null)
                {
                    FillTranslatorRecordTo(Name, VersionedName, r.Fields, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorRecordTo(Name, VersionedName, r.Fields, aHead.Fields, l, false);
                }
            }
            public void FillTranslatorRecordTo(String Name, String VersionedName, VariableDef[] Fields, VariableDef[] HeadFields, List<String> l, Boolean InitialHasError)
            {
                var FieldTos = new List<String>();
                var d = Fields.ToDictionary(f => f.Name);
                var HasError = InitialHasError;
                foreach (var fHead in HeadFields)
                {
                    if (fHead.Type.OnTypeRef && (fHead.Type.TypeRef.Name == "Unit") && fHead.Type.TypeRef.Version == "")
                    {
                        FieldTos.AddRange(GetTemplate("Translator_FieldTo_Unit").Substitute("Name", fHead.Name));
                        continue;
                    }
                    if (d.ContainsKey(fHead.Name))
                    {
                        var f = d[fHead.Name];
                        if (IsSameType(f.Type, fHead.Type, false))
                        {
                            FieldTos.AddRange(GetTemplate("Translator_FieldTo_Identity").Substitute("Name", f.Name));
                            continue;
                        }
                        else if (IsSameType(f.Type, fHead.Type, true))
                        {
                            FieldTos.AddRange(GetTemplate("Translator_FieldTo_Function").Substitute("Name", f.Name).Substitute("TypeFriendlyName", f.Type.TypeFriendlyName()));
                            continue;
                        }
                    }
                    FieldTos.AddRange(GetTemplate("Translator_FieldTo_Identity").Substitute("Name", fHead.Name));
                    HasError = true;
                }
                var Result = GetTemplate("Translator_RecordTo").Substitute("Name", Name).Substitute("VersionedName", VersionedName).Substitute("FieldTos", FieldTos.ToArray());
                if (HasError)
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorTaggedUnionFrom(TaggedUnionDef tu, List<String> l)
            {
                var Name = tu.Name;
                TaggedUnionDef tuHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnTaggedUnion)
                    {
                        tuHead = tHead.TaggedUnion;
                    }
                }
                var VersionedName = tu.TypeFriendlyName();
                if (tuHead == null)
                {
                    FillTranslatorTaggedUnionFrom(VersionedName, Name, VersionedName, tu.Alternatives, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorTaggedUnionFrom(VersionedName, Name, VersionedName, tu.Alternatives, tuHead.Alternatives, l, false);
                }
            }
            public void FillTranslatorTaggedUnionFrom(String VersionedName, String TypeString, String VersionedTypeString, VariableDef[] Alternatives, VariableDef[] HeadAlternatives, List<String> l, Boolean InitialHasError)
            {
                var AlternativeFroms = new List<String>();
                var d = Alternatives.ToDictionary(a => a.Name);
                var HasError = InitialHasError;
                foreach (var aHead in HeadAlternatives)
                {
                    if (d.ContainsKey(aHead.Name))
                    {
                        var a = d[aHead.Name];
                        if (a.Type.OnTypeRef && (a.Type.TypeRef.Name == "Unit") && a.Type.TypeRef.Version == "")
                        {
                            AlternativeFroms.AddRange(GetTemplate("Translator_AlternativeFrom_Unit").Substitute("VersionedTypeString", VersionedTypeString).Substitute("Name", a.Name));
                            continue;
                        }
                        if (IsSameType(a.Type, aHead.Type, false))
                        {
                            AlternativeFroms.AddRange(GetTemplate("Translator_AlternativeFrom_Identity").Substitute("VersionedTypeString", VersionedTypeString).Substitute("Name", a.Name));
                            continue;
                        }
                        else if (IsSameType(a.Type, aHead.Type, true))
                        {
                            AlternativeFroms.AddRange(GetTemplate("Translator_AlternativeFrom_Function").Substitute("VersionedTypeString", VersionedTypeString).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                            continue;
                        }
                    }
                    AlternativeFroms.AddRange(GetTemplate("Translator_AlternativeFrom_Identity").Substitute("VersionedTypeString", VersionedTypeString).Substitute("Name", aHead.Name));
                    HasError = true;
                }
                var Result = GetTemplate("Translator_TaggedUnionFrom").Substitute("VersionedName", VersionedName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("AlternativeFroms", AlternativeFroms.ToArray());
                if (HasError)
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorTaggedUnionTo(TaggedUnionDef tu, List<String> l)
            {
                var Name = tu.Name;
                TaggedUnionDef tuHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnTaggedUnion)
                    {
                        tuHead = tHead.TaggedUnion;
                    }
                }
                var VersionedName = tu.TypeFriendlyName();
                if (tuHead == null)
                {
                    FillTranslatorTaggedUnionTo(VersionedName, Name, VersionedName, tu.Alternatives, new VariableDef[] { }, l, true);
                }
                else
                {
                    FillTranslatorTaggedUnionTo(VersionedName, Name, VersionedName, tu.Alternatives, tuHead.Alternatives, l, false);
                }
            }
            public void FillTranslatorTaggedUnionTo(String VersionedName, String TypeString, String VersionedTypeString, VariableDef[] Alternatives, VariableDef[] HeadAlternatives, List<String> l, Boolean InitialHasError)
            {
                var AlternativeTos = new List<String>();
                var d = HeadAlternatives.ToDictionary(a => a.Name);
                var HasError = InitialHasError;
                foreach (var a in Alternatives)
                {
                    if (d.ContainsKey(a.Name))
                    {
                        var aHead = d[a.Name];
                        if (aHead.Type.OnTypeRef && (aHead.Type.TypeRef.Name == "Unit") && aHead.Type.TypeRef.Version == "")
                        {
                            AlternativeTos.AddRange(GetTemplate("Translator_AlternativeTo_Unit").Substitute("VersionedTypeString", VersionedTypeString).Substitute("Name", a.Name));
                            continue;
                        }
                        if (IsSameType(a.Type, aHead.Type, false))
                        {
                            AlternativeTos.AddRange(GetTemplate("Translator_AlternativeTo_Identity").Substitute("TypeString", TypeString).Substitute("Name", a.Name));
                            continue;
                        }
                        else if (IsSameType(a.Type, aHead.Type, true))
                        {
                            AlternativeTos.AddRange(GetTemplate("Translator_AlternativeTo_Function").Substitute("TypeString", TypeString).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                            continue;
                        }
                    }
                    AlternativeTos.AddRange(GetTemplate("Translator_AlternativeTo_Identity").Substitute("TypeString", TypeString).Substitute("Name", a.Name));
                    HasError = true;
                }
                var Result = GetTemplate("Translator_TaggedUnionTo").Substitute("VersionedName", VersionedName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("AlternativeTos", AlternativeTos.ToArray());
                if (HasError)
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorEnumFrom(EnumDef e, List<String> l)
            {
                var Name = e.Name;
                EnumDef eHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnEnum)
                    {
                        eHead = tHead.Enum;
                    }
                }
                var VersionedName = e.TypeFriendlyName();
                if (eHead == null)
                {
                    FillTranslatorEnumFrom(Name, VersionedName, e.Literals, new LiteralDef[] { }, l);
                }
                else
                {
                    FillTranslatorEnumFrom(Name, VersionedName, e.Literals, eHead.Literals, l);
                }
            }
            public void FillTranslatorEnumFrom(String Name, String VersionedName, LiteralDef[] Literals, LiteralDef[] HeadLiterals, List<String> l)
            {
                var LiteralFroms = new List<String>();
                foreach (var ltl in Literals)
                {
                    LiteralFroms.AddRange(GetTemplate("Translator_LiteralFrom").Substitute("TaggedUnionName", Name).Substitute("VersionedTaggedUnionName", VersionedName).Substitute("Name", ltl.Name));
                }
                var Result = GetTemplate("Translator_EnumFrom").Substitute("Name", Name).Substitute("VersionedName", VersionedName).Substitute("LiteralFroms", LiteralFroms.ToArray()).AsComment();
                l.AddRange(Result);
            }
            public void FillTranslatorEnumTo(EnumDef e, List<String> l)
            {
                var Name = e.Name;
                EnumDef eHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnEnum)
                    {
                        eHead = tHead.Enum;
                    }
                }
                var VersionedName = e.TypeFriendlyName();
                if (eHead == null)
                {
                    FillTranslatorEnumTo(Name, VersionedName, e.Literals, new LiteralDef[] { }, l);
                }
                else
                {
                    FillTranslatorEnumTo(Name, VersionedName, e.Literals, eHead.Literals, l);
                }
            }
            public void FillTranslatorEnumTo(String Name, String VersionedName, LiteralDef[] Literals, LiteralDef[] HeadLiterals, List<String> l)
            {
                var LiteralTos = new List<String>();
                foreach (var ltl in Literals)
                {
                    LiteralTos.AddRange(GetTemplate("Translator_LiteralTo").Substitute("TaggedUnionName", Name).Substitute("VersionedTaggedUnionName", VersionedName).Substitute("Name", ltl.Name));
                }
                var Result = GetTemplate("Translator_EnumTo").Substitute("Name", Name).Substitute("VersionedName", VersionedName).Substitute("LiteralTos", LiteralTos.ToArray()).AsComment();
                l.AddRange(Result);
            }
            public void FillTranslatorClientCommand(ClientCommandDef c, List<String> l)
            {
                var Name = c.Name;
                ClientCommandDef cHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnClientCommand)
                    {
                        cHead = tHead.ClientCommand;
                    }
                }
                var VersionedName = c.TypeFriendlyName();
                if (cHead == null)
                {
                    l.AddRange(GetTemplate("Translator_ClientCommand").Substitute("Name", Name).Substitute("VersionedName", VersionedName));
                }
                else
                {
                    l.AddRange(GetTemplate("Translator_ClientCommand").Substitute("Name", Name).Substitute("VersionedName", VersionedName));
                    FillTranslatorRecordTo(Name + "Request", VersionedName + "Request", c.OutParameters, cHead.OutParameters, l, false);
                    FillTranslatorTaggedUnionFrom(VersionedName + "Reply", Name + "Reply", VersionedName + "Reply", c.InParameters, cHead.InParameters, l, false);
                }
            }
            public void FillTranslatorServerCommand(ServerCommandDef c, List<String> l)
            {
                var Name = c.Name;
                ServerCommandDef cHead = null;
                if (VersionedNameToType.ContainsKey(Name))
                {
                    var tHead = VersionedNameToType[Name];
                    if (tHead.OnServerCommand)
                    {
                        cHead = tHead.ServerCommand;
                    }
                }
                var VersionedName = c.TypeFriendlyName();
                if (cHead == null)
                {
                    l.AddRange(GetTemplate("Translator_ServerCommand").Substitute("VersionedName", VersionedName));
                    FillTranslatorRecordFrom(Name + "Event", VersionedName + "Event", c.OutParameters, cHead.OutParameters, l, true);
                }
                else
                {
                    l.AddRange(GetTemplate("Translator_ServerCommand").Substitute("VersionedName", VersionedName));
                    FillTranslatorRecordFrom(Name + "Event", VersionedName + "Event", c.OutParameters, cHead.OutParameters, l, false);
                }
            }
            public void FillTranslatorTupleFrom(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var Name = nts.TypeFriendlyName();
                var Fields = ts.Tuple.Types.Select((t, i) => new VariableDef { Name = "Item" + i.ToInvariantString(), Type = t, Description = "" }).ToList();
                var HeadFields = nts.Tuple.Types.Select((t, i) => new VariableDef { Name = "Item" + i.ToInvariantString(), Type = t, Description = "" }).ToList();
                var VersionedName = ts.TypeFriendlyName();
                FillTranslatorRecordFrom(Name, VersionedName, Fields.ToArray(), HeadFields.ToArray(), l, false);
            }
            public void FillTranslatorTupleTo(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var Name = nts.TypeFriendlyName();
                var Fields = ts.Tuple.Types.Select((t, i) => new VariableDef { Name = "Item" + i.ToInvariantString(), Type = t, Description = "" }).ToList();
                var HeadFields = nts.Tuple.Types.Select((t, i) => new VariableDef { Name = "Item" + i.ToInvariantString(), Type = t, Description = "" }).ToList();
                var VersionedName = ts.TypeFriendlyName();
                FillTranslatorRecordTo(Name, VersionedName, Fields.ToArray(), HeadFields.ToArray(), l, false);
            }
            public void FillTranslatorOptionalFrom(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = new VariableDef[]
                {
                    new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" },
                    new VariableDef { Name = "HasValue", Type = ElementTypeSpec, Description = "" }
                };
                var HeadAlternatives = new VariableDef[]
                {
                    new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" },
                    new VariableDef { Name = "HasValue", Type = HeadElementTypeSpec, Description = "" }
                };
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true);
                }
                else
                {
                    FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false);
                }
            }
            public void FillTranslatorOptionalTo(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = new VariableDef[]
                {
                    new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" },
                    new VariableDef { Name = "HasValue", Type = ElementTypeSpec, Description = "" }
                };
                var HeadAlternatives = new VariableDef[]
                {
                    new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" },
                    new VariableDef { Name = "HasValue", Type = HeadElementTypeSpec, Description = "" }
                };
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true);
                }
                else
                {
                    FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false);
                }
            }
            public void FillTranslatorListFrom(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
                var Result = GetTemplate("Translator_ListFrom").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("VersionedElementTypeFriendlyName", VersionedElementTypeFriendlyName);
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorListTo(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
                var Result = GetTemplate("Translator_ListTo").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("VersionedElementTypeFriendlyName", VersionedElementTypeFriendlyName);
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorSetFrom(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
                var Result = GetTemplate("Translator_SetFrom").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("VersionedElementTypeFriendlyName", VersionedElementTypeFriendlyName);
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorSetTo(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var ElementTypeSpec = ts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var HeadElementTypeSpec = nts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
                var Result = GetTemplate("Translator_SetTo").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("VersionedElementTypeFriendlyName", VersionedElementTypeFriendlyName);
                if (!IsExistentType(HeadElementTypeSpec))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorMapFrom(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var KeyTypeSpec = ts.GenericTypeSpec.GenericParameterValues[0].TypeSpec;
                var ValueTypeSpec = ts.GenericTypeSpec.GenericParameterValues[1].TypeSpec;
                var HeadKeyTypeSpec = nts.GenericTypeSpec.GenericParameterValues[0].TypeSpec;
                var HeadValueTypeSpec = nts.GenericTypeSpec.GenericParameterValues[1].TypeSpec;
                String[] KeyFrom;
                String[] ValueFrom;
                if (IsSameType(KeyTypeSpec, HeadKeyTypeSpec, false))
                {
                    KeyFrom = GetTemplate("Translator_KeyValueFrom_Identity").Substitute("Name", "Key");
                }
                else
                {
                    KeyFrom = GetTemplate("Translator_KeyValueFrom_Function").Substitute("Name", "Key").Substitute("TypeFriendlyName", KeyTypeSpec.TypeFriendlyName());
                }
                if (IsSameType(ValueTypeSpec, HeadValueTypeSpec, false))
                {
                    ValueFrom = GetTemplate("Translator_KeyValueFrom_Identity").Substitute("Name", "Value");
                }
                else
                {
                    ValueFrom = GetTemplate("Translator_KeyValueFrom_Identity").Substitute("Name", "Value").Substitute("TypeFriendlyName", ValueTypeSpec.TypeFriendlyName());
                }
                var Result = GetTemplate("Translator_MapFrom").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("KeyFrom", KeyFrom).Substitute("ValueFrom", ValueFrom);
                if (!(IsExistentType(HeadKeyTypeSpec) && IsExistentType(HeadValueTypeSpec)))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }
            public void FillTranslatorMapTo(TypeSpec ts, List<String> l)
            {
                var nts = ts.Nonversioned();
                var VersionedTypeFriendlyName = ts.TypeFriendlyName();
                var TypeString = GetTypeString(nts);
                var VersionedTypeString = GetTypeString(ts);
                var KeyTypeSpec = ts.GenericTypeSpec.GenericParameterValues[0].TypeSpec;
                var ValueTypeSpec = ts.GenericTypeSpec.GenericParameterValues[1].TypeSpec;
                var HeadKeyTypeSpec = nts.GenericTypeSpec.GenericParameterValues[0].TypeSpec;
                var HeadValueTypeSpec = nts.GenericTypeSpec.GenericParameterValues[1].TypeSpec;
                String[] KeyTo;
                String[] ValueTo;
                if (IsSameType(KeyTypeSpec, HeadKeyTypeSpec, false))
                {
                    KeyTo = GetTemplate("Translator_KeyValueTo_Identity").Substitute("Name", "Key");
                }
                else
                {
                    KeyTo = GetTemplate("Translator_KeyValueTo_Function").Substitute("Name", "Key").Substitute("TypeFriendlyName", KeyTypeSpec.TypeFriendlyName());
                }
                if (IsSameType(ValueTypeSpec, HeadValueTypeSpec, false))
                {
                    ValueTo = GetTemplate("Translator_KeyValueTo_Identity").Substitute("Name", "Value");
                }
                else
                {
                    ValueTo = GetTemplate("Translator_KeyValueTo_Identity").Substitute("Name", "Value").Substitute("TypeFriendlyName", ValueTypeSpec.TypeFriendlyName());
                }
                var Result = GetTemplate("Translator_MapTo").Substitute("VersionedTypeFriendlyName", VersionedTypeFriendlyName).Substitute("TypeString", TypeString).Substitute("VersionedTypeString", VersionedTypeString).Substitute("KeyTo", KeyTo).Substitute("ValueTo", ValueTo);
                if (!(IsExistentType(HeadKeyTypeSpec) && IsExistentType(HeadValueTypeSpec)))
                {
                    Result = Result.AsComment();
                }
                l.AddRange(Result);
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static String[] GetLines(String Value)
            {
                return CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static TypeSpec Nonversioned(this TypeSpec t)
        {
            switch (t._Tag)
            {
                case TypeSpecTag.TypeRef:
                    var r = t.TypeRef;
                    if (r.Version != "")
                    {
                        return TypeSpec.CreateTypeRef(new TypeRef { Name = r.Name, Version = "" });
                    }
                    return t;
                case TypeSpecTag.GenericParameterRef:
                    return t;
                case TypeSpecTag.Tuple:
                    return TypeSpec.CreateTuple(new TupleDef { Types = t.Tuple.Types.Select(tt => Nonversioned(tt)).ToArray() });
                case TypeSpecTag.GenericTypeSpec:
                    var gts = t.GenericTypeSpec;
                    return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = Nonversioned(gts.TypeSpec), GenericParameterValues = gts.GenericParameterValues.Select(gpv => Nonversioned(gpv)).ToArray() });
                default:
                    throw new InvalidOperationException();
            }
        }
        private static GenericParameterValue Nonversioned(GenericParameterValue gpv)
        {
            if (gpv.OnLiteral)
            {
                return gpv;
            }
            else if (gpv.OnTypeSpec)
            {
                return GenericParameterValue.CreateTypeSpec(Nonversioned(gpv.TypeSpec));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] AsComment(this String[] Lines)
        {
            return Lines.Select(Line => "//" + Line).ToArray();
        }
    }
}
