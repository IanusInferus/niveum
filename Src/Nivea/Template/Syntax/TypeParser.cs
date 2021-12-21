//==========================================================================
//
//  File:        TypeParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 类型词法解析器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Nivea.Template.Semantics;

namespace Nivea.Template.Syntax
{
    public static class TypeParser
    {
        private static Regex rVersion = new Regex(@"^(?<Name>.*?)\[(?<Version>.*?)\]$", RegexOptions.ExplicitCapture);
        public static void ParseNameAndVersion(String TypeString, out String Name, out String Version)
        {
            var m = rVersion.Match(TypeString);
            if (m.Success)
            {
                Name = m.Result("${Name}");
                Version = m.Result("${Version}");
                return;
            }
            Name = TypeString;
            Version = "";
        }

        public static TypeSpec ParseTypeSpec(String TypeString, Action<Object, int, int> Mark, Func<int, Exception> InvalidCharExceptionGenerator)
        {
            int InvalidCharIndex;
            var ov = TryParseTypeSpec(TypeString, Mark, out InvalidCharIndex);
            if (ov.OnNone)
            {
                throw InvalidCharExceptionGenerator(InvalidCharIndex);
            }
            return ov.Value;
        }
        public static Optional<TypeSpec> TryParseTypeSpec(String TypeString, Action<Object, int, int> Mark, out int InvalidCharIndex)
        {
            var osml = TokenParser.TrySplitSymbolMemberChain(TypeString, out InvalidCharIndex);
            if (osml.OnNone)
            {
                return Optional<TypeSpec>.Empty;
            }
            var sml = osml.Value;

            var tTotal = Optional<TypeSpec>.Empty;
            var FirstStart = 0;
            foreach (var s in sml)
            {
                var LocalInvalidCharIndex = 0;
                var oName = TokenParser.TryUnescapeSymbolName(s.Name, out LocalInvalidCharIndex);
                if (oName.OnNone)
                {
                    InvalidCharIndex = s.NameStartIndex + LocalInvalidCharIndex;
                    return Optional<TypeSpec>.Empty;
                }
                var Name = oName.Value;

                var l = new List<TypeSpec>();
                foreach (var p in s.Parameters)
                {
                    var LocalLocalInvalidCharIndex = 0;
                    var ov = TryParseTypeSpec(p.Key, (o, Start, End) => Mark(o, p.Value + Start, p.Value + End), out LocalLocalInvalidCharIndex);
                    if (ov.OnNone)
                    {
                        InvalidCharIndex = p.Value + LocalLocalInvalidCharIndex;
                        return Optional<TypeSpec>.Empty;
                    }
                    l.Add(ov.Value);
                }
                Mark(l, s.NameEndIndex, s.SymbolEndIndex);

                TypeSpec t;
                if (Name.StartsWith("'"))
                {
                    Name = new String(Name.Skip(1).ToArray());
                    t = TypeSpec.CreateGenericParameterRef(Name);
                }
                else
                {
                    String Version;
                    ParseNameAndVersion(Name, out Name, out Version);
                    var NameList = new List<String> { Name };
                    var Ref = new TypeRef { Name = NameList, Version = Version };
                    Mark(NameList, s.NameStartIndex, s.NameStartIndex + Name.Length);
                    Mark(Ref, s.NameStartIndex, s.NameEndIndex);
                    t = TypeSpec.CreateTypeRef(Ref);
                }
                Mark(t, s.NameStartIndex, s.NameEndIndex);

                if (s.Parameters.Count > 0)
                {
                    if (tTotal.OnNone && String.Equals(Name, "Tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        t = TypeSpec.CreateTuple(l);
                    }
                    else if (tTotal.OnNone && String.Equals(Name, "Array", StringComparison.OrdinalIgnoreCase) && (l.Count == 1))
                    {
                        t = TypeSpec.CreateArray(l.Single());
                    }
                    else
                    {
                        if (!t.OnTypeRef)
                        {
                            InvalidCharIndex = s.NameStartIndex;
                            return Optional<TypeSpec>.Empty;
                        }
                        var gts = new GenericTypeSpec { TypeSpec = t, ParameterValues = l };
                        Mark(gts, s.SymbolStartIndex, s.SymbolEndIndex);
                        t = TypeSpec.CreateGenericTypeSpec(gts);
                    }
                    Mark(t, s.SymbolStartIndex, s.SymbolEndIndex);
                }

                if (tTotal.OnNone)
                {
                    tTotal = t;
                    FirstStart = s.SymbolStartIndex;
                }
                else
                {
                    var tms = new TypeMemberSpec { Parent = tTotal.Value, Child = t };
                    var tt = TypeSpec.CreateMember(tms);
                    tTotal = tt;
                    Mark(tms, FirstStart, s.SymbolEndIndex);
                    Mark(tt, FirstStart, s.SymbolEndIndex);
                }
            }

            return tTotal;
        }
    }
}
