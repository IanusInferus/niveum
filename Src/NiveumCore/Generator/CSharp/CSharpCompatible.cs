//==========================================================================
//
//  File:        CSharpCompatible.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C#通讯兼容代码生成器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Niveum.ObjectSchema.CSharpCompatible
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpCompatible(this Schema Schema, String NamespaceName, String ImplementationNamespaceName, String ImplementationClassName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName, ImplementationNamespaceName, ImplementationClassName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpCompatible(this Schema Schema, String ImplementationNamespaceName, String ImplementationClassName)
        {
            return CompileToCSharpCompatible(Schema, "", ImplementationNamespaceName, ImplementationClassName);
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
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            return Inner.GetTypeString(Type, NamespaceName);
        }
        public TypeRef GetSuffixedTypeRef(List<String> Name, String Version, String Suffix)
        {
            return Inner.GetSuffixedTypeRef(Name, Version, Suffix);
        }
        public String GetSuffixedTypeString(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            return Inner.GetSuffixedTypeString(Name, Version, Suffix, NamespaceName);
        }
        public String GetSuffixedTypeName(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            return Inner.GetSuffixedTypeName(Name, Version, Suffix, NamespaceName);
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
            return ts.OnTypeRef && ts.TypeRef.NameMatches("Unit");
        }
        private Boolean IsSameType(TypeSpec Left, TypeSpec Right, Boolean IgnoreVersion)
        {
            if (Left.OnTypeRef && Right.OnTypeRef)
            {
                var LeftTypeRef = Left.TypeRef;
                var RightTypeRef = Right.TypeRef;
                if (LeftTypeRef.Name.SequenceEqual(RightTypeRef.Name))
                {
                    if (IgnoreVersion)
                    {
                        return true;
                    }
                    else if (LeftTypeRef.Version.SequenceEqual(RightTypeRef.Version))
                    {
                        return true;
                    }
                }
            }
            else if (Left.OnGenericParameterRef && Right.OnGenericParameterRef)
            {
                if (Left.GenericParameterRef.SequenceEqual(Right.GenericParameterRef))
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

        public void FillTranslatorAliasFrom(Dictionary<String, TypeDef> VersionedNameToType, AliasDef a, List<String> l, String NamespaceName)
        {
            var Name = a.FullName();
            AliasDef aHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnAlias)
                {
                    aHead = tHead.Alias;
                }
            }
            var VersionedSimpleName = a.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(a.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(a.GetTypeSpec(), NamespaceName);
            if (aHead == null)
            {
                FillTranslatorRecordFrom(VersionedSimpleName, TypeString, VersionedTypeString, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorRecordFrom(VersionedSimpleName, TypeString, VersionedTypeString, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { new VariableDef { Name = "Value", Type = aHead.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, l, false, NamespaceName);
            }
        }
        public void FillTranslatorAliasTo(Dictionary<String, TypeDef> VersionedNameToType, AliasDef a, List<String> l, String NamespaceName)
        {
            var Name = a.FullName();
            AliasDef aHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnAlias)
                {
                    aHead = tHead.Alias;
                }
            }
            var VersionedSimpleName = a.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(a.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(a.GetTypeSpec(), NamespaceName);
            if (aHead == null)
            {
                FillTranslatorRecordTo(VersionedSimpleName, TypeString, VersionedTypeString, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorRecordTo(VersionedSimpleName, TypeString, VersionedTypeString, new List<VariableDef> { new VariableDef { Name = "Value", Type = a.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, new List<VariableDef> { new VariableDef { Name = "Value", Type = aHead.Type, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, l, false, NamespaceName);
            }
        }
        public void FillTranslatorRecordFrom(Dictionary<String, TypeDef> VersionedNameToType, RecordDef r, List<String> l, String NamespaceName)
        {
            var Name = r.FullName();
            RecordDef rHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnRecord)
                {
                    rHead = tHead.Record;
                }
            }
            var VersionedSimpleName = r.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(r.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(r.GetTypeSpec(), NamespaceName);
            if (rHead == null)
            {
                FillTranslatorRecordFrom(VersionedSimpleName, TypeString, VersionedTypeString, r.Fields, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorRecordFrom(VersionedSimpleName, TypeString, VersionedTypeString, r.Fields, rHead.Fields, l, false, NamespaceName);
            }
        }
        public void FillTranslatorRecordFrom(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<VariableDef> Fields, List<VariableDef> HeadFields, List<String> l, Boolean InitialHasError, String NamespaceName)
        {
            l.AddRange(Translator_RecordFrom(VersionedSimpleName, TypeString, VersionedTypeString, Fields, HeadFields, InitialHasError, NamespaceName));
        }
        public void FillTranslatorRecordTo(Dictionary<String, TypeDef> VersionedNameToType, RecordDef r, List<String> l, String NamespaceName)
        {
            var Name = r.FullName();
            RecordDef rHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnRecord)
                {
                    rHead = tHead.Record;
                }
            }
            var VersionedSimpleName = r.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(r.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(r.GetTypeSpec(), NamespaceName);
            if (rHead == null)
            {
                FillTranslatorRecordTo(VersionedSimpleName, TypeString, VersionedTypeString, r.Fields, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorRecordTo(VersionedSimpleName, TypeString, VersionedTypeString, r.Fields, rHead.Fields, l, false, NamespaceName);
            }
        }
        public void FillTranslatorRecordTo(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<VariableDef> Fields, List<VariableDef> HeadFields, List<String> l, Boolean InitialHasError, String NamespaceName)
        {
            l.AddRange(Translator_RecordTo(VersionedSimpleName, TypeString, VersionedTypeString, Fields, HeadFields, InitialHasError, NamespaceName));
        }
        public void FillTranslatorTaggedUnionFrom(Dictionary<String, TypeDef> VersionedNameToType, TaggedUnionDef tu, List<String> l, String NamespaceName)
        {
            var Name = tu.FullName();
            TaggedUnionDef tuHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnTaggedUnion)
                {
                    tuHead = tHead.TaggedUnion;
                }
            }
            var VersionedSimpleName = tu.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(tu.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(tu.GetTypeSpec(), NamespaceName);
            if (tuHead == null)
            {
                FillTranslatorTaggedUnionFrom(VersionedSimpleName, TypeString, VersionedTypeString, tu.Alternatives, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorTaggedUnionFrom(VersionedSimpleName, TypeString, VersionedTypeString, tu.Alternatives, tuHead.Alternatives, l, false, NamespaceName);
            }
        }
        public void FillTranslatorTaggedUnionFrom(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<VariableDef> Alternatives, List<VariableDef> HeadAlternatives, List<String> l, Boolean InitialHasError, String NamespaceName)
        {
            l.AddRange(Translator_TaggedUnionFrom(VersionedSimpleName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, InitialHasError, NamespaceName));
        }
        public void FillTranslatorTaggedUnionTo(Dictionary<String, TypeDef> VersionedNameToType, TaggedUnionDef tu, List<String> l, String NamespaceName)
        {
            var Name = tu.FullName();
            TaggedUnionDef tuHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnTaggedUnion)
                {
                    tuHead = tHead.TaggedUnion;
                }
            }
            var VersionedSimpleName = tu.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(tu.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(tu.GetTypeSpec(), NamespaceName);
            if (tuHead == null)
            {
                FillTranslatorTaggedUnionTo(VersionedSimpleName, TypeString, VersionedTypeString, tu.Alternatives, new List<VariableDef> { }, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorTaggedUnionTo(VersionedSimpleName, TypeString, VersionedTypeString, tu.Alternatives, tuHead.Alternatives, l, false, NamespaceName);
            }
        }
        public void FillTranslatorTaggedUnionTo(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<VariableDef> Alternatives, List<VariableDef> HeadAlternatives, List<String> l, Boolean InitialHasError, String NamespaceName)
        {
            l.AddRange(Translator_TaggedUnionTo(VersionedSimpleName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, InitialHasError, NamespaceName));
        }
        public void FillTranslatorEnumFrom(Dictionary<String, TypeDef> VersionedNameToType, EnumDef e, List<String> l, String NamespaceName)
        {
            var Name = e.FullName();
            EnumDef eHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnEnum)
                {
                    eHead = tHead.Enum;
                }
            }
            var VersionedSimpleName = e.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(e.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(e.GetTypeSpec(), NamespaceName);
            if (eHead == null)
            {
                FillTranslatorEnumFrom(VersionedSimpleName, TypeString, VersionedTypeString, e.Literals, new List<LiteralDef> { }, l, NamespaceName);
            }
            else
            {
                FillTranslatorEnumFrom(VersionedSimpleName, TypeString, VersionedTypeString, e.Literals, eHead.Literals, l, NamespaceName);
            }
        }
        public void FillTranslatorEnumFrom(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<LiteralDef> Literals, List<LiteralDef> HeadLiterals, List<String> l, String NamespaceName)
        {
            l.AddRange(Translator_EnumFrom(VersionedSimpleName, TypeString, VersionedTypeString, Literals, HeadLiterals, NamespaceName).Select(Line => "//" + Line));
        }
        public void FillTranslatorEnumTo(Dictionary<String, TypeDef> VersionedNameToType, EnumDef e, List<String> l, String NamespaceName)
        {
            var Name = e.FullName();
            EnumDef eHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnEnum)
                {
                    eHead = tHead.Enum;
                }
            }
            var VersionedSimpleName = e.GetTypeSpec().SimpleName(NamespaceName);
            var TypeString = GetTypeString(Nonversioned(e.GetTypeSpec()), NamespaceName);
            var VersionedTypeString = GetTypeString(e.GetTypeSpec(), NamespaceName);
            if (eHead == null)
            {
                FillTranslatorEnumTo(VersionedSimpleName, TypeString, VersionedTypeString, e.Literals, new List<LiteralDef> { }, l, NamespaceName);
            }
            else
            {
                FillTranslatorEnumTo(VersionedSimpleName, TypeString, VersionedTypeString, e.Literals, eHead.Literals, l, NamespaceName);
            }
        }
        public void FillTranslatorEnumTo(String VersionedSimpleName, String TypeString, String VersionedTypeString, List<LiteralDef> Literals, List<LiteralDef> HeadLiterals, List<String> l, String NamespaceName)
        {
            l.AddRange(Translator_EnumTo(VersionedSimpleName, TypeString, VersionedTypeString, Literals, HeadLiterals, NamespaceName).Select(Line => "//" + Line));
        }
        public void FillTranslatorClientCommand(Dictionary<String, TypeDef> VersionedNameToType, ClientCommandDef c, List<String> l, String NamespaceName)
        {
            var Name = c.FullName();
            ClientCommandDef cHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnClientCommand)
                {
                    cHead = tHead.ClientCommand;
                }
            }
            var cHeadTypeRef = Nonversioned(c.GetTypeSpec()).TypeRef;
            var SimpleName = cHeadTypeRef.SimpleName(NamespaceName);
            var VersionedSimpleName = c.GetTypeSpec().SimpleName(NamespaceName);
            var RequestTypeString = GetSuffixedTypeString(c.Name, c.Version, "Request", NamespaceName);
            var ReplyTypeString = GetSuffixedTypeString(c.Name, c.Version, "Reply", NamespaceName);
            var RequestName = GetSuffixedTypeName(c.Name, c.Version, "Request", NamespaceName);
            var ReplyName = GetSuffixedTypeName(c.Name, c.Version, "Reply", NamespaceName);
            var UnversionedRequestTypeString = GetSuffixedTypeString(cHeadTypeRef.Name, cHeadTypeRef.Version, "Request", NamespaceName);
            var UnversionedReplyTypeString = GetSuffixedTypeString(cHeadTypeRef.Name, cHeadTypeRef.Version, "Reply", NamespaceName);
            if (c.Attributes.Any(a => a.Key == "Async"))
            {
                l.AddRange(Translator_ClientCommandAsync(SimpleName, VersionedSimpleName, RequestTypeString, ReplyTypeString, NamespaceName).Select(Line => cHead != null ? Line : "//" + Line));
            }
            else
            {
                l.AddRange(Translator_ClientCommand(SimpleName, VersionedSimpleName, RequestTypeString, ReplyTypeString, NamespaceName).Select(Line => cHead != null ? Line : "//" + Line));
            }
            if (cHead != null)
            {
                FillTranslatorRecordTo(RequestName, UnversionedRequestTypeString, RequestTypeString, c.OutParameters, cHead.OutParameters, l, false, NamespaceName);
                FillTranslatorTaggedUnionFrom(ReplyName, UnversionedReplyTypeString, ReplyTypeString, c.InParameters, cHead.InParameters, l, false, NamespaceName);
            }
            else
            {
                FillTranslatorRecordTo(RequestName, UnversionedRequestTypeString, RequestTypeString, c.OutParameters, new List<VariableDef> { }, l, true, NamespaceName);
                FillTranslatorTaggedUnionFrom(ReplyName, UnversionedReplyTypeString, ReplyTypeString, c.InParameters, new List<VariableDef> { }, l, true, NamespaceName);
            }
        }
        public void FillTranslatorServerCommand(Dictionary<String, TypeDef> VersionedNameToType, ServerCommandDef c, List<String> l, String NamespaceName)
        {
            var Name = c.FullName();
            ServerCommandDef cHead = null;
            if (VersionedNameToType.ContainsKey(Name))
            {
                var tHead = VersionedNameToType[Name];
                if (tHead.OnServerCommand)
                {
                    cHead = tHead.ServerCommand;
                }
            }
            var cHeadTypeRef = Nonversioned(c.GetTypeSpec()).TypeRef;
            var VersionedSimpleName = c.GetTypeSpec().SimpleName(NamespaceName);
            var EventTypeString = GetSuffixedTypeString(c.Name, c.Version, "Event", NamespaceName);
            var EventName = GetSuffixedTypeName(c.Name, c.Version, "Event", NamespaceName);
            var UnversionedEventTypeString = GetSuffixedTypeString(cHeadTypeRef.Name, cHeadTypeRef.Version, "Event", NamespaceName);
            l.AddRange(Translator_ServerCommand(VersionedSimpleName, EventTypeString, NamespaceName).Select(Line => cHead != null ? Line : "//" + Line));
            if (cHead != null)
            {
                FillTranslatorRecordFrom(EventName, UnversionedEventTypeString, EventTypeString, c.OutParameters, cHead.OutParameters, l, false, NamespaceName);
            }
            else
            {
                FillTranslatorRecordFrom(EventName, UnversionedEventTypeString, EventTypeString, c.OutParameters, new List<VariableDef> { }, l, true, NamespaceName);
            }
        }
        public void FillTranslatorTupleFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            l.AddRange(Translator_TupleFrom(ts.SimpleName(NamespaceName), GetTypeString(nts, NamespaceName), GetTypeString(ts, NamespaceName), ts.Tuple, nts.Tuple, NamespaceName));
        }
        public void FillTranslatorTupleTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            l.AddRange(Translator_TupleTo(ts.SimpleName(NamespaceName), GetTypeString(nts, NamespaceName), GetTypeString(ts, NamespaceName), ts.Tuple, nts.Tuple, NamespaceName));
        }
        public void FillTranslatorOptionalFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var Alternatives = new List<VariableDef>
            {
                new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String>{ "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "Some", Type = ElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            var HeadAlternatives = new List<VariableDef>
            {
                new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String>{ "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "Some", Type = HeadElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorTaggedUnionFrom(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false, NamespaceName);
            }
        }
        public void FillTranslatorOptionalTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var Alternatives = new List<VariableDef>
            {
                new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String>{ "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "Some", Type = ElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            var HeadAlternatives = new List<VariableDef>
            {
                new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String>{ "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                new VariableDef { Name = "Some", Type = HeadElementTypeSpec, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
            };
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, true, NamespaceName);
            }
            else
            {
                FillTranslatorTaggedUnionTo(VersionedName, TypeString, VersionedTypeString, Alternatives, HeadAlternatives, l, false, NamespaceName);
            }
        }
        public void FillTranslatorListFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementSimpleName = ElementTypeSpec.SimpleName(NamespaceName);
            var Result = Translator_ListFrom(VersionedSimpleName, TypeString, VersionedTypeString, VersionedElementSimpleName, NamespaceName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorListTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementSimpleName = ElementTypeSpec.SimpleName(NamespaceName);
            var Result = Translator_ListTo(VersionedSimpleName, TypeString, VersionedTypeString, VersionedElementSimpleName, NamespaceName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorSetFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementSimpleName = ElementTypeSpec.SimpleName(NamespaceName);
            var Result = Translator_SetFrom(VersionedSimpleName, TypeString, VersionedTypeString, VersionedElementSimpleName, NamespaceName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorSetTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var ElementTypeSpec = ts.GenericTypeSpec.ParameterValues.Single();
            var HeadElementTypeSpec = nts.GenericTypeSpec.ParameterValues.Single();
            var VersionedElementSimpleName = ElementTypeSpec.SimpleName(NamespaceName);
            var Result = Translator_SetTo(VersionedSimpleName, TypeString, VersionedTypeString, VersionedElementSimpleName, NamespaceName);
            if (!IsExistentType(VersionedNameToType, HeadElementTypeSpec))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorMapFrom(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var KeyTypeSpec = ts.GenericTypeSpec.ParameterValues[0];
            var ValueTypeSpec = ts.GenericTypeSpec.ParameterValues[1];
            var HeadKeyTypeSpec = nts.GenericTypeSpec.ParameterValues[0];
            var HeadValueTypeSpec = nts.GenericTypeSpec.ParameterValues[1];
            var Result = Translator_MapFrom(VersionedSimpleName, TypeString, VersionedTypeString, KeyTypeSpec, HeadKeyTypeSpec, ValueTypeSpec, HeadValueTypeSpec, NamespaceName);
            if (!(IsExistentType(VersionedNameToType, HeadKeyTypeSpec) && IsExistentType(VersionedNameToType, HeadValueTypeSpec)))
            {
                Result = Result.Select(Line => "//" + Line);
            }
            l.AddRange(Result);
        }
        public void FillTranslatorMapTo(Dictionary<String, TypeDef> VersionedNameToType, TypeSpec ts, List<String> l, String NamespaceName)
        {
            var nts = Nonversioned(ts);
            var VersionedSimpleName = ts.SimpleName(NamespaceName);
            var TypeString = GetTypeString(nts, NamespaceName);
            var VersionedTypeString = GetTypeString(ts, NamespaceName);
            var KeyTypeSpec = ts.GenericTypeSpec.ParameterValues[0];
            var ValueTypeSpec = ts.GenericTypeSpec.ParameterValues[1];
            var HeadKeyTypeSpec = nts.GenericTypeSpec.ParameterValues[0];
            var HeadValueTypeSpec = nts.GenericTypeSpec.ParameterValues[1];
            var Result = Translator_MapTo(VersionedSimpleName, TypeString, VersionedTypeString, KeyTypeSpec, HeadKeyTypeSpec, ValueTypeSpec, HeadValueTypeSpec, NamespaceName);
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
        public List<String> GetTranslator(ISchemaClosureGenerator SchemaClosureGenerator, Dictionary<String, TypeDef> VersionedNameToType, TypeDef t, String NamespaceName)
        {
            var l = new List<String>();
            var FromTypeDefs = new List<TypeDef> { };
            var ToTypeDefs = new List<TypeDef> { };
            var FromTypeSpecs = new List<TypeSpec> { };
            var ToTypeSpecs = new List<TypeSpec> { };

            if (t.OnClientCommand)
            {
                FillTranslatorClientCommand(VersionedNameToType, t.ClientCommand, l, NamespaceName);
                var ToTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { }, t.ClientCommand.OutParameters.Select(p => p.Type));
                var FromTypeClosure = SchemaClosureGenerator.GetClosure(new List<TypeDef> { }, t.ClientCommand.InParameters.Select(p => p.Type));
                ToTypeDefs = ToTypeClosure.TypeDefs;
                ToTypeSpecs = ToTypeClosure.TypeSpecs;
                FromTypeDefs = FromTypeClosure.TypeDefs;
                FromTypeSpecs = FromTypeClosure.TypeSpecs;
            }
            else if (t.OnServerCommand)
            {
                FillTranslatorServerCommand(VersionedNameToType, t.ServerCommand, l, NamespaceName);
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
                    FillTranslatorAliasFrom(VersionedNameToType, td.Alias, l, NamespaceName);
                }
                else if (td.OnRecord)
                {
                    FillTranslatorRecordFrom(VersionedNameToType, td.Record, l, NamespaceName);
                }
                else if (td.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionFrom(VersionedNameToType, td.TaggedUnion, l, NamespaceName);
                }
                else if (td.OnEnum)
                {
                    FillTranslatorEnumFrom(VersionedNameToType, td.Enum, l, NamespaceName);
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
                    FillTranslatorAliasTo(VersionedNameToType, td.Alias, l, NamespaceName);
                }
                else if (td.OnRecord)
                {
                    FillTranslatorRecordTo(VersionedNameToType, td.Record, l, NamespaceName);
                }
                else if (td.OnTaggedUnion)
                {
                    FillTranslatorTaggedUnionTo(VersionedNameToType, td.TaggedUnion, l, NamespaceName);
                }
                else if (td.OnEnum)
                {
                    FillTranslatorEnumTo(VersionedNameToType, td.Enum, l, NamespaceName);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var ts in FromTypeSpecs)
            {
                if (IsSameType(ts, Nonversioned(ts), false)) { continue; }
                if (TranslatedTypeSpecFroms.Contains(ts.SimpleName(NamespaceName))) { continue; }
                TranslatedTypeSpecFroms.Add(ts.SimpleName(NamespaceName));
                if (ts.OnTuple)
                {
                    FillTranslatorTupleFrom(VersionedNameToType, ts, l, NamespaceName);
                }
                else if (ts.OnGenericTypeSpec)
                {
                    var gts = ts.GenericTypeSpec;
                    if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Optional") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorOptionalFrom(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("List") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorListFrom(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Set") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorSetFrom(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Map") && gts.ParameterValues.Count == 2)
                    {
                        FillTranslatorMapFrom(VersionedNameToType, ts, l, NamespaceName);
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
                if (TranslatedTypeSpecTos.Contains(ts.SimpleName(NamespaceName))) { continue; }
                TranslatedTypeSpecTos.Add(ts.SimpleName(NamespaceName));
                if (ts.OnTuple)
                {
                    FillTranslatorTupleTo(VersionedNameToType, ts, l, NamespaceName);
                }
                else if (ts.OnGenericTypeSpec)
                {
                    var gts = ts.GenericTypeSpec;
                    if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Optional") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorOptionalTo(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("List") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorListTo(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Set") && gts.ParameterValues.Count == 1)
                    {
                        FillTranslatorSetTo(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else if (gts.TypeSpec.OnTypeRef && gts.TypeSpec.TypeRef.NameMatches("Map") && gts.ParameterValues.Count == 2)
                    {
                        FillTranslatorMapTo(VersionedNameToType, ts, l, NamespaceName);
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.TypeSpec.TypeRef.VersionedName()));
                    }
                }
            }
            return l;
        }
        public List<String> GetTranslators(Schema Schema, String NamespaceName)
        {
            var Blocks = new List<IEnumerable<String>>();

            if (Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Any())
            {
                var ServerCommands = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).ToList();
                Blocks.Add(EventPump(ServerCommands, NamespaceName));

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
                        var Translator = GetTranslator(SchemaClosureGenerator, VersionedNameToType, c, NamespaceName);
                        Blocks.Add(Translator);
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
                    var Translator = GetTranslator(SchemaClosureGenerator, VersionedNameToType, c, NamespaceName);
                    if (Translator.Count > 0)
                    {
                        Blocks.Add(Translator);
                    }
                }
            }

            return Blocks.Join(new String[] { "" }).ToList();
        }
        public List<String> GetTypes(Schema Schema, String NamespaceName, String ImplementationNamespaceName, String ImplementationClassName)
        {
            return Inner.WrapNamespace(ImplementationNamespaceName, WrapPartialClass(ImplementationClassName, GetTranslators(Schema, NamespaceName))).ToList();
        }
    }
}
