//==========================================================================
//
//  File:        Translator.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 转换器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using Niveum.Json.Syntax;

namespace Niveum.Json.Semantics
{
    public static class Translator
    {
        public static JToken Translate(SyntaxValue v)
        {
            if (v.OnLiteral)
            {
                var l = v.Literal;
                if (l.OnNullValue)
                {
                    return new JValue();
                }
                else if (l.OnBooleanValue)
                {
                    return new JValue(l.BooleanValue);
                }
                else if (l.OnNumberValue)
                {
                    return new JValue(l.NumberValue);
                }
                else if (l.OnStringValue)
                {
                    return new JValue(l.StringValue);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (v.OnObject)
            {
                return new JObject(TranslateMembers(v.Object.Members));
            }
            else if (v.OnArray)
            {
                return new JArray(TranslateElements(v.Array.Elements));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static Dictionary<String, JToken> TranslateMembers(Optional<SyntaxMembers> Members)
        {
            if (Members.OnNone) { return new Dictionary<String, JToken>(); }
            var CurrentMembers = Members.Value;
            var s = new Stack<Tuple<SyntaxMembers, TokenLiteral, SyntaxValue>>();
            while (CurrentMembers.OnMultiple)
            {
                s.Push(CurrentMembers.Multiple);
                CurrentMembers = CurrentMembers.Multiple.Item1;
            }
            var jMembers = new Dictionary<String, JToken>();
            jMembers.Add(CurrentMembers.Single.Item1.StringValue, Translate(CurrentMembers.Single.Item2));
            while (s.Count > 0)
            {
                var t = s.Pop();
                jMembers.Add(t.Item2.StringValue, Translate(t.Item3));
            }
            return jMembers;
        }
        private static List<JToken> TranslateElements(Optional<SyntaxElements> Elements)
        {
            if (Elements.OnNone) { return new List<JToken>(); }
            var CurrentElements = Elements.Value;
            var s = new Stack<Tuple<SyntaxElements, SyntaxValue>>();
            while (CurrentElements.OnMultiple)
            {
                s.Push(CurrentElements.Multiple);
                CurrentElements = CurrentElements.Multiple.Item1;
            }
            var jElements = new List<JToken>();
            jElements.Add(Translate(CurrentElements.Single));
            while (s.Count > 0)
            {
                jElements.Add(Translate(s.Pop().Item2));
            }
            return jElements;
        }
    }
}
