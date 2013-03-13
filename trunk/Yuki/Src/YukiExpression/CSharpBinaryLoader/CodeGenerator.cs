//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式结构C#二进制加载器代码生成器
//  Version:     2013.03.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using Yuki.ObjectSchema;
using CSharp = Yuki.ObjectSchema.CSharp;

namespace Yuki.ExpressionSchema.CSharpBinaryLoader
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpBinaryLoader(this Schema Schema, String NamespaceName)
        {
            Writer w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpBinaryLoader(this Schema Schema)
        {
            return CompileToCSharpBinaryLoader(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String NamespaceName;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Yuki.ObjectSchema.Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpBinaryLoader);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
            }

            public String[] GetSchema()
            {
                InnerWriter = new CSharp.Common.CodeGenerator.Writer(null, NamespaceName, true);

                var Header = GetHeader();
                var ComplexTypes = GetComplexTypes(Schema);

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports.ToArray()).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports.ToArray()).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            public String[] GetModuleFunctionContext(FunctionDecl Function)
            {
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionContext_Parameter").Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToArray();
                return GetTemplate("Module_FunctionContext").Substitute("FunctionName", Function.Name).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public String[] GetModuleFunctionInitialize(FunctionDecl Function)
            {
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionInitialize_Parameter").Substitute("FunctionName", Function.Name).Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToArray();
                return GetTemplate("Module_FunctionInitialize").Substitute("FunctionName", Function.Name).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public String[] GetModuleFunctionCall(FunctionDecl Function)
            {
                var ParameterList = String.Join(", ", Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionCall_ParameterList_Parameter").Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToArray());
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionCall_Parameter").Substitute("FunctionName", Function.Name).Substitute("Name", p.Name)).ToArray();
                return GetTemplate("Module_FunctionCall").Substitute("FunctionName", Function.Name).Substitute("ParameterList", ParameterList).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public String[] GetModule(ModuleDecl Module)
            {
                var FunctionContexts = Module.Functions.SelectMany(f => GetModuleFunctionContext(f)).ToArray();
                var FunctionInitializes = Module.Functions.SelectMany(f => GetModuleFunctionInitialize(f)).ToArray();
                var FunctionCalls = Module.Functions.SelectMany(f => GetModuleFunctionCall(f)).ToArray();
                return GetTemplate("Module").Substitute("Name", Module.Name).Substitute("FunctionContexts", FunctionContexts).Substitute("FunctionInitializes", FunctionInitializes).Substitute("FunctionCalls", FunctionCalls);
            }

            public String[] GetAssembly(Schema Schema)
            {
                var Modules = Schema.Modules.SelectMany(m => GetTemplate("Assembly_Module").Substitute("Name", m.Name)).ToArray();
                var ModuleInitializes = Schema.Modules.SelectMany(m => GetTemplate("Assembly_ModuleInitialize").Substitute("Name", m.Name)).ToArray();
                return GetTemplate("Assembly").Substitute("Name", "Calculation").Substitute("Modules", Modules).Substitute("ModuleInitializes", ModuleInitializes);
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                l.AddRange(GetAssembly(Schema));
                l.Add("");

                foreach (var m in Schema.Modules)
                {
                    l.AddRange(GetModule(m));
                    l.Add("");
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public String[] GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n');
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                return InnerWriter.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return InnerWriter.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
