//==========================================================================
//
//  File:        TypeParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 类型词法解析器
//  Version:     2016.05.31.
//  Copyright(C) F.R.C.
//
//==========================================================================

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
        public static TypeRef ParseTypeRef(String TypeString)
        {
            var m = rVersion.Match(TypeString);
            if (m.Success)
            {
                return new TypeRef
                {
                    Name = m.Result("${Name}"),
                    Version = m.Result("${Version}")
                };
            }
            return new TypeRef
            {
                Name = TypeString,
                Version = ""
            };
        }

        public static TypeSpec ParseTypeSpec(String TypeString, Action<Object, int, int> Mark, Func<int, Exception> InvalidCharExceptionGenerator)
        {
            int InvalidCharIndex;
            var ov = TryParseTypeSpec(TypeString, Mark, out InvalidCharIndex);
            if (ov.OnNotHasValue)
            {
                throw InvalidCharExceptionGenerator(InvalidCharIndex);
            }
            return ov.Value;
        }
        public static Optional<TypeSpec> TryParseTypeSpec(String TypeString, Action<Object, int, int> Mark, out int InvalidCharIndex)
        {
            var osml = TokenParser.TrySplitSymbolMemberChain(TypeString, out InvalidCharIndex);
            if (osml.OnNotHasValue)
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
                if (oName.OnNotHasValue)
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
                    if (ov.OnNotHasValue)
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
                    var Ref = ParseTypeRef(Name);
                    t = TypeSpec.CreateTypeRef(Ref);
                }
                Mark(t, s.NameStartIndex, s.NameEndIndex);

                if (s.Parameters.Count > 0)
                {
                    if (tTotal.OnNotHasValue && String.Equals(Name, "Tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        t = TypeSpec.CreateTuple(l);
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

                if (tTotal.OnNotHasValue)
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
