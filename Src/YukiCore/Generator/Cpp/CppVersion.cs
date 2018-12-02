//==========================================================================
//
//  File:        CppVersion.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C++版本代码生成器
//  Version:     2018.08.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yuki.ObjectSchema.CppVersion
{
    public static class CodeGenerator
    {
        public static String CompileToCppVersion(this Schema Schema, String NamespaceName, IEnumerable<String> TypeNames)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName, TypeNames).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCppVersion(this Schema Schema, IEnumerable<String> TypeNames)
        {
            return CompileToCppVersion(Schema, "", TypeNames);
        }
    }

    public partial class Templates
    {
        private Cpp.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new Cpp.Templates(Schema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public List<String> GetTypeVersions(Schema Schema, IEnumerable<String> TypeNames)
        {
            var l = new List<String>();

            var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();

            var TypeNameSet = new HashSet<String>();
            foreach (var TypeName in TypeNames)
            {
                if (!TypeNameSet.Contains(TypeName))
                {
                    TypeNameSet.Add(TypeName);
                }
            }
            foreach (var t in Schema.Types)
            {
                var TypeName = t.Name();
                if (TypeNameSet.Contains(TypeName))
                {
                    var TypeFriendlyName = t.TypeFriendlyName();
                    var SubSchema = SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { t }, new List<TypeSpec> { });
                    var Hash = SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { t }, new List<TypeSpec> { }).GetNonversioned().GetNonattributed().Hash();
                    l.AddRange(GetTypeVersion(TypeFriendlyName, Hash));
                }
            }

            return l;
        }

        public List<String> WrapContents(String Namespace, List<String> Contents)
        {
            return Inner.WrapContents(Namespace, Contents);
        }
    }
}
