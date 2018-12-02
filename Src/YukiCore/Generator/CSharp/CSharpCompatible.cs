//==========================================================================
//
//  File:        CSharpCompatible.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#通讯兼容代码生成器
//  Version:     2018.08.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yuki.ObjectSchema.CSharpCompatible
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpCompatible(this Schema Schema, String NamespaceName, String ClassName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName, ClassName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpCompatible(this Schema Schema, String ClassName)
        {
            return CompileToCSharpCompatible(Schema, "", ClassName);
        }
    }

    public partial class Templates
    {
        private CSharp.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new CSharp.Templates(Schema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }
        public String GetEscapedStringLiteral(String s)
        {
            return Inner.GetEscapedStringLiteral(s);
        }
        public String GetTypeString(TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public List<String> GetPrimitives(Schema Schema)
        {
            return Inner.GetPrimitives(Schema);
        }

        public class NumericString : IComparable<NumericString>
        {
            private static Regex r = new Regex(@"\d+|.");
            private List<String> Values;
            public NumericString(String s)
            {
                Values = r.Matches(s).Cast<Match>().Select(m => m.Value).ToList();
            }

            public int CompareTo(NumericString other)
            {
                var CommonLength = Math.Min(Values.Count, other.Values.Count);
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
                if (Values.Count < other.Values.Count)
                {
                    return -1;
                }
                else if (Values.Count < other.Values.Count)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private Boolean IsNullType(TypeSpec ts)
        {
            return ts.OnTypeRef && (ts.TypeRef.Name == "Unit") && (ts.TypeRef.Version == "");
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
                if (Left.GenericParameterRef.Equals(Right.GenericParameterRef, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (Left.OnTuple && Right.OnTuple)
            {
                var LeftTypes = Left.Tuple;
                var RightTypes = Right.Tuple;
                if (LeftTypes.Count != RightTypes.Count)
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
                if (LeftSpec.ParameterValues.Count != RightSpec.ParameterValues.Count)
                {
                    return false;
                }
                return LeftSpec.ParameterValues.Zip(RightSpec.ParameterValues, (l, r) => IsSameType(l, r, IgnoreVersion)).All(b => b);
            }
            return false;
        }
        private Boolean IsExistentType(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts)
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
                return ts.Tuple.All(t => IsExistentType(VersionedNameToType, t));
            }
            else if (ts.OnGenericTypeSpec)
            {
                var gts = ts.GenericTypeSpec;
                return IsExistentType(VersionedNameToType, gts.TypeSpec) && gts.ParameterValues.All(gpv => IsExistentType(VersionedNameToType, gpv));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TypeSpec Nonversioned(TypeSpec t)
        {
            if (t.OnTypeRef)
            {
                var r = t.TypeRef;
                if (r.Version != "")
                {
                    return TypeSpec.CreateTypeRef(new TypeRef { Name = r.Name, Version = "" });
                }
                return t;
            }
            else if (t.OnGenericParameterRef)
            {
                return t;
            }
            else if (t.OnTuple)
            {
                return TypeSpec.CreateTuple(t.Tuple.Select(tt => Nonversioned(tt)).ToList());
            }
            else if (t.OnGenericTypeSpec)
            {
                var gts = t.GenericTypeSpec;
                return TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = Nonversioned(gts.TypeSpec), ParameterValues = gts.ParameterValues.Select(gpv => Nonversioned(gpv)).ToList() });
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public void FillTranslatorAliasFrom(Dictionary<String, TypeDef> VersionedNameToType, AliasDef a, List<String> l)
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
                FillTranslatorRecordFrom(Name, VersionedName, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorRecordFrom(Name, VersionedName, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { new VariableDef { Name = "Value", Type = aHead.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, l, false);
            }
        }
        public void FillTranslatorAliasTo(Dictionary<String, TypeDef> VersionedNameToType, AliasDef a, List<String> l)
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
                FillTranslatorRecordTo(Name, VersionedName, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorRecordTo(Name, VersionedName, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { new VariableDef { Name = "Value", Type = aHead.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, l, false);
            }
        }
        public void FillTranslatorRecordFrom(Dictionary<String, TypeDef> VersionedNameToType, RecordDef r, List<String> l)
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
                FillTranslatorRecordFrom(Name, VersionedName, r.Fields, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorRecordFrom(Name, VersionedName, r.Fields, aHead.Fields, l, false);
            }
        }
        public void FillTranslatorRecordFrom(String Name, String VersionedName, List<VariableDef> Fields, List<VariableDef> HeadFields, List<String> l, Boolean InitialHasError)
        {
            l.AddRange(Translator_RecordFrom(Name, VersionedName, Fields, HeadFields, InitialHasError));
        }
        public void FillTranslatorRecordTo(Dictionary<String, TypeDef> VersionedNameToType, RecordDef r, List<String> l)
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
                FillTranslatorRecordTo(Name, VersionedName, r.Fields, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorRecordTo(Name, VersionedName, r.Fields, aHead.Fields, l, false);
            }
        }
        public void FillTranslatorRecordTo(String Name, String VersionedName, List<VariableDef> Fields, List<VariableDef> HeadFields, List<String> l, Boolean InitialHasError)
        {
            l.AddRange(Translator_RecordTo(Name, VersionedName, Fields, HeadFields, InitialHasError));
        }
        public void FillTranslatorTaggedUnionFrom(Dictionary<String, TypeDef> VersionedNameToType, TaggedUnionDef tu, List<String> l)
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
                FillTranslatorTaggedUnionFrom(VersionedName, GetEscapedIdentifier(Name), GetEscapedIdentifier(VersionedName), tu.Alternatives, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorTaggedUnionFrom(VersionedName, GetEscapedIdentifier(Name), GetEscapedIdentifier(VersionedName), tu.Alternatives, tuHead.Alternatives, l, false);
            }
        }
        public void FillTranslatorTaggedUnionFrom(String VersionedName, String TypeString, String VersionedTypeString, List<VariableDef> Alternatives, List<VariableDef> HeadAlternatives, List<String> l, Boolean InitialHasError)
        {
            l.AddRange(Translator_TaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, InitialHasError));
        }
        public void FillTranslatorTaggedUnionTo(Dictionary<String, TypeDef> VersionedNameToType, TaggedUnionDef tu, List<String> l)
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
                FillTranslatorTaggedUnionTo(VersionedName, GetEscapedIdentifier(Name), GetEscapedIdentifier(VersionedName), tu.Alternatives, new List<VariableDef> { }, l, true);
            }
            else
            {
                FillTranslatorTaggedUnionTo(VersionedName, GetEscapedIdentifier(Name), GetEscapedIdentifier(VersionedName), tu.Alternatives, tuHead.Alternatives, l, false);
            }
        }
        public void FillTranslatorTaggedUnionTo(String VersionedName, String TypeString, String VersionedTypeString, List<VariableDef> Alternatives, List<VariableDef> HeadAlternatives, List<String> l, Boolean InitialHasError)
        {
            l.AddRange(Translator_TaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, InitialHasError));
        }
        public void FillTranslatorEnumFrom(Dictionary<String, TypeDef> VersionedNameToType, EnumDef e, List<String> l)
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
                FillTranslatorEnumFrom(Name, VersionedName, e.Literals, new List<LiteralDef> { }, l);
            }
            else
            {
                FillTranslatorEnumFrom(Name, VersionedName, e.Literals, eHead.Literals, l);
            }
        }
        public void FillTranslatorEnumFrom(String Name, String VersionedName, List<LiteralDef> Literals, List<LiteralDef> HeadLiterals, List<String> l)
        {
            l.AddRange(Translator_EnumFrom(Name, VersionedName, Literals, HeadLiterals).Select(Line => "//" + Line));
        }
        public void FillTranslatorEnumTo(Dictionary<String, TypeDef> VersionedNameToType, EnumDef e, List<String> l)
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
                FillTranslatorEnumTo(Name, VersionedName, e.Literals, new List<LiteralDef> { }, l);
            }
            else
            {
                FillTranslatorEnumTo(Name, VersionedName, e.Literals, eHead.Literals, l);
            }
        }
        public void FillTranslatorEnumTo(String Name, String VersionedName, List<LiteralDef> Literals, List<LiteralDef> HeadLiterals, List<String> l)
        {
            l.AddRange(Translator_EnumTo(Name, VersionedName, Literals, HeadLiterals).Select(Line => "//" + Line));
        }
        public void FillTranslatorClientCommand(Dictionary<String, TypeDef> VersionedNameToType, ClientCommandDef c, List<String> l)
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
            if (c.Attributes.Any(a => a.Key == "Async"))
            {
                l.AddRange(Translator_ClientCommandAsync(Name, VersionedName));
            }
            else
            {
                l.AddRange(Translator_ClientCommand(Name, VersionedName));
            }
            if (cHead != null)
            {
                FillTranslatorRecordTo(Name + "Request", VersionedName + "Request", c.OutParameters, cHead.OutParameters, l, false);
                FillTranslatorTaggedUnionFrom(VersionedName + "Reply", Name + "Reply", VersionedName + "Reply", c.InParameters, cHead.InParameters, l, false);
            }
            else
            {
                FillTranslatorRecordTo(Name + "Request", VersionedName + "Request", c.OutParameters, new List<VariableDef> { }, l, true);
                FillTranslatorTaggedUnionFrom(VersionedName + "Reply", Name + "Reply", VersionedName + "Reply", c.InParameters, new List<VariableDef> { }, l, true);
            }
        }
        public void FillTranslatorServerCommand(Dictionary<String, TypeDef> VersionedNameToType, ServerCommandDef c, List<String> l)
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
            l.AddRange(Translator_ServerCommand(VersionedName));
            if (cHead != null)
            {
                FillTranslatorRecordFrom(Name + "Event", VersionedName + "Event", c.OutParameters, cHead.OutParameters, l, false);
            }
            else
            {
                FillTranslatorRecordFrom(Name + "Event", VersionedName + "Event", c.OutParameters, new List<VariableDef> { }, l, true);
            }
        }
        public void FillTranslatorTupleFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            l.AddRange(Translator_TupleFrom(ts.TypeFriendlyName(), GetTypeString(nts), GetTypeString(ts), ts.Tuple, nts.Tuple));
        }
        public void FillTranslatorTupleTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            l.AddRange(Translator_TupleTo(ts.TypeFriendlyName(), GetTypeString(nts), GetTypeString(ts), ts.Tuple, nts.Tuple));
        }
        public void FillTranslatorOptionalFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var Alternatives = new List<VariableDef>
            {
                new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "HasValue", Type = ElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            var HeadAlternatives = new List<VariableDef>
            {
                new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "HasValue", Type = HeadElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true);
            }
            else
            {
                FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false);
            }
        }
        public void FillTranslatorOptionalTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var Alternatives = new List<VariableDef>
            {
                new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "HasValue", Type = ElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            var HeadAlternatives = new List<VariableDef>
            {
                new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "HasValue", Type = HeadElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true);
            }
            else
            {
                FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false);
            }
        }
        public void FillTranslatorListFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
            var Result = Translator_ListFrom(VersionedTypeFriendlyName, TypeString, VersionedTypeString, VersionedElementTypeFriendlyName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorListTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
            var Result = Translator_ListTo(VersionedTypeFriendlyName, TypeString, VersionedTypeString, VersionedElementTypeFriendlyName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorSetFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
            var Result = Translator_SetFrom(VersionedTypeFriendlyName, TypeString, VersionedTypeString, VersionedElementTypeFriendlyName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorSetTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementTypeFriendlyName = ElementTypeSpec.TypeFriendlyName();
            var Result = Translator_SetTo(VersionedTypeFriendlyName, TypeString, VersionedTypeString, VersionedElementTypeFriendlyName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorMapFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var KeyTypeSpec = ts.GenericTypeSpec.ParameterValues[0];
            var ValueTypeSpec = ts.GenericTypeSpec.ParameterValues[1];
            var HeadKeyTypeSpec = nts.GenericTypeSpec.ParameterValues[0];
            var HeadValueTypeSpec = nts.GenericTypeSpec.ParameterValues[1];
            var Result = Translator_MapFrom(VersionedTypeFriendlyName, TypeString, VersionedTypeString, KeyTypeSpec, HeadKeyTypeSpec, ValueTypeSpec, HeadValueTypeSpec);
            if (!(IsExistentType(VersionedNameToType, HeadKeyTypeSpec) && IsExistentType(VersionedNameToType, HeadValueTypeSpec)))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorMapTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l)
        {
            var nts = Nonversioned(ts);
            var VersionedTypeFriendlyName = ts.TypeFriendlyName();
            var TypeString = GetTypeString(nts);
            var VersionedTypeString = GetTypeString(ts);
            var KeyTypeSpec = ts.GenericTypeSpec.ParameterValues[0];
            var ValueTypeSpec = ts.GenericTypeSpec.ParameterValues[1];
            var HeadKeyTypeSpec = nts.GenericTypeSpec.ParameterValues[0];
            var HeadValueTypeSpec = nts.GenericTypeSpec.ParameterValues[1];
            var Result = Translator_MapTo(VersionedTypeFriendlyName, TypeString, VersionedTypeString, KeyTypeSpec, HeadKeyTypeSpec, ValueTypeSpec, HeadValueTypeSpec);
            if (!(IsExistentType(VersionedNameToType, HeadKeyTypeSpec) && IsExistentType(VersionedNameToType, HeadValueTypeSpec)))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }

        private HashSet<String> TranslatedTypeFroms = new HashSet<String>();
        private HashSet<String> TranslatedTypeTos = new HashSet<String>();
        private HashSet<String> TranslatedTypeSpecFroms = new HashSet<String>();
        private HashSet<String> TranslatedTypeSpecTos = new HashSet<String>();
        public List<String> GetTranslator(ISchemaClosureGenerator SchemaClosureGenerator, Dictionary<String, TypeDef> VersionedNameToType, TypeDef t)
        {
            var l = new List<String>();
            var FromTypeDefs = new List<TypeDef> { };
            var ToTypeDefs = new List<TypeDef> { };
            var FromTypeSpecs = new List<TypeSpec> { };
            var ToTypeSpecs = new List<TypeSpec> { };

            if (t.OnClientCommand)
            {
                FillTranslatorClientCommand(VersionedNameToType, t.ClientCommand, l);
                var ToTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { }, t.ClientCommand.OutParameters.Select(p => p.Type));
                var FromTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { }, t.ClientCommand.InParameters.Select(p => p.Type));
                ToTypeDefs = ToTypeClosure.TypeDefs;
                ToTypeSpecs = ToTypeClosure.TypeSpecs;
                FromTypeDefs = FromTypeClosure.TypeDefs;
                FromTypeSpecs = FromTypeClosure.TypeSpecs;
            }
            else if (t.OnServerCommand)
            {
                FillTranslatorServerCommand(VersionedNameToType, t.ServerCommand, l);
                var FromTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { }, t.ServerCommand.OutParameters.Select(p => p.Type));
                FromTypeDefs = FromTypeClosure.TypeDefs;
                FromTypeSpecs = FromTypeClosure.TypeSpecs;
            }
            else
            {
                var ToTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { t }, new List<TypeSpec> { });
                ToTypeDefs = ToTypeClosure.TypeDefs;
                ToTypeSpecs = ToTypeClosure.TypeSpecs;
            }
            foreach (var td in FromTypeDefs)
            {
                if (td.Version() == "") { continue; }
                if (TranslatedTypeFroms.Contains(td.VersionedName())) { continue; }
                TranslatedTypeFroms.Add(td.VersionedName());
                if (td.OnAlias)
                {
                    FillTranslatorAliasFrom(VersionedNameToType, td.Alias, l);
                }
                else if (td.OnRecord)
                {
                    FillTranslatorRecordFrom(VersionedNameToType, td.Record, l);
                }
                else if (td.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionFrom(VersionedNameToType, td.TaggedUnion, l);
                }
                else if (td.OnEnum)
                {
                    FillTranslatorEnumFrom(VersionedNameToType, td.Enum, l);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var td in ToTypeDefs)
            {
                if (td.Version() == "") { continue; }
                if (TranslatedTypeTos.Contains(td.VersionedName())) { continue; }
                TranslatedTypeTos.Add(td.VersionedName());
                if (td.OnAlias)
                {
                    FillTranslatorAliasTo(VersionedNameToType, td.Alias, l);
                }
                else if (td.OnRecord)
                {
                    FillTranslatorRecordTo(VersionedNameToType, td.Record, l);
                }
                else if (td.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionTo(VersionedNameToType, td.TaggedUnion, l);
                }
                else if (td.OnEnum)
                {
                    FillTranslatorEnumTo(VersionedNameToType, td.Enum, l);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var ts in FromTypeSpecs)
            {
                if (IsSameType(ts, Nonversioned(ts), false)) { continue; }
                if (TranslatedTypeSpecFroms.Contains(ts.TypeFriendlyName())) { continue; }
                TranslatedTypeSpecFroms.Add(ts.TypeFriendlyName());
                if (ts.OnTuple)
                {
                    FillTranslatorTupleFrom(VersionedNameToType, ts, l);
                }
                else if (ts.OnGenericTypeSpec)
                {
                    var gts = ts.GenericTypeSpec;
                    if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Optional" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorOptionalFrom(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "List" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorListFrom(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Set" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorSetFrom(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Map" && gts.ParameterValues.Count == 2)
                    {
                        FillTranslatorMapFrom(VersionedNameToType, ts, l);
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.TypeSpec.TypeRef.VersionedName()));
                    }
                }
            }
            foreach (var ts in ToTypeSpecs)
            {
                if (IsSameType(ts, Nonversioned(ts), false)) { continue; }
                if (TranslatedTypeSpecTos.Contains(ts.TypeFriendlyName())) { continue; }
                TranslatedTypeSpecTos.Add(ts.TypeFriendlyName());
                if (ts.OnTuple)
                {
                    FillTranslatorTupleTo(VersionedNameToType, ts, l);
                }
                else if (ts.OnGenericTypeSpec)
                {
                    var gts = ts.GenericTypeSpec;
                    if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Optional" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorOptionalTo(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "List" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorListTo(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Set" && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorSetTo(VersionedNameToType, ts, l);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.Name == "Map" && gts.ParameterValues.Count == 2)
                    {
                        FillTranslatorMapTo(VersionedNameToType, ts, l);
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.TypeSpec.TypeRef.VersionedName()));
                    }
                }
            }
            return l;
        }
        public List<String> GetComplexTypes(Schema Schema)
        {
            var l = new List<String>();

            if (Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Any())
            {
                var ServerCommands = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).ToList();
                l.AddRange(EventPump(ServerCommands));
                l.Add("");

                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var VersionedNameToType = Schema.GetMap().ToDictionary(t => t.Key, t => t.Value);

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
                        var Translator = GetTranslator(SchemaClosureGenerator, VersionedNameToType, c);
                        l.AddRange(Translator);
                        l.Add("");
                    }
                }
            }
            else
            {
                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var VersionedNameToType = Schema.GetMap().ToDictionary(t => t.Key, t => t.Value);

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
                    var Translator = GetTranslator(SchemaClosureGenerator, VersionedNameToType, c);
                    if (Translator.Count > 0)
                    {
                        l.AddRange(Translator);
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
