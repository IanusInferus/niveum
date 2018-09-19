//==========================================================================
//
//  File:        SyntaxParser.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 文法分析器
//  Version:     2018.09.19.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;

namespace Niveum.Json.Syntax
{
    public static class SyntaxParser
    {
        public static SyntaxValue ReadValue(PositionedTextReader r, Optional<Dictionary<Object, TextRange>> TextRanges)
        {
            int State = 0;
            var StartPosition = r.CurrentPosition;
            var States = new Stack<int>();
            var Rules = new Stack<SyntaxRule>();
            var CurrentRule = Optional<SyntaxRule>.Empty;

            void Push()
            {
                States.Push(State);
                Rules.Push(CurrentRule.Value);
            }
            void Proceed()
            {
                CurrentRule = Optional<SyntaxRule>.Empty;
            }
            Exception MakeIllegalSyntaxRule()
            {
                var FilePath = r.FilePath;
                var Message = CurrentRule.OnHasValue ? "IllegalSyntaxRule '" + CurrentRule.Value._Tag.ToString() + "'" : !r.EndOfText ? "InvalidChar '" + r.Peek() + "'" : "InvalidEndOfText";
                if (CurrentRule.OnHasValue && TextRanges.OnHasValue && TextRanges.Value.ContainsKey(CurrentRule.Value))
                {
                    var Range = TextRanges.Value[CurrentRule.Value];
                    if (FilePath.OnHasValue)
                    {
                        return new InvalidOperationException(FilePath.Value + Range.ToString() + ": " + Message);
                    }
                    else
                    {
                        return new InvalidOperationException(Range.ToString() + ": " + Message);
                    }
                }
                else if (FilePath.OnHasValue)
                {
                    return new InvalidOperationException(FilePath.Value + r.CurrentPosition.ToString() + ": " + Message);
                }
                else
                {
                    return new InvalidOperationException(r.CurrentPosition.ToString() + ": " + Message);
                }
            }
            T MarkRange<T>(T Rule, Object StartToken, Object EndToken)
            {
                if (TextRanges.OnHasValue)
                {
                    var d = TextRanges.Value;
                    if (d.ContainsKey(StartToken) && d.ContainsKey(EndToken))
                    {
                        var Range = new TextRange(d[StartToken].Start, d[EndToken].End);
                        d.Add(Rule, Range);
                    }
                }
                return Rule;
            }
            void Reduce1(Func<SyntaxRule, SyntaxRule> Translator)
            {
                var Rule1 = Rules.Pop();
                CurrentRule = Translator(Rule1);
                State = States.Pop();
            }
            void Reduce2(Func<SyntaxRule, SyntaxRule, SyntaxRule> Translator)
            {
                var Rule2 = Rules.Pop();
                var Rule1 = Rules.Pop();
                CurrentRule = Translator(Rule1, Rule2);
                States.Pop();
                State = States.Pop();
            }
            void Reduce3(Func<SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule> Translator)
            {
                var Rule3 = Rules.Pop();
                var Rule2 = Rules.Pop();
                var Rule1 = Rules.Pop();
                CurrentRule = Translator(Rule1, Rule2, Rule3);
                States.Pop();
                States.Pop();
                State = States.Pop();
            }
            void Reduce5(Func<SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule, SyntaxRule> Translator)
            {
                var Rule5 = Rules.Pop();
                var Rule4 = Rules.Pop();
                var Rule3 = Rules.Pop();
                var Rule2 = Rules.Pop();
                var Rule1 = Rules.Pop();
                CurrentRule = Translator(Rule1, Rule2, Rule3, Rule4, Rule5);
                States.Pop();
                States.Pop();
                States.Pop();
                States.Pop();
                State = States.Pop();
            }

            while (true)
            {
                while (CurrentRule.OnNotHasValue && !r.EndOfText)
                {
                    CurrentRule = TokenParser.ReadToken(r, TextRanges);
                    if (CurrentRule.OnHasValue && CurrentRule.Value.OnWhitespace)
                    {
                        CurrentRule = Optional<SyntaxRule>.Empty;
                    }
                }
                if (CurrentRule.OnNotHasValue)
                {
                    throw MakeIllegalSyntaxRule();
                }
                var c = CurrentRule.Value;
                if (State == 0)
                {
                    if (c.OnLeftBrace)
                    {
                        Push();
                        Proceed();
                        State = 1;
                    }
                    else if (c.OnLeftBracket)
                    {
                        Push();
                        Proceed();
                        State = 2;
                    }
                    else if (c.OnValue)
                    {
                        var Value = c.Value;
                        while (!r.EndOfText)
                        {
                            CurrentRule = TokenParser.ReadToken(r, TextRanges);
                            if (CurrentRule.OnHasValue && !CurrentRule.Value.OnWhitespace)
                            {
                                throw MakeIllegalSyntaxRule();
                            }
                        }
                        return Value;
                    }
                    else if (c.OnObject)
                    {
                        Push();
                        Proceed();
                        Reduce1(o => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateObject(o.Object), o, o)), o, o));
                    }
                    else if (c.OnArray)
                    {
                        Push();
                        Proceed();
                        Reduce1(a => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateArray(a.Array), a, a)), a, a));
                    }
                    else if (c.OnLiteral)
                    {
                        Push();
                        Proceed();
                        Reduce1(l => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateLiteral(l.Literal), l, l)), l, l));
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 1)
                {
                    if (c.OnRightBrace)
                    {
                        Push();
                        Proceed();
                        Reduce2((LeftBrace, RightBrace) => MarkRange(SyntaxRule.CreateObject(MarkRange(new SyntaxObject { Members = Optional<SyntaxMembers>.Empty }, LeftBrace, RightBrace)), LeftBrace, RightBrace));
                    }
                    else if (c.OnLiteral && c.Literal.OnStringValue)
                    {
                        Push();
                        Proceed();
                        State = 4;
                    }
                    else if (c.OnMembers)
                    {
                        Push();
                        Proceed();
                        State = 3;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 2)
                {
                    if (c.OnLeftBrace)
                    {
                        Push();
                        Proceed();
                        State = 1;
                    }
                    else if (c.OnLeftBracket)
                    {
                        Push();
                        Proceed();
                        State = 2;
                    }
                    else if (c.OnRightBracket)
                    {
                        Push();
                        Proceed();
                        Reduce2((LeftBracket, RightBracket) => MarkRange(SyntaxRule.CreateArray(MarkRange(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty }, LeftBracket, RightBracket)), LeftBracket, RightBracket));
                    }
                    else if (c.OnValue)
                    {
                        Push();
                        Proceed();
                        Reduce1(v => MarkRange(SyntaxRule.CreateElements(MarkRange(SyntaxElements.CreateSingle(v.Value), v, v)), v, v));
                    }
                    else if (c.OnObject)
                    {
                        Push();
                        Proceed();
                        Reduce1(o => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateObject(o.Object), o, o)), o, o));
                    }
                    else if (c.OnArray)
                    {
                        Push();
                        Proceed();
                        Reduce1(a => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateArray(a.Array), a, a)), a, a));
                    }
                    else if (c.OnLiteral)
                    {
                        Push();
                        Proceed();
                        Reduce1(l => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateLiteral(l.Literal), l, l)), l, l));
                    }
                    else if (c.OnElements)
                    {
                        Push();
                        Proceed();
                        State = 5;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 3)
                {
                    if (c.OnRightBrace)
                    {
                        Push();
                        Proceed();
                        Reduce3((LeftBrace, m, RightBrace) => MarkRange(SyntaxRule.CreateObject(MarkRange(new SyntaxObject { Members = m.Members }, LeftBrace, RightBrace)), LeftBrace, RightBrace));
                    }
                    else if (c.OnComma)
                    {
                        Push();
                        Proceed();
                        State = 6;
                    }
                    else if (c.OnMembers)
                    {
                        Push();
                        Proceed();
                        State = 3;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 4)
                {
                    if (c.OnColon)
                    {
                        Push();
                        Proceed();
                        State = 7;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 5)
                {
                    if (c.OnRightBracket)
                    {
                        Push();
                        Proceed();
                        Reduce3((LeftBracket, e, RightBracket) => MarkRange(SyntaxRule.CreateArray(MarkRange(new SyntaxArray { Elements = e.Elements }, LeftBracket, RightBracket)), LeftBracket, RightBracket));
                    }
                    else if (c.OnComma)
                    {
                        Push();
                        Proceed();
                        State = 8;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 6)
                {
                    if (c.OnLiteral && c.Literal.OnStringValue)
                    {
                        Push();
                        Proceed();
                        State = 9;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 7)
                {
                    if (c.OnLeftBrace)
                    {
                        Push();
                        Proceed();
                        State = 1;
                    }
                    else if (c.OnLeftBracket)
                    {
                        Push();
                        Proceed();
                        State = 2;
                    }
                    else if (c.OnRightBracket)
                    {
                        Push();
                        Proceed();
                        Reduce2((LeftBracket, RightBracket) => MarkRange(SyntaxRule.CreateArray(MarkRange(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty }, LeftBracket, RightBracket)), LeftBracket, RightBracket));
                    }
                    else if (c.OnValue)
                    {
                        Push();
                        Proceed();
                        Reduce3((s, Colon, v) => MarkRange(SyntaxRule.CreateMembers(MarkRange(SyntaxMembers.CreateSingle(new Tuple<TokenLiteral, SyntaxValue>(s.Literal, v.Value)), s, v)), s, v));
                    }
                    else if (c.OnObject)
                    {
                        Push();
                        Proceed();
                        Reduce1(o => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateObject(o.Object), o, o)), o, o));
                    }
                    else if (c.OnArray)
                    {
                        Push();
                        Proceed();
                        Reduce1(a => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateArray(a.Array), a, a)), a, a));
                    }
                    else if (c.OnLiteral)
                    {
                        Push();
                        Proceed();
                        Reduce1(l => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateLiteral(l.Literal), l, l)), l, l));
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 8)
                {
                    if (c.OnLeftBrace)
                    {
                        Push();
                        Proceed();
                        State = 1;
                    }
                    else if (c.OnLeftBracket)
                    {
                        Push();
                        Proceed();
                        State = 2;
                    }
                    else if (c.OnRightBracket)
                    {
                        Push();
                        Proceed();
                        Reduce2((LeftBracket, RightBracket) => MarkRange(SyntaxRule.CreateArray(MarkRange(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty }, LeftBracket, RightBracket)), LeftBracket, RightBracket));
                    }
                    else if (c.OnValue)
                    {
                        Push();
                        Proceed();
                        Reduce3((e, Comma, v) => MarkRange(SyntaxRule.CreateElements(MarkRange(SyntaxElements.CreateMultiple(new Tuple<SyntaxElements, SyntaxValue>(e.Elements, v.Value)), e, v)), e, v));
                    }
                    else if (c.OnObject)
                    {
                        Push();
                        Proceed();
                        Reduce1(o => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateObject(o.Object), o, o)), o, o));
                    }
                    else if (c.OnArray)
                    {
                        Push();
                        Proceed();
                        Reduce1(a => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateArray(a.Array), a, a)), a, a));
                    }
                    else if (c.OnLiteral)
                    {
                        Push();
                        Proceed();
                        Reduce1(l => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateLiteral(l.Literal), l, l)), l, l));
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 9)
                {
                    if (c.OnColon)
                    {
                        Push();
                        Proceed();
                        State = 10;
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else if (State == 10)
                {
                    if (c.OnLeftBrace)
                    {
                        Push();
                        Proceed();
                        State = 1;
                    }
                    else if (c.OnLeftBracket)
                    {
                        Push();
                        Proceed();
                        State = 2;
                    }
                    else if (c.OnRightBracket)
                    {
                        Push();
                        Proceed();
                        Reduce2((LeftBracket, RightBracket) => MarkRange(SyntaxRule.CreateArray(MarkRange(new SyntaxArray { Elements = Optional<SyntaxElements>.Empty }, LeftBracket, RightBracket)), LeftBracket, RightBracket));
                    }
                    else if (c.OnValue)
                    {
                        Push();
                        Proceed();
                        Reduce5((m, Comma, s, Colon, v) => MarkRange(SyntaxRule.CreateMembers(MarkRange(SyntaxMembers.CreateMultiple(new Tuple<SyntaxMembers, TokenLiteral, SyntaxValue>(m.Members, s.Literal, v.Value)), m, v)), m, v));
                    }
                    else if (c.OnObject)
                    {
                        Push();
                        Proceed();
                        Reduce1(o => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateObject(o.Object), o, o)), o, o));
                    }
                    else if (c.OnArray)
                    {
                        Push();
                        Proceed();
                        Reduce1(a => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateArray(a.Array), a, a)), a, a));
                    }
                    else if (c.OnLiteral)
                    {
                        Push();
                        Proceed();
                        Reduce1(l => MarkRange(SyntaxRule.CreateValue(MarkRange(SyntaxValue.CreateLiteral(l.Literal), l, l)), l, l));
                    }
                    else
                    {
                        throw MakeIllegalSyntaxRule();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
