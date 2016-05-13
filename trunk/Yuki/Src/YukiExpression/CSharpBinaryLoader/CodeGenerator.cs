//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式结构C#二进制加载器代码生成器
//  Version:     2016.05.13.
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
            var w = new Writer(Schema, NamespaceName);
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

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var ComplexTypes = GetComplexTypes(Schema);

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public List<String> GetHeader()
            {
                return GetTemplate("Header");
            }

            public List<String> GetModuleFunctionContext(FunctionDecl Function)
            {
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionContext_Parameter").Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToList();
                return GetTemplate("Module_FunctionContext").Substitute("FunctionName", Function.Name).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public List<String> GetModuleFunctionInitialize(FunctionDecl Function)
            {
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionInitialize_Parameter").Substitute("FunctionName", Function.Name).Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToList();
                return GetTemplate("Module_FunctionInitialize").Substitute("FunctionName", Function.Name).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public List<String> GetModuleFunctionCall(FunctionDecl Function)
            {
                var ParameterList = String.Join(", ", Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionCall_ParameterList_Parameter").Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToArray());
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionCall_Parameter").Substitute("FunctionName", Function.Name).Substitute("Name", p.Name)).ToList();
                return GetTemplate("Module_FunctionCall").Substitute("FunctionName", Function.Name).Substitute("ParameterList", ParameterList).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public List<String> GetModule(ModuleDecl Module)
            {
                var FunctionContexts = Module.Functions.SelectMany(f => GetModuleFunctionContext(f)).ToList();
                var FunctionInitializes = Module.Functions.SelectMany(f => GetModuleFunctionInitialize(f)).ToList();
                var FunctionCalls = Module.Functions.SelectMany(f => GetModuleFunctionCall(f)).ToList();
                return GetTemplate("Module").Substitute("Name", Module.Name).Substitute("FunctionContexts", FunctionContexts).Substitute("FunctionInitializes", FunctionInitializes).Substitute("FunctionCalls", FunctionCalls);
            }

            public List<String> GetAssembly(Schema Schema)
            {
                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                var Modules = Schema.Modules.SelectMany(m => GetTemplate("Assembly_Module").Substitute("Name", m.Name)).ToList();
                var ModuleInitializes = Schema.Modules.SelectMany(m => GetTemplate("Assembly_ModuleInitialize").Substitute("Name", m.Name)).ToList();
                return GetTemplate("Assembly").Substitute("Name", "Calculation").Substitute("Modules", Modules).Substitute("Hash", Hash).Substitute("ModuleInitializes", ModuleInitializes);
            }

            public List<String> GetComplexTypes(Schema Schema)
            {
                var l = new List<String>();

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

                return l;
            }

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
