//==========================================================================
//
//  File:        CSharpRetry.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C#重试循环代码生成器
//  Version:     2017.07.18.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.CSharpRetry
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpRetry(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpRetry(this Schema Schema)
        {
            return CompileToCSharpRetry(Schema, "");
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

        public List<String> GetPrimitives(Schema Schema)
        {
            return Inner.GetPrimitives(Schema);
        }

        public List<String> GetComplexTypes(Schema Schema)
        {
            var l = new List<String>();

            var cl = new List<TypeDef>();

            foreach (var c in Schema.Types)
            {
                if (c.OnClientCommand)
                {
                    cl.Add(c);
                }
                else if (c.OnServerCommand)
                {
                    cl.Add(c);
                }
            }

            if (cl.Count > 0)
            {
                l.AddRange(RetryWrapper(cl));
                l.Add("");
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
