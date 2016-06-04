//==========================================================================
//
//  File:        AmbiguousRemover.cs
//  Location:    Nivea <Visual C#>
//  Description: 歧义去除器
//  Version:     2016.06.04.
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
            var Files = InputFiles.ToDictionary(f => f.Key, f => ReduceFileParserResult(f.Value, DefaultNamespace, tp, (Message, Obj) =>
            {
                var Range = f.Value.Positions.ContainsKey(Obj) ? f.Value.Positions[Obj] : TreeFormat.Optional<TextRange>.Empty;
                UnresolvableAmbiguousOrErrors.Add(new KeyValuePair<String, FileTextRange>(Message, new FileTextRange { Text = f.Value.Text, Range = Range }));
            }));
            return new AmbiguousRemoverResult { Files = Files, UnresolvableAmbiguousOrErrors = UnresolvableAmbiguousOrErrors };
        }

        private static Nivea.Template.Syntax.FileParserResult ReduceFileParserResult(Nivea.Template.Syntax.FileParserResult i, List<String> DefaultNamespace, TypeProvider tp, Action<String, Object> UnresolvableAmbiguousOrErrors)
        {
            var Positions = new Dictionary<Object, TextRange>();
            Action<Object, Object> Mark = (NewObj, OldObj) =>
            {
                if (i.Positions.ContainsKey(OldObj) && !Positions.ContainsKey(NewObj))
                {
                    Positions.Add(NewObj, i.Positions[OldObj]);
                }
            };

            var c = new Context { DefaultNamespace = DefaultNamespace, Imports = i.File.Sections.Where(s => s.OnImport).SelectMany(s => s.Import).ToList(), tp = tp, _Mark = Mark, UnresolvableAmbiguousOrErrors = UnresolvableAmbiguousOrErrors };
            return new Nivea.Template.Syntax.FileParserResult { File = ReduceFile(i.File, c), Text = i.Text, Positions = Positions };
        }

        private class Context
        {
            public List<String> DefaultNamespace;
            public List<List<String>> Imports;
            public TypeProvider tp;
            public Action<Object, Object> _Mark;
            public Action<String, Object> UnresolvableAmbiguousOrErrors;

            public List<String> Namespace;
            public LinkedList<HashSet<String>> GenericParametersStack = new LinkedList<HashSet<String>> { };
            public Dictionary<TypeSpec, BindedTypeSpec> TypeBinding = new Dictionary<TypeSpec, BindedTypeSpec> { };

            public T Mark<T>(T NewObj, Object OldObj)
            {
                _Mark(NewObj, OldObj);
                return NewObj;
            }
            public Context GetErrorSuppressed()
            {
                return GetUnresolvableAmbiguousOrErrorsReplaced((Message, Obj) => { });
            }
            public Context GetUnresolvableAmbiguousOrErrorsReplaced(Action<String, Object> UnresolvableAmbiguousOrErrors)
            {
                return new Context
                {
                    DefaultNamespace = DefaultNamespace,
                    Imports = Imports,
                    tp = tp,
                    _Mark = _Mark,
                    UnresolvableAmbiguousOrErrors = UnresolvableAmbiguousOrErrors,
                    Namespace = Namespace,
                    GenericParametersStack = GenericParametersStack,
                    TypeBinding = TypeBinding
                };
            }

            public void BindType(TypeSpec t)
            {
                if (TypeBinding.ContainsKey(t)) { return; }
                var l = TypeBinder.BindType(t, Namespace, Imports, tp);
                if (l.Count == 0)
                {
                    UnresolvableAmbiguousOrErrors("UnresolvedType", t);
                }
                else if (l.Count == 1)
                {
                    var Binded = l.Single();
                    TypeBinding.Add(t, Binded);
                    foreach (var p in Binded.GenericParameters)
                    {
                        BindType(p);
                    }
                }
                else
                {
                    UnresolvableAmbiguousOrErrors("AmbiguousType", t);
                }
            }
        }

        private static File ReduceFile(File i, Context c)
        {
            c.Namespace = c.DefaultNamespace;
            var Sections = c.Mark(i.Sections.Select(s => ReduceSection(s, c)).ToList(), i.Sections);
            return c.Mark(new File { Sections = Sections }, i);
        }

        private static SectionDef ReduceSection(SectionDef i, Context c)
        {
            if (i.OnNamepsace)
            {
                c.Namespace = i.Namepsace;
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
                var t = ReduceTypeDef(i.Type, c);
                return c.Mark(SectionDef.CreateType(t), i);
            }
            else if (i.OnTemplate)
            {
                var t = ReduceTemplateDef(i.Template, c);
                return c.Mark(SectionDef.CreateTemplate(t), i);
            }
            else if (i.OnGlobal)
            {
                var t = ReduceExpr(i.Global, c);
                return c.Mark(SectionDef.CreateGlobal(t), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

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
                var Type = ReduceTypeSpec(i.Alias.Type, c);
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
                var UnderlyingType = ReduceTypeSpec(i.Enum.UnderlyingType, c);
                var Literals = c.Mark(i.Enum.Literals.Select(ltl => ReduceLiteralDef(ltl, c)).ToList(), i.Enum.Literals);
                GenerateLiteralDefSet(Literals, c.UnresolvableAmbiguousOrErrors);
                var Enum = c.Mark(new EnumDef { Name = i.Enum.Name, Version = i.Enum.Version, UnderlyingType = UnderlyingType, Literals = Literals, Description = i.Enum.Description }, i.Enum);
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
            var Type = ReduceTypeSpec(i.Type, c.GetErrorSuppressed());
            return c.Mark(new VariableDef { Name = i.Name, Type = Type, Description = i.Description }, i);
        }

        private static VariableDef ReduceVariableDef(VariableDef i, Context c)
        {
            var Type = ReduceTypeSpec(i.Type, c);
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
                c.BindType(i);
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
                c.BindType(i);
                var Types = c.Mark(i.Tuple.Select(t => CloneTypeSpec(t, c)).ToList(), i.Tuple);
                return c.Mark(TypeSpec.CreateTuple(i.Tuple), i);
            }
            else if (i.OnGenericTypeSpec)
            {
                c.BindType(i);
                var ParameterValues = c.Mark(i.GenericTypeSpec.ParameterValues.Select(t => CloneTypeSpec(t, c)).ToList(), i.GenericTypeSpec);
                var gts = c.Mark(new GenericTypeSpec { TypeSpec = CloneTypeSpec(i.GenericTypeSpec.TypeSpec, c), ParameterValues = ParameterValues }, i.GenericTypeSpec);
                return c.Mark(TypeSpec.CreateGenericTypeSpec(gts), i);
            }
            else if (i.OnMember)
            {
                c.BindType(i);
                var tms = c.Mark(new TypeMemberSpec { Parent = CloneTypeSpec(i.Member.Parent, c), Child = CloneTypeSpec(i.Member.Child, c) }, i.Member);
                return c.Mark(TypeSpec.CreateMember(tms), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TypeSpec CloneTypeSpec(TypeSpec i, Context c)
        {
            if (i.OnTypeRef)
            {
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
                var Types = c.Mark(i.Tuple.Select(t => CloneTypeSpec(t, c)).ToList(), i.Tuple);
                return c.Mark(TypeSpec.CreateTuple(i.Tuple), i);
            }
            else if (i.OnGenericTypeSpec)
            {
                var ParameterValues = c.Mark(i.GenericTypeSpec.ParameterValues.Select(t => CloneTypeSpec(t, c)).ToList(), i.GenericTypeSpec);
                var gts = c.Mark(new GenericTypeSpec { TypeSpec = CloneTypeSpec(i.GenericTypeSpec.TypeSpec, c), ParameterValues = ParameterValues }, i.GenericTypeSpec);
                return c.Mark(TypeSpec.CreateGenericTypeSpec(gts), i);
            }
            else if (i.OnMember)
            {
                var tms = c.Mark(new TypeMemberSpec { Parent = CloneTypeSpec(i.Member.Parent, c), Child = CloneTypeSpec(i.Member.Child, c) }, i.Member);
                return c.Mark(TypeSpec.CreateMember(tms), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TemplateDef ReduceTemplateDef(TemplateDef i, Context c)
        {
            var Signature = ReduceTemplateSignature(i.Signature, c);
            var Body = c.Mark(i.Body.Select(e => ReduceTemplateExpr(e, c)).ToList(), i.Body);
            return c.Mark(new TemplateDef { Signature = Signature, Body = Body }, i);
        }

        private static TemplateSignature ReduceTemplateSignature(TemplateSignature i, Context c)
        {
            var Parameters = c.Mark(i.Parameters.Select(p => ReduceVariableDef(p, c)).ToList(), i.Parameters);
            return c.Mark(new TemplateSignature { Name = i.Name, Parameters = Parameters }, c);
        }

        private static TemplateExpr ReduceTemplateExpr(TemplateExpr i, Context c)
        {
            if (i.OnLine)
            {
                var Line = c.Mark(i.Line.Select(s => ReduceTemplateSpan(s, c)).ToList(), i.Line);
                return c.Mark(TemplateExpr.CreateLine(Line), i);
            }
            else if (i.OnIndentedExpr)
            {
                var e = ReduceIndentedExpr(i.IndentedExpr, c);
                return c.Mark(TemplateExpr.CreateIndentedExpr(e), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static TemplateSpan ReduceTemplateSpan(TemplateSpan i, Context c)
        {
            if (i.OnLiteral)
            {
                return c.Mark(TemplateSpan.CreateLiteral(i.Literal), i);
            }
            else if (i.OnIdentifier)
            {
                var l = c.Mark(i.Identifier.Select(s => ReduceTemplateSpan(s, c)).ToList(), i.Identifier);
                return c.Mark(TemplateSpan.CreateIdentifier(l), i);
            }
            else if (i.OnExpr)
            {
                var e = ReduceExpr(i.Expr, c);
                return c.Mark(TemplateSpan.CreateExpr(e), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static IndentedExpr ReduceIndentedExpr(IndentedExpr i, Context c)
        {
            var e = ReduceExpr(i.Expr, c);
            return c.Mark(new IndentedExpr { IndentSpace = i.IndentSpace, Expr = e }, i);
        }

        private static Expr ReduceExpr(Expr i, Context c)
        {
            if (i.OnError)
            {
                return c.Mark(Expr.CreateError(), i);
            }
            else if (i.OnAmbiguous)
            {
                if (i.Ambiguous.Count == 0) { throw new InvalidOperationException(); }

                var Candidates = i.Ambiguous.Select(a =>
                {
                    var Errors = new List<KeyValuePair<String, Object>> { };
                    return new
                    {
                        Expr = ReduceExpr(a, c.GetUnresolvableAmbiguousOrErrorsReplaced((Message, Obj) =>
                        {
                            Errors.Add(new KeyValuePair<String, Object>(Message, Obj));
                        })),
                        Errors = Errors
                    };
                }).OrderBy(a => a.Errors.Count).ToList();

                var NoErrorCandidates = Candidates.TakeWhile(Candidate => Candidate.Errors.Count == 0).ToList();
                if (NoErrorCandidates.Count == 1)
                {
                    return NoErrorCandidates.Single().Expr;
                }
                else if (NoErrorCandidates.Count > 1)
                {
                    c.UnresolvableAmbiguousOrErrors("UnresolvableAmbiguous", i);
                    return c.Mark(Expr.CreateError(), i);
                }
                else
                {
                    var First = Candidates.First();
                    foreach (var Error in First.Errors)
                    {
                        c.UnresolvableAmbiguousOrErrors(Error.Key, Error.Value);
                    }
                    return First.Expr;
                }
            }
            else if (i.OnSequence)
            {
                var l = c.Mark(i.Sequence.Select(e => ReduceExpr(e, c)).ToList(), i.Sequence);
                return c.Mark(Expr.CreateSequence(l), i);
            }
            else if (i.OnYield)
            {
                var e = ReduceExpr(i.Yield, c);
                return c.Mark(Expr.CreateYield(e), i);
            }
            else if (i.OnYieldMany)
            {
                var e = ReduceExpr(i.YieldMany, c);
                return c.Mark(Expr.CreateYieldMany(e), i);
            }
            else if (i.OnTemplate)
            {
                var l = c.Mark(i.Template.Select(e => ReduceTemplateExpr(e, c)).ToList(), i.Template);
                return c.Mark(Expr.CreateTemplate(l), i);
            }
            else if (i.OnYieldTemplate)
            {
                var l = c.Mark(i.YieldTemplate.Select(e => ReduceTemplateExpr(e, c)).ToList(), i.YieldTemplate);
                return c.Mark(Expr.CreateYieldTemplate(l), i);
            }
            else if (i.OnThrow)
            {
                var oe = i.Throw.OnHasValue ? ReduceExpr(i.Throw.Value, c) : Optional<Expr>.Empty;
                return c.Mark(Expr.CreateThrow(oe), i);
            }
            else if (i.OnLet)
            {
                var Left = c.Mark(i.Let.Left.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Let.Left);
                var Right = ReduceExpr(i.Let.Right, c);
                var e = c.Mark(new LetExpr { Left = Left, Right = Right }, i.Let);
                return c.Mark(Expr.CreateLet(e), i);
            }
            else if (i.OnVar)
            {
                var Left = c.Mark(i.Var.Left.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Var.Left);
                var Right = i.Var.Right.OnHasValue ? ReduceExpr(i.Var.Right.Value, c) : Optional<Expr>.Empty;
                var e = c.Mark(new VarExpr { Left = Left, Right = Right }, i.Var);
                return c.Mark(Expr.CreateVar(e), i);
            }
            else if (i.OnIf)
            {
                var Branches = c.Mark(i.If.Branches.Select(b => ReduceIfBranch(b, c)).ToList(), i.If);
                var e = c.Mark(new IfExpr { Branches = Branches }, i.If);
                return c.Mark(Expr.CreateIf(e), i);
            }
            else if (i.OnMatch)
            {
                var Target = ReduceExpr(i.Match.Target, c);
                var Alternatives = c.Mark(i.Match.Alternatives.Select(a => ReduceMatchAlternative(a, c)).ToList(), i.Match.Alternatives);
                var e = c.Mark(new MatchExpr { Target = Target, Alternatives = Alternatives }, i.Match);
                return c.Mark(Expr.CreateMatch(e), i);
            }
            else if (i.OnFor)
            {
                var Enumerable = ReduceExpr(i.For.Enumerable, c);
                var EnumeratedValue = c.Mark(i.For.EnumeratedValue.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.For.EnumeratedValue);
                var Body = ReduceExpr(i.For.Body, c);
                var e = c.Mark(new ForExpr { Enumerable = Enumerable, EnumeratedValue = EnumeratedValue, Body = Body }, i.For);
                return c.Mark(Expr.CreateFor(e), i);
            }
            else if (i.OnWhile)
            {
                var Condition = ReduceExpr(i.While.Condition, c);
                var Body = ReduceExpr(i.While.Body, c);
                var e = c.Mark(new WhileExpr { Condition = Condition, Body = Body }, i.While);
                return c.Mark(Expr.CreateWhile(e), i);
            }
            else if (i.OnContinue)
            {
                return c.Mark(Expr.CreateContinue(i.Continue), i);
            }
            else if (i.OnBreak)
            {
                return c.Mark(Expr.CreateBreak(i.Break), i);
            }
            else if (i.OnReturn)
            {
                var oe = i.Return.OnHasValue ? ReduceExpr(i.Return.Value, c) : Optional<Expr>.Empty;
                return c.Mark(Expr.CreateReturn(oe), i);
            }
            else if (i.OnAssign)
            {
                var Left = c.Mark(i.Assign.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Assign.Left);
                var Right = ReduceExpr(i.Assign.Right, c);
                var e = c.Mark(new AssignExpr { Left = Left, Right = Right }, i.Assign);
                return c.Mark(Expr.CreateAssign(e), i);
            }
            else if (i.OnIncrease)
            {
                var Left = c.Mark(i.Increase.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Increase.Left);
                var Right = ReduceExpr(i.Increase.Right, c);
                var e = c.Mark(new IncreaseExpr { Left = Left, Right = Right }, i.Increase);
                return c.Mark(Expr.CreateIncrease(e), i);
            }
            else if (i.OnDecrease)
            {
                var Left = c.Mark(i.Decrease.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Decrease.Left);
                var Right = ReduceExpr(i.Decrease.Right, c);
                var e = c.Mark(new DecreaseExpr { Left = Left, Right = Right }, i.Decrease);
                return c.Mark(Expr.CreateDecrease(e), i);
            }
            else if (i.OnLambda)
            {
                var Parameters = c.Mark(i.Lambda.Parameters.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Lambda.Parameters);
                var Body = ReduceExpr(i.Lambda.Body, c);
                var e = c.Mark(new LambdaExpr { Parameters = Parameters, Body = Body }, i.Lambda);
                return c.Mark(Expr.CreateLambda(e), i);
            }
            else if (i.OnNull)
            {
                return c.Mark(Expr.CreateNull(), i);
            }
            else if (i.OnDefault)
            {
                return c.Mark(Expr.CreateDefault(), i);
            }
            else if (i.OnPrimitiveLiteral)
            {
                var Type = ReduceTypeSpec(i.PrimitiveLiteral.Type, c);
                var e = c.Mark(new PrimitiveLiteralExpr { Type = Type, Value = i.PrimitiveLiteral.Value }, i.PrimitiveLiteral);
                return c.Mark(Expr.CreatePrimitiveLiteral(e), i);
            }
            else if (i.OnRecordLiteral)
            {
                var Type = i.RecordLiteral.Type.OnHasValue ? ReduceTypeSpec(i.RecordLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var FieldAssigns = c.Mark(i.RecordLiteral.FieldAssigns.Select(a => ReduceFieldAssign(a, c)).ToList(), i.RecordLiteral.FieldAssigns);
                var e = c.Mark(new RecordLiteralExpr { Type = Type, FieldAssigns = FieldAssigns }, i.RecordLiteral);
                return c.Mark(Expr.CreateRecordLiteral(e), i);
            }
            else if (i.OnTaggedUnionLiteral)
            {
                var Type = i.TaggedUnionLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TaggedUnionLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Content = i.TaggedUnionLiteral.Expr.OnHasValue ? ReduceExpr(i.TaggedUnionLiteral.Expr.Value, c) : Optional<Expr>.Empty;
                var e = c.Mark(new TaggedUnionLiteralExpr { Type = Type, Alternative = i.TaggedUnionLiteral.Alternative, Expr = Content }, i.TaggedUnionLiteral);
                return c.Mark(Expr.CreateTaggedUnionLiteral(e), i);
            }
            else if (i.OnEnumLiteral)
            {
                var Type = i.EnumLiteral.Type.OnHasValue ? ReduceTypeSpec(i.EnumLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var e = c.Mark(new EnumLiteralExpr { Type = Type, Name = i.EnumLiteral.Name }, i.EnumLiteral);
                return c.Mark(Expr.CreateEnumLiteral(e), i);
            }
            else if (i.OnTupleLiteral)
            {
                var Type = i.TupleLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TupleLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.TupleLiteral.Parameters.Select(p => ReduceExpr(p, c)).ToList(), c);
                var e = c.Mark(new TupleLiteralExpr { Type = Type, Parameters = Parameters }, i.TupleLiteral);
                return c.Mark(Expr.CreateTupleLiteral(e), i);
            }
            else if (i.OnListLiteral)
            {
                var Type = i.ListLiteral.Type.OnHasValue ? ReduceTypeSpec(i.ListLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.ListLiteral.Parameters.Select(p => ReduceExpr(p, c)).ToList(), c);
                var e = c.Mark(new ListLiteralExpr { Type = Type, Parameters = Parameters }, i.ListLiteral);
                return c.Mark(Expr.CreateListLiteral(e), i);
            }
            else if (i.OnTypeLiteral)
            {
                return c.Mark(Expr.CreateTypeLiteral(ReduceTypeSpec(i.TypeLiteral, c)), i);
            }
            else if (i.OnVariableRef)
            {
                return c.Mark(Expr.CreateVariableRef(ReduceVariableRef(i.VariableRef, c)), i);
            }
            else if (i.OnFunctionCall)
            {
                var Func = ReduceExpr(i.FunctionCall.Func, c);
                var Parameters = c.Mark(i.FunctionCall.Parameters.Select(p => ReduceExpr(p, c)).ToList(), i.FunctionCall.Parameters);
                var e = c.Mark(new FunctionCallExpr { Func = Func, Parameters = Parameters }, i.FunctionCall);
                return c.Mark(Expr.CreateFunctionCall(e), i.FunctionCall);
            }
            else if (i.OnCast)
            {
                var Operand = ReduceExpr(i.Cast.Operand, c);
                var Type = ReduceTypeSpec(i.Cast.Type, c);
                var e = c.Mark(new CastExpr { Operand = Operand, Type = Type }, i.Cast);
                return c.Mark(Expr.CreateCast(e), i.Cast);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static VariableRef ReduceVariableRef(VariableRef i, Context c)
        {
            if (i.OnName)
            {
                return c.Mark(VariableRef.CreateName(i.Name), i);
            }
            else if (i.OnThis)
            {
                return c.Mark(VariableRef.CreateThis(), i);
            }
            else if (i.OnMemberAccess)
            {
                var Parent = ReduceExpr(i.MemberAccess.Parent, c);
                var Child = ReduceVariableRef(i.MemberAccess.Child, c);
                var a = c.Mark(new MemberAccess { Parent = Parent, Child = Child }, i.MemberAccess);
                return c.Mark(VariableRef.CreateMemberAccess(a), i);
            }
            else if (i.OnIndexerAccess)
            {
                var Expr = ReduceExpr(i.IndexerAccess.Expr, c);
                var Index = c.Mark(i.IndexerAccess.Index.Select(e => ReduceExpr(e, c)).ToList(), i.IndexerAccess.Index);
                var a = c.Mark(new IndexerAccess { Expr = Expr, Index = Index }, i.IndexerAccess);
                return c.Mark(VariableRef.CreateIndexerAccess(a), i);
            }
            else if (i.OnGenericFunctionSpec)
            {
                var Func = ReduceVariableRef(i.GenericFunctionSpec.Func, c);
                var Parameters = c.Mark(i.GenericFunctionSpec.Parameters.Select(p => ReduceTypeSpec(p, c)).ToList(), i.GenericFunctionSpec.Parameters);
                var s = c.Mark(new GenericFunctionSpec { Func = Func, Parameters = Parameters }, i.GenericFunctionSpec);
                return c.Mark(VariableRef.CreateGenericFunctionSpec(s), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static LeftValueDef ReduceLeftValueDef(LeftValueDef i, Context c)
        {
            if (i.OnVariable)
            {
                var Type = i.Variable.Type.OnHasValue ? ReduceTypeSpec(i.Variable.Type.Value, c) : Optional<TypeSpec>.Empty;
                var d = c.Mark(new LocalVariableDef { Name = i.Variable.Name, Type = Type }, i.Variable);
                return c.Mark(LeftValueDef.CreateVariable(d), i);
            }
            else if (i.OnIgnore)
            {
                var Type = i.Ignore.OnHasValue ? ReduceTypeSpec(i.Ignore.Value, c) : Optional<TypeSpec>.Empty;
                return c.Mark(LeftValueDef.CreateIgnore(Type), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static IfBranch ReduceIfBranch(IfBranch i, Context c)
        {
            var Condition = ReduceExpr(i.Condition, c);
            var Expr = ReduceExpr(i.Expr, c);
            return c.Mark(new IfBranch { Condition = Condition, Expr = Expr }, i);
        }

        private static MatchAlternative ReduceMatchAlternative(MatchAlternative i, Context c)
        {
            var Pattern = ReduceMatchPattern(i.Pattern, c);
            var Condition = i.Condition.OnHasValue ? ReduceExpr(i.Condition.Value, c) : Optional<Expr>.Empty;
            var Expr = ReduceExpr(i.Expr, c);
            return c.Mark(new MatchAlternative { Pattern = Pattern, Condition = Condition, Expr = Expr }, i);
        }

        private static LeftValueRef ReduceLeftValueRef(LeftValueRef i, Context c)
        {
            if (i.OnVariable)
            {
                var Variable = ReduceVariableRef(i.Variable, c);
                return c.Mark(LeftValueRef.CreateVariable(Variable), i);
            }
            else if (i.OnIgnore)
            {
                return c.Mark(LeftValueRef.CreateIgnore(), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static FieldAssign ReduceFieldAssign(FieldAssign i, Context c)
        {
            var Expr = ReduceExpr(i.Expr, c);
            return c.Mark(new FieldAssign { Name = i.Name, Expr = Expr }, i);
        }

        private static MatchPattern ReduceMatchPattern(MatchPattern i, Context c)
        {
            if (i.OnError)
            {
                return c.Mark(MatchPattern.CreateError(), i);
            }
            else if (i.OnAmbiguous)
            {
                var Candidates = i.Ambiguous.Select(a =>
                {
                    var Errors = new List<KeyValuePair<String, Object>> { };
                    return new
                    {
                        Expr = ReduceMatchPattern(a, c.GetUnresolvableAmbiguousOrErrorsReplaced((Message, Obj) =>
                        {
                            Errors.Add(new KeyValuePair<String, Object>(Message, Obj));
                        })),
                        Errors = Errors
                    };
                }).OrderBy(a => a.Errors.Count).ToList();

                var NoErrorCandidates = Candidates.TakeWhile(Candidate => Candidate.Errors.Count == 0).ToList();
                if (NoErrorCandidates.Count == 1)
                {
                    return NoErrorCandidates.Single().Expr;
                }
                else if (NoErrorCandidates.Count > 1)
                {
                    c.UnresolvableAmbiguousOrErrors("UnresolvableAmbiguous", i);
                    return c.Mark(MatchPattern.CreateError(), i);
                }
                else
                {
                    var First = Candidates.First();
                    foreach (var Error in First.Errors)
                    {
                        c.UnresolvableAmbiguousOrErrors(Error.Key, Error.Value);
                    }
                    return First.Expr;
                }
            }
            else if (i.OnSequence)
            {
                var l = c.Mark(i.Sequence.Select(e => ReduceMatchPattern(e, c)).ToList(), i.Sequence);
                return c.Mark(MatchPattern.CreateSequence(l), i);
            }
            else if (i.OnLet)
            {
                var d = ReduceLeftValueDef(i.Let, c);
                return c.Mark(MatchPattern.CreateLet(d), i);
            }
            else if (i.OnIgnore)
            {
                return c.Mark(MatchPattern.CreateIgnore(), i);
            }
            else if (i.OnNull)
            {
                return c.Mark(MatchPattern.CreateNull(), i);
            }
            else if (i.OnDefault)
            {
                return c.Mark(MatchPattern.CreateDefault(), i);
            }
            else if (i.OnPrimitiveLiteral)
            {
                var Type = ReduceTypeSpec(i.PrimitiveLiteral.Type, c);
                var e = c.Mark(new PrimitiveLiteralExpr { Type = Type, Value = i.PrimitiveLiteral.Value }, i.PrimitiveLiteral);
                return c.Mark(MatchPattern.CreatePrimitiveLiteral(e), i);
            }
            else if (i.OnRecordLiteral)
            {
                var Type = i.RecordLiteral.Type.OnHasValue ? ReduceTypeSpec(i.RecordLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var FieldAssigns = c.Mark(i.RecordLiteral.FieldAssigns.Select(a => ReduceFieldAssignPattern(a, c)).ToList(), i.RecordLiteral.FieldAssigns);
                var e = c.Mark(new RecordLiteralPattern { Type = Type, FieldAssigns = FieldAssigns }, i.RecordLiteral);
                return c.Mark(MatchPattern.CreateRecordLiteral(e), i);
            }
            else if (i.OnTaggedUnionLiteral)
            {
                var Type = i.TaggedUnionLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TaggedUnionLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Content = i.TaggedUnionLiteral.Expr.OnHasValue ? ReduceMatchPattern(i.TaggedUnionLiteral.Expr.Value, c) : Optional<MatchPattern>.Empty;
                var e = c.Mark(new TaggedUnionLiteralPattern { Type = Type, Alternative = i.TaggedUnionLiteral.Alternative, Expr = Content }, i.TaggedUnionLiteral);
                return c.Mark(MatchPattern.CreateTaggedUnionLiteral(e), i);
            }
            else if (i.OnEnumLiteral)
            {
                var Type = i.EnumLiteral.Type.OnHasValue ? ReduceTypeSpec(i.EnumLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var e = c.Mark(new EnumLiteralExpr { Type = Type, Name = i.EnumLiteral.Name }, i.EnumLiteral);
                return c.Mark(MatchPattern.CreateEnumLiteral(e), i);

            }
            else if (i.OnTupleLiteral)
            {
                var Type = i.TupleLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TupleLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.TupleLiteral.Parameters.Select(p => ReduceMatchPattern(p, c)).ToList(), c);
                var e = c.Mark(new TupleLiteralPattern { Type = Type, Parameters = Parameters }, i.TupleLiteral);
                return c.Mark(MatchPattern.CreateTupleLiteral(e), i);
            }
            else if (i.OnListLiteral)
            {
                var Type = i.ListLiteral.Type.OnHasValue ? ReduceTypeSpec(i.ListLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.ListLiteral.Parameters.Select(p => ReduceMatchPattern(p, c)).ToList(), c);
                var e = c.Mark(new ListLiteralPattern { Type = Type, Parameters = Parameters }, i.ListLiteral);
                return c.Mark(MatchPattern.CreateListLiteral(e), i);
            }
            else if (i.OnVariableRef)
            {
                return c.Mark(MatchPattern.CreateVariableRef(ReduceVariableRef(i.VariableRef, c)), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static FieldAssignPattern ReduceFieldAssignPattern(FieldAssignPattern i, Context c)
        {
            var Expr = ReduceMatchPattern(i.Expr, c);
            return c.Mark(new FieldAssignPattern { Name = i.Name, Expr = Expr }, i);
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
