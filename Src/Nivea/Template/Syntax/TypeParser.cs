//==========================================================================
//
//  File:        TypeParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 类型词法解析器
//  Version:     2016.05.27.
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
            var ov = TryParseTypeSpec(TypeString, 0, Mark, out InvalidCharIndex);
            if (ov.OnNotHasValue)
            {
                throw InvalidCharExceptionGenerator(InvalidCharIndex);
            }
            return ov.Value;
        }
        public static Optional<TypeSpec> TryParseTypeSpec(String TypeString, Action<Object, int, int> Mark, out int InvalidCharIndex)
        {
            return TryParseTypeSpec(TypeString, 0, Mark, out InvalidCharIndex);
        }

        private static Optional<TypeSpec> TryParseTypeSpec(String TypeString, int Offset, Action<Object, int, int> Mark, out int InvalidCharIndex)
        {
            InvalidCharIndex = Offset;
            var otsl = TryParseTypeSpecLiteral(TypeString, false, Offset, out InvalidCharIndex);
            if (otsl.OnNotHasValue)
            {
                return Optional<TypeSpec>.Empty;
            }
            var tsl = otsl.Value;
            var TypeNamePair = tsl.TypeName;
            var TypeName = TypeNamePair.Key;
            var Parameters = tsl.Parameters;

            var Index = 0;
            foreach (var c in TypeName)
            {
                if (Char.IsWhiteSpace(c))
                {
                    InvalidCharIndex = Offset + Index;
                    return Optional<TypeSpec>.Empty;
                }
                Index += 1;
            }

            TypeSpec t;
            if (TypeName.StartsWith("'"))
            {
                TypeName = new String(TypeName.Skip(1).ToArray());
                t = TypeSpec.CreateGenericParameterRef(TypeName);
            }
            else
            {
                var Ref = ParseTypeRef(TypeName);
                t = TypeSpec.CreateTypeRef(Ref);
            }

            if (Parameters.Count == 0)
            {
                Mark(t, Offset + TypeNamePair.Value, Offset + TypeNamePair.Value + TypeName.Length);
            }
            else if (String.Equals(TypeName, "Tuple", StringComparison.OrdinalIgnoreCase))
            {
                var l = new List<TypeSpec>();
                foreach (var p in Parameters)
                {
                    var ov = TryParseTypeSpec(p.Key, p.Value, Mark, out InvalidCharIndex);
                    if (ov.OnNotHasValue)
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                    l.Add(ov.Value);
                }
                t = TypeSpec.CreateTuple(l);
                Mark(t, Offset, Offset + TypeString.Length);
            }
            else
            {
                var l = new List<TypeSpec>();
                foreach (var p in Parameters)
                {
                    var ov = TryParseTypeSpec(p.Key, p.Value, Mark, out InvalidCharIndex);
                    if (ov.OnNotHasValue)
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                    l.Add(ov.Value);
                }
                t = TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = t, ParameterValues = l });
                Mark(t, Offset, Offset + TypeString.Length);
            }

            if (tsl.Child.OnHasValue)
            {
                var MemberList = new Queue<KeyValuePair<TypeSpec, int>>();
                MemberList.Enqueue(new KeyValuePair<TypeSpec, int>(t, Offset + tsl.Child.Value.Value - 1));
                while (tsl.Child.OnHasValue)
                {
                    var ChildString = tsl.Child.Value.Key;
                    var ChildOffset = Offset + tsl.Child.Value.Value;
                    otsl = TryParseTypeSpecLiteral(ChildString, true, ChildOffset, out InvalidCharIndex);
                    if (otsl.OnNotHasValue)
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                    tsl = otsl.Value;
                    TypeNamePair = tsl.TypeName;
                    TypeName = TypeNamePair.Key;
                    Parameters = tsl.Parameters;

                    if (TypeName.StartsWith("'"))
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                    else
                    {
                        var Ref = ParseTypeRef(TypeName);
                        t = TypeSpec.CreateTypeRef(Ref);
                    }

                    if (Parameters.Count == 0)
                    {
                        Mark(t, ChildOffset + TypeNamePair.Value, ChildOffset + TypeNamePair.Value + TypeName.Length);
                    }
                    else
                    {
                        var l = new List<TypeSpec>();
                        foreach (var p in Parameters)
                        {
                            var ov = TryParseTypeSpec(p.Key, p.Value, Mark, out InvalidCharIndex);
                            if (ov.OnNotHasValue)
                            {
                                return Optional<TypeSpec>.Empty;
                            }
                            l.Add(ov.Value);
                        }
                        t = TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = t, ParameterValues = l });
                        Mark(t, ChildOffset, ChildOffset + ChildString.Length);
                    }
                    MemberList.Enqueue(new KeyValuePair<TypeSpec, int>(t, tsl.Child.OnHasValue ? ChildOffset + tsl.Child.Value.Value - 1 : Offset + TypeString.Length));
                }

                var pp = MemberList.Dequeue();
                t = pp.Key;
                while (MemberList.Count > 0)
                {
                    var tNext = MemberList.Peek();
                    t = TypeSpec.CreateMember(new TypeMemberSpec { Parent = t, Child = tNext.Key });
                    Mark(t, Offset, tNext.Value);
                    pp = MemberList.Dequeue();
                }
            }
            return t;
        }

        private class TypeSpecLiteral
        {
            public KeyValuePair<String, int> TypeName;
            public List<KeyValuePair<String, int>> Parameters;
            public Optional<KeyValuePair<String, int>> Child;
        }
        private static Optional<TypeSpecLiteral> TryParseTypeSpecLiteral(String TypeString, Boolean IsInChild, int Offset, out int InvalidCharIndex)
        {
            //State 0       开始
            //State 1+n     内部
            //State 2       后方空格
            //Level

            //State 0
            //    EndOfString -> 结束
            //    < -> State 1
            //    > -> 失败
            //    . -> 如果IsInChild，则后方为子结点，结束，否则加入到TypeNameChars，前进
            //    _ -> 加入到TypeNameChars，前进
            //
            //State 1
            //    EndOfString -> 错误
            //    < -> （如果Level > 0，则加入到ParamChars，否则ParamStartIndex = Index + 1），Level += 1, 前进
            //    > -> Level -= 1, 如果Level > 0，则加入到ParamChars，前进，否则State 2，前进
            //    , -> 如果Level = 1, 则提交参数到Parameters，清空ParamChars，ParamStartIndex = Index + 1，前进
            //    _ -> 加入到ParamChars，前进
            //
            //State 2
            //    EndOfString -> 结束
            //    Space -> State 3，前进
            //    . -> 后方为子结点，结束
            //    _ -> 失败
            //
            //State 3
            //    EndOfString -> 结束
            //    Space -> 前进
            //    _ -> 失败

            var TypeNameChars = new List<Char>();
            var ParamStartIndex = 0;
            var ParamChars = new List<Char>();
            var ParameterStrings = new List<KeyValuePair<String, int>>();
            var HasChild = false;
            var ChildStartIndex = 0;

            var Index = 0;
            Action Proceed = () => Index += 1;
            Func<Boolean> EndOfString = () => Index >= TypeString.Length;
            Func<String> PeekChar = () => TypeString.Substring(Index, 1);

            var State = 0;
            var Level = 0;

            while (true)
            {
                if (State == 0)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == "<")
                    {
                        State = 1;
                    }
                    else if (c == ">")
                    {
                        InvalidCharIndex = Offset + Index;
                        return Optional<TypeSpecLiteral>.Empty;
                    }
                    else if (c == ".")
                    {
                        if (IsInChild)
                        {
                            HasChild = true;
                            ChildStartIndex = Index + 1;
                            break;
                        }
                        else
                        {
                            TypeNameChars.AddRange(c);
                            Proceed();
                        }
                    }
                    else
                    {
                        TypeNameChars.AddRange(c);
                        Proceed();
                    }
                }
                else if (State == 1)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == "<")
                    {
                        if (Level > 0)
                        {
                            ParamChars.AddRange(c);
                        }
                        else
                        {
                            ParamStartIndex = Index + 1;
                        }
                        Level += 1;
                        Proceed();
                    }
                    else if (c == ">")
                    {
                        Level -= 1;
                        if (Level > 0)
                        {
                            ParamChars.AddRange(c);
                        }
                        else
                        {
                            State = 2;
                        }
                        Proceed();
                    }
                    else if (c == ",")
                    {
                        if (Level == 1)
                        {
                            var Param = new String(ParamChars.ToArray());
                            ParameterStrings.Add(new KeyValuePair<String, int>(Param.Trim(' '), ParamStartIndex + Param.TakeWhile(cc => cc == ' ').Count()));
                            ParamChars.Clear();
                            ParamStartIndex = Index + 1;
                        }
                        else
                        {
                            ParamChars.AddRange(c);
                        }
                        Proceed();
                    }
                    else
                    {
                        ParamChars.AddRange(c);
                        Proceed();
                    }
                }
                else if (State == 2)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == " ")
                    {
                        State = 3;
                        Proceed();
                    }
                    else if (c == ".")
                    {
                        HasChild = true;
                        ChildStartIndex = Index + 1;
                        break;
                    }
                    else
                    {
                        InvalidCharIndex = Offset + Index;
                        return Optional<TypeSpecLiteral>.Empty;
                    }
                }
                else if (State == 3)
                {
                    if (EndOfString()) { break; }
                    var c = PeekChar();
                    if (c == " ")
                    {
                        Proceed();
                    }
                    else
                    {
                        InvalidCharIndex = Offset + Index;
                        return Optional<TypeSpecLiteral>.Empty;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            if (ParamChars.Count > 0)
            {
                var Param = new String(ParamChars.ToArray());
                ParameterStrings.Add(new KeyValuePair<String, int>(Param.Trim(' '), ParamStartIndex + Param.TakeWhile(c => c == ' ').Count()));
                ParamChars.Clear();
            }

            var TypeName = new String(TypeNameChars.ToArray());
            var Child = Optional<KeyValuePair<String, int>>.Empty;
            if (HasChild)
            {
                Child = new KeyValuePair<String, int>(TypeString.Substring(ChildStartIndex), ChildStartIndex);
            }

            InvalidCharIndex = Offset;
            return new TypeSpecLiteral { TypeName = new KeyValuePair<String, int>(TypeName.Trim(' '), TypeName.TakeWhile(cc => cc == ' ').Count()), Parameters = ParameterStrings, Child = Child };
        }
    }
}
