//==========================================================================
//
//  File:        TypeParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 类型词法解析器
//  Version:     2016.05.23.
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
            return ParseTypeSpec(TypeString, 0, Mark, InvalidCharExceptionGenerator);
        }

        private static TypeSpec ParseTypeSpec(String TypeString, int Offset, Action<Object, int, int> Mark, Func<int, Exception> InvalidCharExceptionGenerator)
        {
            var tsl = ParseTypeSpecLiteral(TypeString, Offset, InvalidCharExceptionGenerator);
            var TypeNamePair = tsl.TypeName;
            var TypeName = TypeNamePair.Key;
            var Parameters = tsl.Parameters;

            var Index = 0;
            foreach (var c in TypeName)
            {
                if (Char.IsWhiteSpace(c))
                {
                    throw InvalidCharExceptionGenerator(Offset + Index);
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
                return t;
            }

            if (String.Equals(TypeName, "Tuple", StringComparison.OrdinalIgnoreCase))
            {
                t = TypeSpec.CreateTuple(Parameters.Select(p => ParseTypeSpec(p.Key, p.Value, Mark, InvalidCharExceptionGenerator)).ToList());
                Mark(t, Offset, Offset + TypeString.Length);
                return t;
            }

            t = TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = t, ParameterValues = Parameters.Select(p => ParseTypeSpec(p.Key, p.Value, Mark, InvalidCharExceptionGenerator)).ToList() });
            Mark(t, Offset, Offset + TypeString.Length);
            return t;
        }

        private class TypeSpecLiteral
        {
            public KeyValuePair<String, int> TypeName;
            public List<KeyValuePair<String, int>> Parameters;
        }
        private static TypeSpecLiteral ParseTypeSpecLiteral(String TypeString, int Offset, Func<int, Exception> InvalidCharExceptionGenerator)
        {
            //State 0       开始
            //State 1+n     内部
            //State 2       后方空格
            //Level

            //State 0
            //    EndOfString -> 结束
            //    < -> State 1
            //    > -> 失败
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
            //    Space -> 前进
            //    _ -> 失败

            var TypeNameChars = new List<Char>();
            var ParamStartIndex = 0;
            var ParamChars = new List<Char>();
            var ParameterStrings = new List<KeyValuePair<String, int>>();

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
                        throw InvalidCharExceptionGenerator(Offset + Index);
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
                        Proceed();
                    }
                    else
                    {
                        throw InvalidCharExceptionGenerator(Offset + Index);
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

            return new TypeSpecLiteral { TypeName = new KeyValuePair<String, int>(TypeName.Trim(' '), TypeName.TakeWhile(cc => cc == ' ').Count()), Parameters = ParameterStrings };
        }
    }
}
