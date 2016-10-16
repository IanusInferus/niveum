//==========================================================================
//
//  File:        CppBinaryLoader.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式结构C++二进制加载器代码生成器
//  Version:     2016.10.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using OS = Yuki.ObjectSchema;

namespace Yuki.ExpressionSchema.CppBinaryLoader
{
    public static class CodeGenerator
    {
        public static String CompileToCppBinaryLoader(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCppBinaryLoader(this Schema Schema)
        {
            return CompileToCppBinaryLoader(Schema, "");
        }
    }

    public partial class Templates
    {
        private OS.Cpp.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new OS.Cpp.Templates(new OS.Schema
            {
                Types = new List<OS.TypeDef> { },
                TypeRefs = new List<OS.TypeDef>
                    {
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Unit", GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } }),
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Boolean", GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } })
                    },
                Imports = new List<String> { }
            });
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }
        public String GetEscapedStringLiteral(String s)
        {
            return Inner.GetEscapedStringLiteral(s);
        }

        public List<String> GetComplexTypes(Schema Schema)
        {
            var l = new List<String>();

            l.AddRange(Assembly(Schema));
            l.Add("");

            foreach (var m in Schema.Modules)
            {
                l.AddRange(Module(m));
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public List<String> WrapContents(String Namespace, List<String> Contents)
        {
            return Inner.WrapContents(Namespace, Contents);
        }

        public Boolean IsInclude(String s)
        {
            return Inner.IsInclude(s);
        }
    }
}
