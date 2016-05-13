//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式结构C++二进制加载器代码生成器
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
using Cpp = Yuki.ObjectSchema.Cpp;

namespace Yuki.ExpressionSchema.CppBinaryLoader
{
    public static class CodeGenerator
    {
        public static String CompileToCppBinaryLoader(this Schema Schema, String NamespaceName)
        {
            var w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCppBinaryLoader(this Schema Schema)
        {
            return CompileToCppBinaryLoader(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String NamespaceName;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Yuki.ObjectSchema.Properties.Resources.Cpp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppBinaryLoader);
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
                var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
                var ComplexTypes = GetComplexTypes(Schema);

                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("Header", Header).Substitute("Includes", Includes).Substitute("Contents", WrapContents(NamespaceName, ComplexTypes))).Select(Line => Line.TrimEnd(' ')).ToList();
            }

            public List<String> WrapContents(String Namespace, List<String> Contents)
            {
                if (Contents.Count == 0) { return Contents; }
                var c = Contents;
                if (Namespace != "")
                {
                    foreach (var nn in Namespace.Split('.').Reverse())
                    {
                        c = GetTemplate("Namespace").Substitute("NamespaceName", nn).Substitute("Contents", c);
                    }
                }
                return c;
            }

            public Boolean IsInclude(String s)
            {
                if (s.StartsWith("<") && s.EndsWith(">")) { return true; }
                if (s.StartsWith(@"""") && s.EndsWith(@"""")) { return true; }
                return false;
            }

            public List<String> GetHeader()
            {
                return GetTemplate("Header");
            }

            public List<String> GetModuleFunctionContext(FunctionDecl Function)
            {
                return GetTemplate("Module_FunctionContext").Substitute("FunctionName", Function.Name).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public List<String> GetModuleFunctionInitialize(FunctionDecl Function)
            {
                var Parameters = Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionInitialize_Parameter").Substitute("FunctionName", Function.Name).Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToList();
                return GetTemplate("Module_FunctionInitialize").Substitute("FunctionName", Function.Name).Substitute("Parameters", Parameters).Substitute("ReturnType", Function.ReturnValue.ToString());
            }

            public List<String> GetModuleFunctionCall(FunctionDecl Function)
            {
                var ParameterList = String.Join(", ", Function.Parameters.SelectMany(p => GetTemplate("Module_FunctionCall_ParameterList_Parameter").Substitute("Name", p.Name).Substitute("Type", p.Type.ToString())).ToList());
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

                foreach (var m in Schema.Modules)
                {
                    l.AddRange(GetModule(m));
                    l.Add("");
                }

                l.AddRange(GetAssembly(Schema));
                l.Add("");

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
                return Cpp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return Cpp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Cpp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
