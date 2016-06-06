//==========================================================================
//
//  File:        AmbiguousRemover.cs
//  Location:    Nivea <Visual C#>
//  Description: 歧义去除器
//  Version:     2016.06.07.
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
                    else if (s.OnTemplate)
                    {
                        //TODO 注册模板
                    }
                    else if (s.OnGlobal)
                    {
                        //TODO 注册全局变量、全局函数
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

        private static TypeSpec Any = TypeSpec.CreateTypeRef(new TypeRef { Name = "__Any", Version = "" });
        private static TypeSpec Void = TypeSpec.CreateTypeRef(new TypeRef { Name = "__Void", Version = "" });
        private static TypeSpec TypeLiteral = TypeSpec.CreateTypeRef(new TypeRef { Name = "__TypeLiteral", Version = "" });
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

            public LinkedList<Dictionary<String, TypeSpec>> VariableStack = new LinkedList<Dictionary<String, TypeSpec>> { };
            public Dictionary<Expr, TypeSpec> ExprTypes = new Dictionary<Expr, TypeSpec> { };
            public Dictionary<MatchPattern, TypeSpec> PatternTypes = new Dictionary<MatchPattern, TypeSpec> { };

            public T Mark<T>(T NewObj, Object OldObj)
            {
                _Mark(NewObj, OldObj);
                return NewObj;
            }
            public Expr MarkType(Expr NewObj, Expr OldObj, Optional<TypeSpec> t)
            {
                Mark(NewObj, OldObj);
                if (t.OnHasValue)
                {
                    ExprTypes.Add(NewObj, t.Value);
                }
                return NewObj;
            }
            public MatchPattern MarkType(MatchPattern NewObj, MatchPattern OldObj, Optional<TypeSpec> t)
            {
                Mark(NewObj, OldObj);
                if (t.OnHasValue)
                {
                    PatternTypes.Add(NewObj, t.Value);
                }
                return NewObj;
            }
            public Optional<TypeSpec> TryGetType(Expr Obj)
            {
                if (ExprTypes.ContainsKey(Obj))
                {
                    return ExprTypes[Obj];
                }
                return Optional<TypeSpec>.Empty;
            }
            public Optional<TypeSpec> TryGetType(LeftValueDef Obj)
            {
                //TODO
                return Optional<TypeSpec>.Empty;
            }
            public Optional<TypeSpec> TryGetType(VariableRef Obj)
            {
                //TODO
                return Optional<TypeSpec>.Empty;
            }
            public Optional<TypeSpec> TryGetType(MatchPattern Obj)
            {
                if (PatternTypes.ContainsKey(Obj))
                {
                    return PatternTypes[Obj];
                }
                return Optional<TypeSpec>.Empty;
            }
            public Boolean IsCompatibleType(TypeSpec t, TypeSpec Requirement)
            {
                //TODO
                return true;
            }
            public Optional<TypeSpec> GetCommonSuperType(TypeSpec t1, TypeSpec t2)
            {
                if (t1 == Any) { return t2; }
                if (t2 == Any) { return t1; }
                //TODO
                return Void;
            }
            public Optional<TypeSpec> TryResolveCompatibleTypes(IEnumerable<Expr> Objs)
            {
                var t = Optional<TypeSpec>.Empty;
                foreach (var Obj in Objs)
                {
                    if (!ExprTypes.ContainsKey(Obj))
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                    if (t.OnNotHasValue)
                    {
                        t = ExprTypes[Obj];
                    }
                    else
                    {
                        t = GetCommonSuperType(t.Value, ExprTypes[Obj]);
                        if (t.OnNotHasValue)
                        {
                            return Optional<TypeSpec>.Empty;
                        }
                    }
                }
                return t;
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
                    TypeBinding = TypeBinding,
                    VariableStack = new LinkedList<Dictionary<String, TypeSpec>>(VariableStack.Take(VariableStack.Count > 0 ? VariableStack.Count - 1 : 0).Concat(VariableStack.Skip(VariableStack.Count > 0 ? VariableStack.Count - 1 : 0).Select(d => d.ToDictionary(p => p.Key, p => p.Value))))
                };
            }

            public Optional<BindedTypeSpec> BindType(TypeSpec t)
            {
                if (TypeBinding.ContainsKey(t)) { return Optional<BindedTypeSpec>.Empty; }
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
                    return Binded;
                }
                else
                {
                    UnresolvableAmbiguousOrErrors("AmbiguousType", t);
                }
                return Optional<BindedTypeSpec>.Empty;
            }
            public TypeSpec ComposeFuncType(List<TypeSpec> ParameterTypes, TypeSpec ReturnType)
            {
                var FuncGeneric = TypeSpec.CreateMember(new TypeMemberSpec { Parent = TypeSpec.CreateTypeRef(new TypeRef { Name = "System", Version = "" }), Child = TypeSpec.CreateTypeRef(new TypeRef { Name = (ReturnType == Void) ? "Action" : "Func", Version = "" }) });
                var Func = TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = FuncGeneric, ParameterValues = ParameterTypes });
                return Func;
            }
            public Optional<TypeSpec> TryGetFuncTypeReturnType(Optional<TypeSpec> t)
            {
                if (t.OnNotHasValue) { return Optional<TypeSpec>.Empty; }
                //TODO
                return Optional<TypeSpec>.Empty;
            }

            public void PushVariableStack()
            {
                VariableStack.AddLast(new Dictionary<String, TypeSpec> { });
            }
            public void PopVariableStack()
            {
                VariableStack.RemoveLast();
            }

            public void RegisterVariableDef(String Name, TypeSpec Type, Object SemObj)
            {
                var d = VariableStack.Last.Value;
                if (d.ContainsKey(Name))
                {
                    UnresolvableAmbiguousOrErrors("DuplicatedVariableDefinition", SemObj);
                }
                else
                {
                    d.Add(Name, Type);
                    BindType(Type);
                }
            }
            public Optional<BindedTypeSpec> GetVariableType(String Name)
            {
                foreach (var d in VariableStack.Reverse())
                {
                    if (d.ContainsKey(Name))
                    {
                        var t = d[Name];
                        if (TypeBinding.ContainsKey(t))
                        {
                            return TypeBinding[t];
                        }
                    }
                }
                //TODO 查找全局变量
                return Optional<BindedTypeSpec>.Empty;
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
                c.PushVariableStack();
                var t = ReduceTemplateDef(i.Template, c);
                c.PopVariableStack();
                return c.Mark(SectionDef.CreateTemplate(t), i);
            }
            else if (i.OnGlobal)
            {
                var t = ReduceExpr(i.Global, c, SequenceAsTuple: false);
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
                var e = ReduceExpr(i.Expr, c, SequenceAsTuple: false);
                return c.Mark(TemplateSpan.CreateExpr(e), i);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static IndentedExpr ReduceIndentedExpr(IndentedExpr i, Context c)
        {
            var e = ReduceExpr(i.Expr, c, SequenceAsTuple: false);
            return c.Mark(new IndentedExpr { IndentSpace = i.IndentSpace, Expr = e }, i);
        }

        private static Expr ReduceExpr(Expr i, Context c, Optional<TypeSpec> TypeRequirement = default(Optional<TypeSpec>), Boolean SequenceAsTuple = true)
        {
            Expr o;
            if (i.OnError)
            {
                o = c.Mark(Expr.CreateError(), i);
            }
            else if (i.OnAmbiguous)
            {
                if (i.Ambiguous.Count == 0) { throw new InvalidOperationException(); }

                var Candidates = i.Ambiguous.Select(a =>
                {
                    var Errors = new List<KeyValuePair<String, Object>> { };
                    var cCloned = c.GetUnresolvableAmbiguousOrErrorsReplaced((Message, Obj) =>
                    {
                        Errors.Add(new KeyValuePair<String, Object>(Message, Obj));
                    });
                    return new
                    {
                        cCloned = cCloned,
                        Expr = ReduceExpr(a, cCloned, TypeRequirement, SequenceAsTuple),
                        Errors = Errors
                    };
                }).OrderBy(a => a.Errors.Count).ToList();

                var NoErrorCandidates = Candidates.TakeWhile(Candidate => Candidate.Errors.Count == 0).ToList();
                if (NoErrorCandidates.Count == 1)
                {
                    var One = NoErrorCandidates.Single();
                    c.VariableStack = One.cCloned.VariableStack;
                    o = One.Expr;
                }
                else if (NoErrorCandidates.Count > 1)
                {
                    c.UnresolvableAmbiguousOrErrors("UnresolvableAmbiguous", i);
                    o = c.Mark(Expr.CreateError(), i);
                }
                else
                {
                    var First = Candidates.First();
                    c.VariableStack = First.cCloned.VariableStack;
                    foreach (var Error in First.Errors)
                    {
                        c.UnresolvableAmbiguousOrErrors(Error.Key, Error.Value);
                    }
                    o = First.Expr;
                }
            }
            else if (i.OnSequence)
            {
                c.PushVariableStack();
                var l = c.Mark(i.Sequence.Select(e => ReduceExpr(e, c)).ToList(), i.Sequence);
                c.PopVariableStack();

                var Types = l.Select(e => c.TryGetType(e)).ToList();
                var Type = Optional<TypeSpec>.Empty;
                if (SequenceAsTuple)
                {
                    if (Types.All(lt => lt.OnHasValue))
                    {
                        Type = TypeSpec.CreateTuple(Types.Select(lt => lt.Value).ToList());
                    }
                }
                else
                {
                    if (Types.Count > 0)
                    {
                        Type = Types.Last();
                    }
                    else
                    {
                        Type = Void;
                    }
                }

                o = c.MarkType(Expr.CreateSequence(l), i, Type);
            }
            else if (i.OnYield)
            {
                //TODO 检测Yield是否在##中
                var e = ReduceExpr(i.Yield, c);
                o = c.MarkType(Expr.CreateYield(e), i, Void);
            }
            else if (i.OnYieldMany)
            {
                var e = ReduceExpr(i.YieldMany, c);
                o = c.MarkType(Expr.CreateYieldMany(e), i, Void);
            }
            else if (i.OnTemplate)
            {
                var l = c.Mark(i.Template.Select(e => ReduceTemplateExpr(e, c)).ToList(), i.Template);
                o = c.MarkType(Expr.CreateTemplate(l), i, TypeSpec.CreateGenericTypeSpec(new GenericTypeSpec { TypeSpec = TypeSpec.CreateTypeRef(new TypeRef { Name = "List", Version = "" }), ParameterValues = new List<TypeSpec> { TypeSpec.CreateTypeRef(new TypeRef { Name = "String", Version = "" }) } }));
            }
            else if (i.OnYieldTemplate)
            {
                var l = c.Mark(i.YieldTemplate.Select(e => ReduceTemplateExpr(e, c)).ToList(), i.YieldTemplate);
                o = c.MarkType(Expr.CreateYieldTemplate(l), i, Void);
            }
            else if (i.OnThrow)
            {
                var oe = i.Throw.OnHasValue ? ReduceExpr(i.Throw.Value, c) : Optional<Expr>.Empty;
                o = c.MarkType(Expr.CreateThrow(oe), i, Any);
            }
            else if (i.OnLet)
            {
                var LeftTypes = i.Let.Left.Select(d =>
                {
                    var oType = d.OnVariable ? d.Variable.Type : d.Ignore;
                    if (oType.OnHasValue)
                    {
                        return ReduceTypeSpec(oType.Value, c);
                    }
                    else
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                }).ToList();

                var Type = Optional<TypeSpec>.Empty;
                if (LeftTypes.Count == 1)
                {
                    Type = LeftTypes.Single();
                }
                else if (LeftTypes.All(lt => lt.OnHasValue))
                {
                    Type = TypeSpec.CreateTuple(LeftTypes.Select(lt => lt.Value).ToList());
                }

                var Right = ReduceExpr(i.Let.Right, c, Type);
                var Left = c.Mark(i.Let.Left.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Let.Left);
                var e = c.Mark(new LetExpr { Left = Left, Right = Right }, i.Let);
                o = c.MarkType(Expr.CreateLet(e), i, Void);
            }
            else if (i.OnVar)
            {
                var LeftTypes = i.Let.Left.Select(d =>
                {
                    var oType = d.OnVariable ? d.Variable.Type : d.Ignore;
                    if (oType.OnHasValue)
                    {
                        return ReduceTypeSpec(oType.Value, c);
                    }
                    else
                    {
                        return Optional<TypeSpec>.Empty;
                    }
                }).ToList();

                var Type = Optional<TypeSpec>.Empty;
                if (LeftTypes.Count == 1)
                {
                    Type = LeftTypes.Single();
                }
                else if (LeftTypes.All(lt => lt.OnHasValue))
                {
                    Type = TypeSpec.CreateTuple(LeftTypes.Select(lt => lt.Value).ToList());
                }

                var Right = i.Var.Right.OnHasValue ? ReduceExpr(i.Var.Right.Value, c, Type) : Optional<Expr>.Empty;
                var Left = c.Mark(i.Var.Left.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Var.Left);
                var e = c.Mark(new VarExpr { Left = Left, Right = Right }, i.Var);
                o = c.MarkType(Expr.CreateVar(e), i, Void);
            }
            else if (i.OnIf)
            {
                var Branches = c.Mark(i.If.Branches.Select(b => ReduceIfBranch(b, c)).ToList(), i.If);
                var e = c.Mark(new IfExpr { Branches = Branches }, i.If);
                var CommonType = c.TryResolveCompatibleTypes(Branches.Select(b => b.Expr));
                if (CommonType.OnNotHasValue)
                {
                    c.UnresolvableAmbiguousOrErrors("IncompatibleTypeOnBranches", i);
                }
                o = c.MarkType(Expr.CreateIf(e), i, CommonType);
            }
            else if (i.OnMatch)
            {
                //TODO 检查表达式类型兼容，推断表达式类型
                var Target = ReduceExpr(i.Match.Target, c);
                var Alternatives = c.Mark(i.Match.Alternatives.Select(a => ReduceMatchAlternative(a, c)).ToList(), i.Match.Alternatives);
                var e = c.Mark(new MatchExpr { Target = Target, Alternatives = Alternatives }, i.Match);
                var CommonType = c.TryResolveCompatibleTypes(Alternatives.Select(a => a.Expr));
                if (CommonType.OnNotHasValue)
                {
                    c.UnresolvableAmbiguousOrErrors("IncompatibleTypeOnBranches", i);
                }
                o = c.MarkType(Expr.CreateMatch(e), i, CommonType);
            }
            else if (i.OnFor)
            {
                var Enumerable = ReduceExpr(i.For.Enumerable, c);
                c.PushVariableStack();
                var EnumeratedValue = c.Mark(i.For.EnumeratedValue.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.For.EnumeratedValue);
                var Body = ReduceExpr(i.For.Body, c, SequenceAsTuple: false);
                c.PopVariableStack();
                var e = c.Mark(new ForExpr { Enumerable = Enumerable, EnumeratedValue = EnumeratedValue, Body = Body }, i.For);
                o = c.MarkType(Expr.CreateFor(e), i, Void);
            }
            else if (i.OnWhile)
            {
                var Condition = ReduceExpr(i.While.Condition, c);
                c.PushVariableStack();
                var Body = ReduceExpr(i.While.Body, c, SequenceAsTuple: false);
                c.PopVariableStack();
                var e = c.Mark(new WhileExpr { Condition = Condition, Body = Body }, i.While);
                o = c.MarkType(Expr.CreateWhile(e), i, Void);
            }
            else if (i.OnContinue)
            {
                o = c.MarkType(Expr.CreateContinue(i.Continue), i, Void);
            }
            else if (i.OnBreak)
            {
                o = c.MarkType(Expr.CreateBreak(i.Break), i, Void);
            }
            else if (i.OnReturn)
            {
                var oe = i.Return.OnHasValue ? ReduceExpr(i.Return.Value, c) : Optional<Expr>.Empty;
                o = c.MarkType(Expr.CreateReturn(oe), i, Void);
            }
            else if (i.OnAssign)
            {
                var Left = c.Mark(i.Assign.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Assign.Left);
                var Right = ReduceExpr(i.Assign.Right, c);
                var e = c.Mark(new AssignExpr { Left = Left, Right = Right }, i.Assign);
                o = c.MarkType(Expr.CreateAssign(e), i, Void);
            }
            else if (i.OnIncrease)
            {
                var Left = c.Mark(i.Increase.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Increase.Left);
                var Right = ReduceExpr(i.Increase.Right, c);
                var e = c.Mark(new IncreaseExpr { Left = Left, Right = Right }, i.Increase);
                o = c.MarkType(Expr.CreateIncrease(e), i, Void);
            }
            else if (i.OnDecrease)
            {
                var Left = c.Mark(i.Decrease.Left.Select(r => ReduceLeftValueRef(r, c)).ToList(), i.Decrease.Left);
                var Right = ReduceExpr(i.Decrease.Right, c);
                var e = c.Mark(new DecreaseExpr { Left = Left, Right = Right }, i.Decrease);
                o = c.MarkType(Expr.CreateDecrease(e), i, Void);
            }
            else if (i.OnLambda)
            {
                var Parameters = c.Mark(i.Lambda.Parameters.Select(d => ReduceLeftValueDef(d, c)).ToList(), i.Lambda.Parameters);
                var Body = ReduceExpr(i.Lambda.Body, c, SequenceAsTuple: false);
                var e = c.Mark(new LambdaExpr { Parameters = Parameters, Body = Body }, i.Lambda);
                var ParameterTypes = Parameters.Select(p => c.TryGetType(p)).ToList();
                var oBodyType = c.TryGetType(Body);
                var oFuncType = Optional<TypeSpec>.Empty;
                if (ParameterTypes.All(p => p.OnHasValue) && oBodyType.OnHasValue)
                {
                    var BodyType = oBodyType.Value;
                    oFuncType = c.ComposeFuncType(ParameterTypes.Select(p => p.Value).ToList(), BodyType);
                }
                o = c.MarkType(Expr.CreateLambda(e), i, oFuncType);
            }
            else if (i.OnNull)
            {
                o = c.MarkType(Expr.CreateNull(), i, Any);
            }
            else if (i.OnDefault)
            {
                o = c.MarkType(Expr.CreateDefault(), i, Any);
            }
            else if (i.OnPrimitiveLiteral)
            {
                var Type = ReduceTypeSpec(i.PrimitiveLiteral.Type, c);
                var e = c.Mark(new PrimitiveLiteralExpr { Type = Type, Value = i.PrimitiveLiteral.Value }, i.PrimitiveLiteral);
                o = c.MarkType(Expr.CreatePrimitiveLiteral(e), i, Type);
            }
            else if (i.OnRecordLiteral)
            {
                var Type = i.RecordLiteral.Type.OnHasValue ? ReduceTypeSpec(i.RecordLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var FieldAssigns = c.Mark(i.RecordLiteral.FieldAssigns.Select(a => ReduceFieldAssign(a, c)).ToList(), i.RecordLiteral.FieldAssigns);
                var e = c.Mark(new RecordLiteralExpr { Type = Type, FieldAssigns = FieldAssigns }, i.RecordLiteral);
                o = c.MarkType(Expr.CreateRecordLiteral(e), i, Type.OnHasValue ? Type.Value : TypeRequirement);
            }
            else if (i.OnTaggedUnionLiteral)
            {
                var Type = i.TaggedUnionLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TaggedUnionLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Content = i.TaggedUnionLiteral.Expr.OnHasValue ? ReduceExpr(i.TaggedUnionLiteral.Expr.Value, c) : Optional<Expr>.Empty;
                var e = c.Mark(new TaggedUnionLiteralExpr { Type = Type, Alternative = i.TaggedUnionLiteral.Alternative, Expr = Content }, i.TaggedUnionLiteral);
                o = c.MarkType(Expr.CreateTaggedUnionLiteral(e), i, Type.OnHasValue ? Type.Value : TypeRequirement);
            }
            else if (i.OnEnumLiteral)
            {
                var Type = i.EnumLiteral.Type.OnHasValue ? ReduceTypeSpec(i.EnumLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var e = c.Mark(new EnumLiteralExpr { Type = Type, Name = i.EnumLiteral.Name }, i.EnumLiteral);
                o = c.MarkType(Expr.CreateEnumLiteral(e), i, Type.OnHasValue ? Type.Value : TypeRequirement);
            }
            else if (i.OnTupleLiteral)
            {
                var Type = i.TupleLiteral.Type.OnHasValue ? ReduceTypeSpec(i.TupleLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.TupleLiteral.Parameters.Select(p => ReduceExpr(p, c)).ToList(), c);
                var e = c.Mark(new TupleLiteralExpr { Type = Type, Parameters = Parameters }, i.TupleLiteral);
                o = c.MarkType(Expr.CreateTupleLiteral(e), i, Type.OnHasValue ? Type.Value : TypeRequirement);
            }
            else if (i.OnListLiteral)
            {
                var Type = i.ListLiteral.Type.OnHasValue ? ReduceTypeSpec(i.ListLiteral.Type.Value, c) : Optional<TypeSpec>.Empty;
                var Parameters = c.Mark(i.ListLiteral.Parameters.Select(p => ReduceExpr(p, c)).ToList(), c);
                var e = c.Mark(new ListLiteralExpr { Type = Type, Parameters = Parameters }, i.ListLiteral);
                o = c.MarkType(Expr.CreateListLiteral(e), i, Type.OnHasValue ? Type.Value : TypeRequirement);
            }
            else if (i.OnTypeLiteral)
            {
                o = c.MarkType(Expr.CreateTypeLiteral(ReduceTypeSpec(i.TypeLiteral, c)), i, TypeLiteral);
            }
            else if (i.OnVariableRef)
            {
                var r = ReduceVariableRef(i.VariableRef, c);
                o = c.MarkType(Expr.CreateVariableRef(r), i, c.TryGetType(r));
            }
            else if (i.OnFunctionCall)
            {
                var Func = ReduceExpr(i.FunctionCall.Func, c);
                var Parameters = c.Mark(i.FunctionCall.Parameters.Select(p => ReduceExpr(p, c)).ToList(), i.FunctionCall.Parameters);
                var e = c.Mark(new FunctionCallExpr { Func = Func, Parameters = Parameters }, i.FunctionCall);
                var oReturnType = c.TryGetFuncTypeReturnType(c.TryGetType(Func));
                o = c.MarkType(Expr.CreateFunctionCall(e), i, oReturnType);
            }
            else if (i.OnCast)
            {
                var Operand = ReduceExpr(i.Cast.Operand, c);
                var Type = ReduceTypeSpec(i.Cast.Type, c);
                var e = c.Mark(new CastExpr { Operand = Operand, Type = Type }, i.Cast);
                o = c.MarkType(Expr.CreateCast(e), i, Type);
            }
            else
            {
                throw new InvalidOperationException();
            }
            var t = c.TryGetType(o);
            if (t.OnNotHasValue)
            {
                c.UnresolvableAmbiguousOrErrors("TypeNotResolved", i);
            }
            else
            {
                if (TypeRequirement.OnHasValue)
                {
                    if (c.IsCompatibleType(t.Value, TypeRequirement.Value))
                    {
                        //TODO 插入转换代码
                    }
                    else
                    {
                        c.UnresolvableAmbiguousOrErrors("IncompatibleType", i);
                    }
                }
            }
            return o;
        }

        private static VariableRef ReduceVariableRef(VariableRef i, Context c)
        {
            //TODO 解析变量
            if (i.OnName)
            {
                var ot = c.GetVariableType(i.Name);
                if (ot.OnNotHasValue)
                {
                    c.UnresolvableAmbiguousOrErrors("VariableNotExist", i);
                }
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
                if (Type.OnHasValue)
                {
                    c.RegisterVariableDef(i.Variable.Name, Type.Value, i);
                }
                else
                {
                    //TODO
                }
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
            c.PushVariableStack();
            var Expr = ReduceExpr(i.Expr, c, SequenceAsTuple: false);
            c.PopVariableStack();
            return c.Mark(new IfBranch { Condition = Condition, Expr = Expr }, i);
        }

        private static MatchAlternative ReduceMatchAlternative(MatchAlternative i, Context c)
        {
            c.PushVariableStack();
            var Pattern = ReduceMatchPattern(i.Pattern, c);
            var Condition = i.Condition.OnHasValue ? ReduceExpr(i.Condition.Value, c) : Optional<Expr>.Empty;
            var Expr = ReduceExpr(i.Expr, c, SequenceAsTuple: false);
            c.PopVariableStack();
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
                    var cCloned = c.GetUnresolvableAmbiguousOrErrorsReplaced((Message, Obj) =>
                    {
                        Errors.Add(new KeyValuePair<String, Object>(Message, Obj));
                    });
                    return new
                    {
                        cCloned = cCloned,
                        Expr = ReduceMatchPattern(a, cCloned),
                        Errors = Errors
                    };
                }).OrderBy(a => a.Errors.Count).ToList();

                var NoErrorCandidates = Candidates.TakeWhile(Candidate => Candidate.Errors.Count == 0).ToList();
                if (NoErrorCandidates.Count == 1)
                {
                    var One = NoErrorCandidates.Single();
                    c.VariableStack = One.cCloned.VariableStack;
                    return One.Expr;
                }
                else if (NoErrorCandidates.Count > 1)
                {
                    c.UnresolvableAmbiguousOrErrors("UnresolvableAmbiguous", i);
                    return c.Mark(MatchPattern.CreateError(), i);
                }
                else
                {
                    var First = Candidates.First();
                    c.VariableStack = First.cCloned.VariableStack;
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
