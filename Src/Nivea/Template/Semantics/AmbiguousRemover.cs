//==========================================================================
//
//  File:        AmbiguousRemover.cs
//  Location:    Nivea <Visual C#>
//  Description: 歧义去除器
//  Version:     2016.06.03.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Nivea.Template.Semantics
{
    public class AmbiguousRemoverResult
    {
        public Dictionary<String, Nivea.Template.Syntax.FileParserResult> Files;
        public List<KeyValuePair<String, FileTextRange>> UnresolvableAmbiguousOrErrors;
    }

    public static class AmbiguousRemover
    {
        public static AmbiguousRemoverResult Reduce(Dictionary<String, Nivea.Template.Syntax.FileParserResult> InputFiles, List<String> DefaultNamespace, TypeProvider tp)
        {
            tp.LoadAssembly("mscorlib");
            foreach (var p in InputFiles)
            {
                var Namespace = DefaultNamespace;
                foreach (var s in p.Value.File.Sections)
                {
                    if (s.OnNamepsace)
                    {
                        Namespace = s.Namepsace;
                    }
                    else if (s.OnAssembly)
                    {
                        foreach (var AssemblyName in s.Assembly)
                        {
                            try
                            {
                                tp.LoadAssembly(AssemblyName);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidEvaluationException("LoadAssemblyFailure: " + AssemblyName, GetFileTextRange(s, p.Value), s, ex);
                            }
                        }
                    }
                    else if (s.OnType)
                    {
                        tp.AddTypeDef(Namespace, s.Type);
                    }
                }
            }

            //TODO 检测重复的类型、模板、全局变量

            var UnresolvableAmbiguousOrErrors = new List<KeyValuePair<String, FileTextRange>>();
            var Files = InputFiles.ToDictionary(f => f.Key, f => ReduceFileParserResult(f.Value, tp, (Message, Obj) =>
            {
                var Range = f.Value.Positions.ContainsKey(Obj) ? f.Value.Positions[Obj] : TreeFormat.Optional<TextRange>.Empty;
                UnresolvableAmbiguousOrErrors.Add(new KeyValuePair<String, FileTextRange>(Message, new FileTextRange { Text = f.Value.Text, Range = Range }));
            }));
            return new AmbiguousRemoverResult { Files = Files, UnresolvableAmbiguousOrErrors = UnresolvableAmbiguousOrErrors };
        }

        private static Nivea.Template.Syntax.FileParserResult ReduceFileParserResult(Nivea.Template.Syntax.FileParserResult i, TypeProvider tp, Action<String, Object> UnresolvableAmbiguousOrErrors)
        {
            var Positions = new Dictionary<Object, TextRange>();
            Action<Object, Object> Mark = (NewObj, OldObj) =>
            {
                if (i.Positions.ContainsKey(OldObj) && !Positions.ContainsKey(NewObj))
                {
                    Positions.Add(NewObj, i.Positions[OldObj]);
                }
            };

            var c = new Context { Imports = i.File.Sections.Where(s => s.OnImport).SelectMany(s => s.Import).ToList(), tp = tp, _Mark = Mark, UnresolvableAmbiguousOrErrors = UnresolvableAmbiguousOrErrors };
            return new Nivea.Template.Syntax.FileParserResult { File = ReduceFile(i.File, c), Text = i.Text, Positions = Positions };
        }

        private class Context
        {
            public List<List<String>> Imports;
            public TypeProvider tp;
            public Action<Object, Object> _Mark;
            public Action<String, Object> UnresolvableAmbiguousOrErrors;

            public LinkedList<HashSet<String>> GenericParametersStack = new LinkedList<HashSet<String>> { };

            public T Mark<T>(T NewObj, Object OldObj)
            {
                _Mark(NewObj, OldObj);
                return NewObj;
            }
            public Context GetErrorSuppressed()
            {
                return new Context { tp = tp, _Mark = _Mark, UnresolvableAmbiguousOrErrors = (Message, Obj) => { }, GenericParametersStack = GenericParametersStack };
            }
        }

        private static File ReduceFile(File i, Context c)
        {
            var Sections = c.Mark(i.Sections.Select(s => ReduceSection(s, c)).ToList(), i.Sections);
            return c.Mark(new File { Sections = Sections }, i);
        }

        private static SectionDef ReduceSection(SectionDef i, Context c)
        {
            if (i.OnNamepsace)
            {
                return c.Mark(SectionDef.CreateNamepsace(c.Mark(i.Namepsace, i.Namepsace)), i);
            }
            else if (i.OnAssembly)
            {
                return c.Mark(SectionDef.CreateAssembly(c.Mark(i.Assembly, i.Assembly)), i);
            }
            else if (i.OnImport)
            {
                return c.Mark(SectionDef.CreateImport(c.Mark(i.Import, i.Import)), i);
            }
            else if (i.OnType)
            {
                var t = c.Mark(ReduceTypeDef(i.Type, c), i.Type);
                return c.Mark(SectionDef.CreateType(t), i);
            }
            else if (i.OnTemplate)
            {
                var t = c.Mark(ReduceTemplateDef(i.Template, c), i.Type);
                return c.Mark(SectionDef.CreateTemplate(t), i);
            }
            else if (i.OnGlobal)
            {
                var t = c.Mark(ReduceExpr(i.Global, c), i.Type);
                return c.Mark(SectionDef.CreateGlobal(t), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        //TODO 检查类型是否合法、字段是否重复等
        private static TypeDef ReduceTypeDef(TypeDef i, Context c)
        {
            if (i.OnPrimitive)
            {
                var GenericParameters = c.Mark(i.Primitive.GenericParameters.Select(v => ReduceGenericParameter(v, c)).ToList(), i.Primitive.GenericParameters);
                var Primitive = c.Mark(new PrimitiveDef { Name = i.Primitive.Name, GenericParameters = GenericParameters, Description = i.Primitive.Description }, i.Primitive);
                return c.Mark(TypeDef.CreatePrimitive(Primitive), i);
            }
            else if (i.OnAlias)
            {
                var GenericParameters = c.Mark(i.Alias.GenericParameters.Select(v => ReduceGenericParameter(v, c)).ToList(), i.Alias.GenericParameters);
                var GenericParametersSet = GenerateVariableDefSet(GenericParameters, c.UnresolvableAmbiguousOrErrors);
                c.GenericParametersStack.AddLast(GenericParametersSet);
                var Type = c.Mark(ReduceTypeSpec(i.Alias.Type, c), i.Alias.Type);
                c.GenericParametersStack.RemoveLast();
                var Alias = c.Mark(new AliasDef { Name = i.Alias.Name, Version = i.Alias.Version, GenericParameters = GenericParameters, Type = Type, Description = i.Alias.Description }, i.Alias);
                return c.Mark(TypeDef.CreateAlias(Alias), i);
            }
            else if (i.OnRecord)
            {
                var GenericParameters = c.Mark(i.Record.GenericParameters.Select(v => ReduceGenericParameter(v, c)).ToList(), i.Record.GenericParameters);
                var GenericParametersSet = GenerateVariableDefSet(GenericParameters, c.UnresolvableAmbiguousOrErrors);
                c.GenericParametersStack.AddLast(GenericParametersSet);
                var Fields = c.Mark(i.Record.Fields.Select(v => ReduceVariableDef(v, c)).ToList(), i.Record.Fields);
                GenerateVariableDefSet(Fields, c.UnresolvableAmbiguousOrErrors);
                c.GenericParametersStack.RemoveLast();
                var Record = c.Mark(new RecordDef { Name = i.Record.Name, Version = i.Record.Version, GenericParameters = GenericParameters, Fields = Fields, Description = i.Record.Description }, i.Record);
                return c.Mark(TypeDef.CreateRecord(Record), i);
            }
            else if (i.OnTaggedUnion)
            {
                var GenericParameters = c.Mark(i.TaggedUnion.GenericParameters.Select(v => ReduceGenericParameter(v, c)).ToList(), i.TaggedUnion.GenericParameters);
                var GenericParametersSet = GenerateVariableDefSet(GenericParameters, c.UnresolvableAmbiguousOrErrors);
                c.GenericParametersStack.AddLast(GenericParametersSet);
                var Alternatives = c.Mark(i.TaggedUnion.Alternatives.Select(v => ReduceVariableDef(v, c)).ToList(), i.TaggedUnion.Alternatives);
                GenerateVariableDefSet(Alternatives, c.UnresolvableAmbiguousOrErrors);
                c.GenericParametersStack.RemoveLast();
                var TaggedUnion = c.Mark(new TaggedUnionDef { Name = i.TaggedUnion.Name, Version = i.TaggedUnion.Version, GenericParameters = GenericParameters, Alternatives = Alternatives, Description = i.TaggedUnion.Description }, i.TaggedUnion);
                return c.Mark(TypeDef.CreateTaggedUnion(TaggedUnion), i);
            }
            else if (i.OnEnum)
            {
                var UnderlyingType = c.Mark(ReduceTypeSpec(i.Enum.UnderlyingType, c), i.Enum.UnderlyingType);
                var Literals = c.Mark(i.Enum.Literals.Select(ltl => ReduceLiteralDef(ltl, c)).ToList(), i.Enum.Literals);
                GenerateLiteralDefSet(Literals, c.UnresolvableAmbiguousOrErrors);
                var Enum = c.Mark(new EnumDef { Name = i.Enum.Name, Version = i.Enum.Version, UnderlyingType = UnderlyingType, Literals = Literals, Description = i.TaggedUnion.Description }, i.Enum);
                return c.Mark(TypeDef.CreateEnum(Enum), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static HashSet<String> GenerateVariableDefSet(List<VariableDef> Variables, Action<String, Object> UnresolvableAmbiguousOrErrors)
        {
            var d = new HashSet<String>();
            foreach (var v in Variables)
            {
                if (d.Contains(v.Name))
                {
                    UnresolvableAmbiguousOrErrors("DuplicatedVariable", v);
                }
                else
                {
                    d.Add(v.Name);
                }
            }
            return d;
        }

        private static HashSet<String> GenerateLiteralDefSet(List<LiteralDef> Literals, Action<String, Object> UnresolvableAmbiguousOrErrors)
        {
            var d = new HashSet<String>();
            foreach (var ltl in Literals)
            {
                if (d.Contains(ltl.Name))
                {
                    UnresolvableAmbiguousOrErrors("DuplicatedLiteral", ltl);
                }
                else
                {
                    d.Add(ltl.Name);
                }
            }
            return d;
        }

        private static VariableDef ReduceGenericParameter(VariableDef i, Context c)
        {
            if (!(i.Type.OnTypeRef && (i.Type.TypeRef.Name == "Type") && (i.Type.TypeRef.Version == "")))
            {
                c.UnresolvableAmbiguousOrErrors("GenericParameterTypeMustBeType", i);
            }
            var Type = c.Mark(ReduceTypeSpec(i.Type, c.GetErrorSuppressed()), i.Type);
            return c.Mark(new VariableDef { Name = i.Name, Type = Type, Description = i.Description }, i);
        }

        private static VariableDef ReduceVariableDef(VariableDef i, Context c)
        {
            var Type = c.Mark(ReduceTypeSpec(i.Type, c), i.Type);
            return c.Mark(new VariableDef { Name = i.Name, Type = Type, Description = i.Description }, i);
        }

        private static LiteralDef ReduceLiteralDef(LiteralDef i, Context c)
        {
            return c.Mark(new LiteralDef { Name = i.Name, Value = i.Value, Description = i.Description }, i);
        }

        private static TypeSpec ReduceTypeSpec(TypeSpec i, Context c)
        {
            if (i.OnTypeRef)
            {
                //TODO
                var tl = c.tp.GetTypeDefs(new List<String> { }, i.TypeRef, 0);
                if (tl.Count == 0)
                {
                    c.UnresolvableAmbiguousOrErrors("TypeNotExist", i);
                }
                return c.Mark(TypeSpec.CreateTypeRef(c.Mark(new TypeRef { Name = i.TypeRef.Name, Version = i.TypeRef.Version }, i.TypeRef)), i);
            }
            else if (i.OnGenericParameterRef)
            {
                if (!c.GenericParametersStack.Reverse().Any(h => h.Contains(i.GenericParameterRef)))
                {
                    c.UnresolvableAmbiguousOrErrors("GenericParameterNotExist", i);
                }
                return c.Mark(TypeSpec.CreateGenericParameterRef(i.GenericParameterRef), i);
            }
            else if (i.OnTuple)
            {
                var Types = c.Mark(i.Tuple.Select(t => ReduceTypeSpec(t, c)).ToList(), i.Tuple);
                return c.Mark(TypeSpec.CreateTuple(i.Tuple), i);
            }
            else if (i.OnGenericTypeSpec)
            {
                var ParameterValues = c.Mark(i.GenericTypeSpec.ParameterValues.Select(t => ReduceTypeSpec(t, c)).ToList(), i.Tuple);
                //TODO
                var gts = c.Mark(new GenericTypeSpec { TypeSpec = null, ParameterValues = ParameterValues }, i.GenericTypeSpec);
                return c.Mark(TypeSpec.CreateGenericTypeSpec(gts), i);
            }
            else if (i.OnMember)
            {

            }
            else
            {
                throw new InvalidOperationException();
            }
            //TODO
            throw new NotImplementedException();
        }

        private static TemplateDef ReduceTemplateDef(TemplateDef i, Context c)
        {
            //TODO
            throw new NotImplementedException();
        }

        private static Expr ReduceExpr(Expr i, Context c)
        {
            //TODO
            throw new NotImplementedException();
        }

        private static FileTextRange GetFileTextRange(Object SemObj, Nivea.Template.Syntax.FileParserResult FileParserResult)
        {
            var Range = TreeFormat.Optional<TextRange>.Empty;
            if (FileParserResult.Positions.ContainsKey(SemObj))
            {
                Range = FileParserResult.Positions[SemObj];
            }
            return new FileTextRange { Text = FileParserResult.Text, Range = Range };
        }
    }
}
