//==========================================================================
//
//  File:        CppVersion.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C++版本代码生成器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.CppVersion
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

        public List<String> GetTypeVersions(Schema Schema, IEnumerable<String> TypeNames, String NamespaceName)
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
                var TypeName = t.FullName();
                if (TypeNameSet.Contains(TypeName))
                {
                    var SimpleName = t.GetTypeSpec().SimpleName(NamespaceName);
                    var SubSchema = SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { t }, new List<TypeSpec> { });
                    var Hash = SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { t }, new List<TypeSpec> { }).GetNonversioned().GetNonattributed().Hash();
                    l.AddRange(GetTypeVersion(SimpleName, Hash));
                }
            }

            return l;
        }

        public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
        {
            return Inner.WrapNamespace(Namespace, Contents);
        }
    }
}
