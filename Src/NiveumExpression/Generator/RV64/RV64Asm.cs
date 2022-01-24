//==========================================================================
//
//  File:        RV64Asm.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 表达式结构RV64汇编生成器
//  Version:     2022.01.25.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using Niveum.Expression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using OS = Niveum.ObjectSchema;

namespace Niveum.ExpressionSchema.RV64Asm
{
    public static class CodeGenerator
    {
        public static String CompileToRV64CHeader(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.GenerateHeader(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\n", Lines);
        }
        public static String CompileToRV64Assembly(this Schema Schema, String NamespaceName, Niveum.ExpressionSchema.Assembly a)
        {
            var t = new Templates(Schema);
            var Lines = t.GenerateAssembly(Schema, NamespaceName, a).Select(Line => Line.TrimEnd(' '));
            return String.Join("\n", Lines);
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
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Unit" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } }),
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Boolean" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } })
                    },
                Imports = new List<String> { }
            });
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public Boolean IsInclude(String s)
        {
            return Inner.IsInclude(s);
        }

        private IEnumerable<String> BuildBody(String NamespaceName, String ModuleName, FunctionDef f)
        {
            var FunctionLabel = GetEscapedIdentifier($"{NamespaceName}_{ModuleName}_{f.Name}");
            var c = new FunctionBuilderContext(FunctionLabel, f);
            int StackSize = c.StackSizeForSavedRegisters + c.StackSizeForLocal + c.StackSizeForChildArguments;
            yield return $"{FunctionLabel}:";
            yield return "#prolog";
            yield return $"addi\tsp, sp, -{StackSize}";
            yield return $"sd\tra, {StackSize - 8}(sp)";
            yield return $"sd\tfp, {StackSize - 16}(sp)";
            var IntegerCalleeSavedRegisters = c.IntegerCalleeSavedRegisters.OrderBy(r => r).Select((r, k) => new { Reg = r, Offset = StackSize - 16 - k * 8 }).ToList();
            var FloatCalleeSavedRegisters = c.FloatCalleeSavedRegisters.OrderBy(r => r).Select((r, k) => new { Reg = r, Offset = StackSize - 16 - (IntegerCalleeSavedRegisters.Count + k) * 8 }).ToList();
            foreach (var s in IntegerCalleeSavedRegisters)
            {
                yield return $"sd\t{s.Reg}, {s.Offset}(sp)";
            }
            foreach (var s in FloatCalleeSavedRegisters)
            {
                yield return $"fsd\t{s.Reg}, {s.Offset}(sp)";
            }
            yield return $"addi\tfp, sp, {StackSize}";
            yield return "";
            yield return "#body";
            foreach (var LineWrite in c.OutputLineWrites)
            {
                yield return LineWrite();
            }
            yield return "";
            yield return "#epilog";
            foreach (var s in FloatCalleeSavedRegisters)
            {
                yield return $"ld\t{s.Reg}, {s.Offset}(sp)";
            }
            foreach (var s in IntegerCalleeSavedRegisters)
            {
                yield return $"ld\t{s.Reg}, {s.Offset}(sp)";
            }
            yield return $"ld\tfp, {StackSize - 16}(sp)";
            yield return $"ld\tra, {StackSize - 8}(sp)";
            yield return $"addi\tsp, sp, {StackSize}";
            yield return "ret";
            yield return "";
        }

        private IEnumerable<String> Indentize(IEnumerable<String> Lines)
        {
            foreach (var Line in Lines)
            {
                if (Line.EndsWith(":"))
                {
                    yield return Line;
                    continue;
                }
                var Parts = Line.Split(new Char[] { '\t' }, 2);
                if (Parts.Length == 2)
                {
                    yield return "    " + Parts[0].PadRight(10) + "  " + Parts[1];
                }
                else
                {
                    yield return "    " + Line;
                }
            }
        }

        private class FunctionBuilder
        {
            public String Name { get; init; }
            public List<PrimitiveType> ParameterTypes { get; init; }
            public PrimitiveType ReturnType { get; init; }
            public Func<List<VariableContext>, VariableContext> Build { get; init; }
        }

        private class VariableContext
        {
            public PrimitiveType Type;
            public int UseCount;
            public String RegisterName;
            public Optional<int> Offset;
        }

        private class FunctionBuilderContext
        {
            private String FunctionLabel;
            private FunctionDef f;
            private List<VariableDef> Parameters;
            private Dictionary<String, int> ParameterToIndex;
            private Dictionary<String, VariableContext> ParameterToVariableContext;
            private Dictionary<String, VariableContext> RegisterToVariableContext;
            private Dictionary<String, List<FunctionBuilder>> FunctionBuilders;

            private LinkedList<String> FreeIntegerTemporaryRegisters = new LinkedList<String>(Enumerable.Range(0, 6 + 1).Select(v => $"t{v}"));
            private Dictionary<String, LinkedListNode<String>> FreeIntegerTemporaryRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> StandingIntegerTemporaryRegisters = new LinkedList<String>();
            private Dictionary<String, LinkedListNode<String>> StandingIntegerTemporaryRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> FreeIntegerCalleeSavedRegisters = new LinkedList<String>(Enumerable.Range(1, 11 + 1 - 1).Select(v => $"s{v}"));
            private Dictionary<String, LinkedListNode<String>> FreeIntegerCalleeSavedRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> StandingIntegerCalleeSavedRegisters = new LinkedList<String>();
            private Dictionary<String, LinkedListNode<String>> StandingIntegerCalleeSavedRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> FreeFloatTemporaryRegisters = new LinkedList<String>(Enumerable.Range(0, 11 + 1).Select(v => $"ft{v}"));
            private Dictionary<String, LinkedListNode<String>> FreeFloatTemporaryRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> StandingFloatTemporaryRegisters = new LinkedList<String>();
            private Dictionary<String, LinkedListNode<String>> StandingFloatTemporaryRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> FreeFloatCalleeSavedRegisters = new LinkedList<String>(Enumerable.Range(0, 11 + 1).Select(v => $"fs{v}"));
            private Dictionary<String, LinkedListNode<String>> FreeFloatCalleeSavedRegisterDict = new Dictionary<String, LinkedListNode<String>>();
            private LinkedList<String> StandingFloatCalleeSavedRegisters = new LinkedList<String>();
            private Dictionary<String, LinkedListNode<String>> StandingFloatCalleeSavedRegisterDict = new Dictionary<String, LinkedListNode<String>>();

            public int StackSizeForParameters = 0;
            public int StackSizeForSavedRegisters = 0; // including ReturnAddress FramePointer
            public List<String> IntegerCalleeSavedRegisters = new List<String>();
            public HashSet<String> IntegerCalleeSavedRegisterSet = new HashSet<String>();
            public List<String> FloatCalleeSavedRegisters = new List<String>();
            public HashSet<String> FloatCalleeSavedRegisterSet = new HashSet<String>();
            public int StackSizeForLocal = 0;
            private int StackOffsetForLocal = 0;
            public int StackSizeForChildArguments = 0;

            private int LabelNumber = 0;

            public List<Func<String>> OutputLineWrites = new List<Func<String>>();

            public FunctionBuilderContext(String FunctionLabel, FunctionDef f)
            {
                this.FunctionLabel = FunctionLabel;
                this.f = f;
                Parameters = f.Parameters;
                ParameterToIndex = Parameters.Select((d, i) => (d, i)).ToDictionary(p => p.d.Name, p => p.i);
                var ParameterToUsageCount = new Dictionary<String, int>();
                ForEachExpr(f.Body, e =>
                {
                    if (e.OnVariable)
                    {
                        var Name = e.Variable.Name;
                        if (ParameterToUsageCount.ContainsKey(Name))
                        {
                            ParameterToUsageCount[Name] += 1;
                        }
                        else
                        {
                            ParameterToUsageCount.Add(Name, 1);
                        }
                    }
                });

                var FreeIntegerArgumentRegisters = new LinkedList<String>(Enumerable.Range(0, 7 + 1).Select(v => $"a{v}"));
                var FreeFloatArgumentRegisters = new LinkedList<String>(Enumerable.Range(0, 7 + 1).Select(v => $"fa{v}"));
                ParameterToVariableContext = new Dictionary<String, VariableContext>();
                RegisterToVariableContext = new Dictionary<String, VariableContext>();
                foreach (var p in Parameters)
                {
                    if (p.Type == PrimitiveType.Real)
                    {
                        if (FreeFloatArgumentRegisters.Count > 0)
                        {
                            var r = FreeFloatArgumentRegisters.First.Value;
                            var vc = new VariableContext { Type = p.Type, UseCount = ParameterToUsageCount.ContainsKey(p.Name) ? ParameterToUsageCount[p.Name] : 0, RegisterName = r, Offset = Optional<int>.Empty };
                            ParameterToVariableContext.Add(p.Name, vc);
                            RegisterToVariableContext.Add(r, vc);
                            FreeFloatArgumentRegisters.RemoveFirst();
                            if (ParameterToUsageCount.ContainsKey(p.Name))
                            {
                                StandingFloatTemporaryRegisters.AddLast(r);
                                StandingFloatTemporaryRegisterDict.Add(r, StandingFloatTemporaryRegisters.Last);
                            }
                            else
                            {
                                FreeFloatTemporaryRegisters.AddLast(r);
                            }
                        }
                        else
                        {
                            ParameterToVariableContext.Add(p.Name, new VariableContext { Type = p.Type, UseCount = ParameterToUsageCount.ContainsKey(p.Name) ? ParameterToUsageCount[p.Name] : 0, RegisterName = "fp", Offset = StackSizeForParameters });
                            StackSizeForParameters += 8;
                        }
                    }
                    else
                    {
                        if (FreeIntegerArgumentRegisters.Count > 0)
                        {
                            var r = FreeIntegerArgumentRegisters.First.Value;
                            var vc = new VariableContext { Type = p.Type, UseCount = ParameterToUsageCount.ContainsKey(p.Name) ? ParameterToUsageCount[p.Name] : 0, RegisterName = r, Offset = Optional<int>.Empty };
                            ParameterToVariableContext.Add(p.Name, vc);
                            RegisterToVariableContext.Add(r, vc);
                            FreeIntegerArgumentRegisters.RemoveFirst();
                            if (ParameterToUsageCount.ContainsKey(p.Name))
                            {
                                StandingIntegerTemporaryRegisters.AddLast(r);
                                StandingIntegerTemporaryRegisterDict.Add(r, StandingIntegerTemporaryRegisters.Last);
                            }
                            else
                            {
                                FreeIntegerTemporaryRegisters.AddLast(r);
                            }
                        }
                        else
                        {
                            ParameterToVariableContext.Add(p.Name, new VariableContext { Type = p.Type, UseCount = ParameterToUsageCount.ContainsKey(p.Name) ? ParameterToUsageCount[p.Name] : 0, RegisterName = "fp", Offset = StackSizeForParameters });
                            StackSizeForParameters += 8;
                        }
                    }
                }
                foreach (var r in FreeIntegerArgumentRegisters)
                {
                    FreeIntegerTemporaryRegisters.AddLast(r);
                }
                foreach (var r in FreeFloatArgumentRegisters)
                {
                    FreeFloatTemporaryRegisters.AddLast(r);
                }

                if (FreeIntegerTemporaryRegisters.Count > 0)
                {
                    var n = FreeIntegerTemporaryRegisters.First;
                    while (true)
                    {
                        FreeIntegerTemporaryRegisterDict.Add(n.Value, n);
                        if (n.Next == null) { break; }
                        n = n.Next;
                    }
                }
                if (FreeIntegerCalleeSavedRegisters.Count > 0)
                {
                    var n = FreeIntegerCalleeSavedRegisters.First;
                    while (true)
                    {
                        FreeIntegerCalleeSavedRegisterDict.Add(n.Value, n);
                        if (n.Next == null) { break; }
                        n = n.Next;
                    }
                }
                if (FreeFloatTemporaryRegisters.Count > 0)
                {
                    var n = FreeFloatTemporaryRegisters.First;
                    while (true)
                    {
                        FreeFloatTemporaryRegisterDict.Add(n.Value, n);
                        if (n.Next == null) { break; }
                        n = n.Next;
                    }
                }
                if (FreeFloatCalleeSavedRegisters.Count > 0)
                {
                    var n = FreeFloatCalleeSavedRegisters.First;
                    while (true)
                    {
                        FreeFloatCalleeSavedRegisterDict.Add(n.Value, n);
                        if (n.Next == null) { break; }
                        n = n.Next;
                    }
                }

                FunctionBuilders = (new List<FunctionBuilder>
                {
                    CreateOperator("+", PrimitiveType.Int, PrimitiveType.Int, true, (RegisterArguments, Ret) => { }),
                    CreateOperator("-", PrimitiveType.Int, PrimitiveType.Int, false, (RegisterArguments, Ret) => $"subw\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[0]}"),
                    CreateOperator("+", PrimitiveType.Real, PrimitiveType.Real, true, (RegisterArguments, Ret) => { }),
                    CreateOperator("-", PrimitiveType.Real, PrimitiveType.Real, false, (RegisterArguments, Ret) => $"fsgnjn.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[0]}"),
                    CreateOperator("+", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (RegisterArguments, Ret) => $"addw\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator("-", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (RegisterArguments, Ret) => $"subw\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[1]}"),
                    CreateOperator("*", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (RegisterArguments, Ret) => $"mulw\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[1]}"),
                    CreateOperator("/", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Real, (RegisterArguments, Ret) =>
                    {
                        var t0 = AcquireTemporaryRegister(PrimitiveType.Real);
                        Emit($"fcvt.d.w\t{Ret}, {RegisterArguments[0]}");
                        Emit($"fcvt.d.w\t{t0}, {RegisterArguments[1]}");
                        Emit($"fdiv.d\t{Ret}, {Ret}, {t0}");
                        ReleaseRegister(PrimitiveType.Real, t0);
                    }),
                    CreateOperator("+", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (RegisterArguments, Ret) => $"fadd.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator("-", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (RegisterArguments, Ret) => $"fsub.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator("*", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (RegisterArguments, Ret) => $"fmul.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator("/", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (RegisterArguments, Ret) => $"fdiv.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateRuntimeFunction("pow", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("pow", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("exp", PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("log", PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("mod", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("div", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateOperator("!", PrimitiveType.Boolean, PrimitiveType.Boolean, false, (RegisterArguments, Ret) => $"xori\t{RegisterArguments[0]}, {RegisterArguments[0]}, -1"),
                    CreateOperator("<", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"slt\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator(">", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"slt\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[0]}"),
                    CreateOperator("<=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) =>
                    {
                        // (a <= b) == !(b < a)
                        Emit($"slt\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[0]}");
                        Emit($"xori\t{Ret}, {Ret}, -1");
                    }),
                    CreateOperator(">=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) =>
                    {
                        // (a >= b) == !(a < b)
                        Emit($"slt\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}");
                        Emit($"xori\t{Ret}, {Ret}, -1");
                    }),
                    CreateOperator("==", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) =>
                    {
                        // (a == b) == !(a xor b)
                        Emit($"xor\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}");
                        Emit($"xori\t{Ret}, {Ret}, -1");
                    }),
                    CreateOperator("!=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"xor\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator("<", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"flt.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator(">", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"flt.d\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[0]}"),
                    CreateOperator("<=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"fle.d\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateOperator(">=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"fle.d\t{Ret}, {RegisterArguments[1]}, {RegisterArguments[0]}"),
                    CreateOperator("==", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (RegisterArguments, Ret) =>
                    {
                        // (a == b) == !(a xor b)
                        Emit($"xor\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}");
                        Emit($"xori\t{Ret}, {Ret}, -1");
                    }),
                    CreateOperator("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (RegisterArguments, Ret) => $"xor\t{Ret}, {RegisterArguments[0]}, {RegisterArguments[1]}"),
                    CreateRuntimeFunction("round", PrimitiveType.Real, PrimitiveType.Int),
                    CreateRuntimeFunction("floor", PrimitiveType.Real, PrimitiveType.Int),
                    CreateRuntimeFunction("ceil", PrimitiveType.Real, PrimitiveType.Int),
                    CreateRuntimeFunction("round", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real),
                    CreateRuntimeFunction("floor", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real),
                    CreateRuntimeFunction("ceil", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real),
                    CreateRuntimeFunction("min", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("max", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("min", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("max", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("abs", PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("abs", PrimitiveType.Real, PrimitiveType.Real),
                    CreateRuntimeFunction("rand", PrimitiveType.Real),
                    CreateRuntimeFunction("rand", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
                    CreateRuntimeFunction("rand", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
                    CreateOperator("creal", PrimitiveType.Int, PrimitiveType.Real, false, (RegisterArguments, Ret) => $"fcvt.d.w\t{Ret}, {RegisterArguments[0]}")
                }).GroupBy(fb => fb.Name).ToDictionary(g => g.Key, g => g.ToList());

                var Reg = BuildExpr(f.Body);
                if (f.ReturnValue == PrimitiveType.Real)
                {
                    if (Reg.RegisterName != "fa0")
                    {
                        CopyArgumentToRegister(f.ReturnValue, Reg, "fa0");
                    }
                }
                else
                {
                    if (Reg.RegisterName != "a0")
                    {
                        CopyArgumentToRegister(f.ReturnValue, Reg, "a0");
                    }
                }

                StackSizeForSavedRegisters = 16 + (IntegerCalleeSavedRegisters.Count + FloatCalleeSavedRegisters.Count) * 8;
                StackSizeForLocal = ((StackSizeForSavedRegisters + StackSizeForLocal + StackSizeForChildArguments + 15) / 16) * 16 - StackSizeForSavedRegisters - StackSizeForChildArguments;
            }
            private FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType rt, bool IsIdentity, Func<List<String>, String, String> Build)
            {
                var ParameterTypes = new List<PrimitiveType> { pt0 };
                return new FunctionBuilder
                {
                    Name = Name,
                    ParameterTypes = ParameterTypes,
                    ReturnType = rt,
                    Build = Arguments =>
                    {
                        var RegisterArguments = ParameterTypes.Zip(Arguments, (pt, a) => { LoadArgument(pt, a); return a.RegisterName; }).ToList();
                        if ((pt0 != rt) || (RegisterArguments[0].StartsWith("a") && !IsIdentity))
                        {
                            var t0 = AcquireTemporaryRegister(rt);
                            Emit(Build(RegisterArguments, t0));
                            ReleaseVariable(Arguments[0]);
                            var vc = new VariableContext { Type = rt, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                            RegisterToVariableContext.Add(t0, vc);
                            return vc;
                        }
                        else
                        {
                            Emit(Build(RegisterArguments, RegisterArguments[0]));
                            return Arguments[0];
                        }
                    }
                };
            }
            private FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType rt, bool IsIdentity, Action<List<String>, String> Build)
            {
                var ParameterTypes = new List<PrimitiveType> { pt0 };
                return new FunctionBuilder
                {
                    Name = Name,
                    ParameterTypes = ParameterTypes,
                    ReturnType = rt,
                    Build = Arguments =>
                    {
                        var RegisterArguments = ParameterTypes.Zip(Arguments, (pt, a) => { LoadArgument(pt, a); return a.RegisterName; }).ToList();
                        if ((pt0 != rt) || (RegisterArguments[0].StartsWith("a") && !IsIdentity))
                        {
                            var t0 = AcquireTemporaryRegister(rt);
                            Build(RegisterArguments, t0);
                            ReleaseVariable(Arguments[0]);
                            var vc = new VariableContext { Type = rt, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                            RegisterToVariableContext.Add(t0, vc);
                            return vc;
                        }
                        else
                        {
                            Build(RegisterArguments, RegisterArguments[0]);
                            return Arguments[0];
                        }
                    }
                };
            }
            private FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<List<String>, String, String> Build)
            {
                var ParameterTypes = new List<PrimitiveType> { pt0, pt1 };
                return new FunctionBuilder
                {
                    Name = Name,
                    ParameterTypes = ParameterTypes,
                    ReturnType = rt,
                    Build = Arguments =>
                    {
                        var RegisterArguments = ParameterTypes.Zip(Arguments, (pt, a) => { LoadArgument(pt, a); return a.RegisterName; }).ToList();
                        if ((pt0 != rt) || RegisterArguments[0].StartsWith("a"))
                        {
                            var t0 = AcquireTemporaryRegister(rt);
                            Emit(Build(RegisterArguments, t0));
                            ReleaseVariable(Arguments[0]);
                            ReleaseVariable(Arguments[1]);
                            var vc = new VariableContext { Type = rt, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                            RegisterToVariableContext.Add(t0, vc);
                            return vc;
                        }
                        else
                        {
                            Emit(Build(RegisterArguments, RegisterArguments[0]));
                            ReleaseVariable(Arguments[1]);
                            return Arguments[0];
                        }
                    }
                };
            }
            private FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Action<List<String>, String> Build)
            {
                var ParameterTypes = new List<PrimitiveType> { pt0, pt1 };
                return new FunctionBuilder
                {
                    Name = Name,
                    ParameterTypes = ParameterTypes,
                    ReturnType = rt,
                    Build = Arguments =>
                    {
                        var RegisterArguments = ParameterTypes.Zip(Arguments, (pt, a) => { LoadArgument(pt, a); return a.RegisterName; }).ToList();
                        if ((pt0 != rt) || RegisterArguments[0].StartsWith("a"))
                        {
                            var t0 = AcquireTemporaryRegister(rt);
                            Build(RegisterArguments, t0);
                            ReleaseVariable(Arguments[0]);
                            ReleaseVariable(Arguments[1]);
                            var vc = new VariableContext { Type = rt, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                            RegisterToVariableContext.Add(t0, vc);
                            return vc;
                        }
                        else
                        {
                            Build(RegisterArguments, RegisterArguments[0]);
                            ReleaseVariable(Arguments[1]);
                            return Arguments[0];
                        }
                    }
                };
            }
            private FunctionBuilder CreateRuntimeFunction(String Name, List<PrimitiveType> ParameterTypes, PrimitiveType rt)
            {
                return new FunctionBuilder
                {
                    Name = Name,
                    ParameterTypes = ParameterTypes,
                    ReturnType = rt,
                    Build = Arguments =>
                    {
                        var ParameterSuffices = Arguments.Count == 0 ? "V" : String.Join("", ParameterTypes.Select(pt => pt.ToString().First().ToString()));
                        var FunctionName = $"Niveum_Expression_{Name}_{ParameterSuffices}";

                        return BuildFunctionCall(FunctionName, ParameterTypes, rt, Arguments);
                    }
                };
            }

            private FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType rt)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { }, rt);
            }
            private FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0, PrimitiveType rt)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0 }, rt);
            }
            private FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0, pt1 }, rt);
            }
            private FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0, pt1, pt2 }, rt);
            }

            public VariableContext BuildExpr(Expr e)
            {
                if (e.OnLiteral)
                {
                    if (e.Literal.OnBooleanValue)
                    {
                        var v = e.Literal.BooleanValue;
                        var t0 = AcquireTemporaryRegister(PrimitiveType.Int);
                        Emit($"addi\t{t0}, zero, {(v ? 1 : 0)}");
                        var vc = new VariableContext { Type = PrimitiveType.Boolean, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(t0, vc);
                        return vc;
                    }
                    else if (e.Literal.OnIntValue)
                    {
                        var v = e.Literal.IntValue;
                        var t0 = AcquireTemporaryRegister(PrimitiveType.Int);
                        Emit($"addi\t{t0}, zero, {v}");
                        var vc = new VariableContext { Type = PrimitiveType.Int, UseCount = 1, RegisterName = t0, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(t0, vc);
                        return vc;
                    }
                    else if (e.Literal.OnRealValue)
                    {
                        var v = e.Literal.RealValue;
                        var di = new DoubleInt64 { Float64Value = v };
                        var t0 = AcquireTemporaryRegister(PrimitiveType.Int);
                        var t1 = AcquireTemporaryRegister(PrimitiveType.Real);
                        Emit($"li\t{t0}, {di.Int64Value}");
                        Emit($"fmv.d.x\t{t1}, {t0}");
                        ReleaseRegister(PrimitiveType.Int, t0);
                        var vc = new VariableContext { Type = PrimitiveType.Real, UseCount = 1, RegisterName = t1, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(t1, vc);
                        return vc;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (e.OnVariable)
                {
                    return ParameterToVariableContext[e.Variable.Name];
                }
                else if (e.OnFunction)
                {
                    var Name = e.Function.Name;
                    var ParameterTypes = e.Function.ParameterTypes;
                    var Arguments = e.Function.Arguments.Select(p => BuildExpr(p)).ToList();
                    if (FunctionBuilders.ContainsKey(Name))
                    {
                        foreach (var fb in FunctionBuilders[Name])
                        {
                            if ((fb.ParameterTypes.Count == ParameterTypes.Count) && (fb.ParameterTypes.SequenceEqual(ParameterTypes)))
                            {
                                var vc = fb.Build(Arguments);
                                return vc;
                            }
                        }
                    }
                    {
                        var vc = BuildFunctionCall(Name, ParameterTypes, e.Function.ReturnType, Arguments);
                        return vc;
                    }
                }
                else if (e.OnIf)
                {
                    var FailLabel = $"{FunctionLabel}_lbl_{LabelNumber}";
                    LabelNumber += 1;
                    var EndLabel = $"{FunctionLabel}_lbl_{LabelNumber}";
                    LabelNumber += 1;

                    var c = BuildExpr(e.If.Condition);
                    Emit($"beq\t{c.RegisterName}, zero, {FailLabel}");

                    var l = BuildExpr(e.If.TruePart);
                    var lReg = l.RegisterName;
                    ReleaseVariable(l);
                    Emit($"j\t{EndLabel}");

                    Emit(FailLabel + ":");
                    var r = BuildExpr(e.If.FalsePart);
                    if (r.RegisterName != lReg)
                    {
                        if (RegisterToVariableContext.ContainsKey(lReg))
                        {
                            SaveToLocal(RegisterToVariableContext[lReg]);
                        }
                        AcquireTemporaryRegister(r.Type, lReg);
                        CopyArgumentToRegister(r.Type, r, lReg);
                        var vc = new VariableContext { Type = r.Type, UseCount = 1, RegisterName = lReg, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(lReg, vc);
                        Emit(EndLabel + ":");
                        return vc;
                    }
                    else
                    {
                        Emit(EndLabel + ":");
                        return r;
                    }
                }
                else if (e.OnAndAlso)
                {
                    var EndLabel = $"{FunctionLabel}_lbl_{LabelNumber}";
                    LabelNumber += 1;

                    var l = BuildExpr(e.AndAlso.Left);
                    Emit($"beq\t{l.RegisterName}, zero, {EndLabel}");
                    var lReg = l.RegisterName;
                    ReleaseVariable(l);

                    var r = BuildExpr(e.AndAlso.Right);
                    if (r.RegisterName != lReg)
                    {
                        if (RegisterToVariableContext.ContainsKey(lReg))
                        {
                            SaveToLocal(RegisterToVariableContext[lReg]);
                        }
                        AcquireTemporaryRegister(r.Type, lReg);
                        CopyArgumentToRegister(r.Type, r, lReg);
                        var vc = new VariableContext { Type = r.Type, UseCount = 1, RegisterName = lReg, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(lReg, vc);
                        Emit(EndLabel + ":");
                        return vc;
                    }
                    else
                    {
                        Emit(EndLabel + ":");
                        return r;
                    }
                }
                else if (e.OnOrElse)
                {
                    var EndLabel = $"{FunctionLabel}_lbl_{LabelNumber}";
                    LabelNumber += 1;

                    var l = BuildExpr(e.OrElse.Left);
                    Emit($"bne\t{l.RegisterName}, zero, {EndLabel}");
                    var lReg = l.RegisterName;
                    ReleaseVariable(l);

                    var r = BuildExpr(e.OrElse.Right);
                    if (r.RegisterName != lReg)
                    {
                        if (RegisterToVariableContext.ContainsKey(lReg))
                        {
                            SaveToLocal(RegisterToVariableContext[lReg]);
                        }
                        AcquireTemporaryRegister(r.Type, lReg);
                        CopyArgumentToRegister(r.Type, r, lReg);
                        var vc = new VariableContext { Type = r.Type, UseCount = 1, RegisterName = lReg, Offset = Optional<int>.Empty };
                        RegisterToVariableContext.Add(lReg, vc);
                        Emit(EndLabel + ":");
                        return vc;
                    }
                    else
                    {
                        Emit(EndLabel + ":");
                        return r;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private VariableContext BuildFunctionCall(String FunctionName, List<PrimitiveType> ParameterTypes, PrimitiveType rt, List<VariableContext> Arguments)
            {
                var FreeIntegerArgumentRegisters = new LinkedList<String>(Enumerable.Range(0, 7 + 1).Select(v => $"a{v}"));
                var FreeFloatArgumentRegisters = new LinkedList<String>(Enumerable.Range(0, 7 + 1).Select(v => $"fa{v}"));
                var CurrentCallRegisters = new HashSet<String>();
                var ActionStack = new Stack<Action>();
                var ReleaseQueue = new Queue<Action>();
                int StackParameterIndex = 0;
                foreach (var (pt, a) in ParameterTypes.Zip(Arguments, (pt, a) => (pt, a)))
                {
                    if (pt == PrimitiveType.Real)
                    {
                        if (FreeFloatArgumentRegisters.Count > 0)
                        {
                            var r = FreeFloatArgumentRegisters.First.Value;
                            FreeFloatArgumentRegisters.RemoveFirst();
                            ActionStack.Push(() =>
                            {
                                AcquireTemporaryRegister(pt, r);
                                CopyArgumentToRegister(pt, a, r);
                            });
                            ReleaseQueue.Enqueue(() =>
                            {
                                ReleaseRegister(pt, r);
                                ReleaseVariable(a);
                            });
                            CurrentCallRegisters.Add(r);
                            continue;
                        }
                    }
                    else
                    {
                        if (FreeIntegerArgumentRegisters.Count > 0)
                        {
                            var r = FreeIntegerArgumentRegisters.First.Value;
                            FreeIntegerArgumentRegisters.RemoveFirst();
                            ActionStack.Push(() =>
                            {
                                AcquireTemporaryRegister(pt, r);
                                CopyArgumentToRegister(pt, a, r);
                            });
                            ReleaseQueue.Enqueue(() =>
                            {
                                ReleaseRegister(pt, r);
                                ReleaseVariable(a);
                            });
                            CurrentCallRegisters.Add(r);
                            continue;
                        }
                    }
                    var i = StackParameterIndex;
                    ActionStack.Push(() =>
                    {
                        var SrcReg = AcquireTemporaryRegister(pt);
                        CopyArgumentToRegister(pt, a, SrcReg);
                        var DestReg = "sp";
                        var Offset = i * 8;
                        if (pt == PrimitiveType.Real)
                        {
                            Emit($"fsd\t{SrcReg}, {Offset}({DestReg})");
                        }
                        else
                        {
                            Emit($"sd\t{SrcReg}, {Offset}({DestReg})");
                        }
                        ReleaseRegister(pt, SrcReg);
                    });
                    ReleaseQueue.Enqueue(() => ReleaseVariable(a));
                    StackParameterIndex += 1;
                }

                EnsureStackSizeForChildArguments(StackParameterIndex * 8);

                foreach (var r in StandingIntegerTemporaryRegisters.ToList())
                {
                    SaveToLocal(RegisterToVariableContext[r]);
                }
                foreach (var r in StandingFloatTemporaryRegisters.ToList())
                {
                    SaveToLocal(RegisterToVariableContext[r]);
                }

                String ReturnRegister;
                if (rt == PrimitiveType.Real)
                {
                    ReturnRegister = "fa0";
                }
                else
                {
                    ReturnRegister = "a0";
                }
                if (RegisterToVariableContext.ContainsKey(ReturnRegister))
                {
                    SaveToLocal(RegisterToVariableContext[ReturnRegister]);
                }

                while (ActionStack.Count > 0)
                {
                    var a = ActionStack.Pop();
                    a();
                }

                Emit($"call {FunctionName}");

                while (ReleaseQueue.Count > 0)
                {
                    var a = ReleaseQueue.Dequeue();
                    a();
                }

                AcquireTemporaryRegister(rt, ReturnRegister);
                var vc = new VariableContext { Type = rt, UseCount = 1, RegisterName = ReturnRegister, Offset = Optional<int>.Empty };
                RegisterToVariableContext.Add(ReturnRegister, vc);
                return vc;
            }

            private void Emit(String Line)
            {
                OutputLineWrites.Add(() => Line);
            }
            private void Emit(Func<String> LineWrite)
            {
                OutputLineWrites.Add(LineWrite);
            }
            private String AcquireTemporaryRegister(PrimitiveType t)
            {
                if (t == PrimitiveType.Real)
                {
                    if (FreeFloatTemporaryRegisters.Count > 0)
                    {
                        var r = FreeFloatTemporaryRegisters.First.Value;
                        FreeFloatTemporaryRegisters.RemoveFirst();
                        FreeFloatTemporaryRegisterDict.Remove(r);
                        StandingFloatTemporaryRegisters.AddLast(r);
                        StandingFloatTemporaryRegisterDict.Add(r, StandingFloatTemporaryRegisters.Last);
                        return r;
                    }
                    if (FreeFloatCalleeSavedRegisters.Count > 0)
                    {
                        var r = FreeFloatCalleeSavedRegisters.First.Value;
                        FreeFloatCalleeSavedRegisters.RemoveFirst();
                        FreeFloatCalleeSavedRegisterDict.Remove(r);
                        StandingFloatCalleeSavedRegisters.AddLast(r);
                        StandingFloatCalleeSavedRegisterDict.Add(r, StandingFloatCalleeSavedRegisters.Last);
                        if (!FloatCalleeSavedRegisterSet.Contains(r))
                        {
                            FloatCalleeSavedRegisters.Add(r);
                            FloatCalleeSavedRegisterSet.Add(r);
                        }
                        return r;
                    }
                    if (StandingFloatTemporaryRegisters.Count > 0)
                    {
                        var r = StandingFloatTemporaryRegisters.First.Value;
                        var vc = RegisterToVariableContext[r];
                        SaveToLocal(vc);
                        AcquireTemporaryRegister(t, r);
                        return r;
                    }
                }
                else
                {
                    if (FreeIntegerTemporaryRegisters.Count > 0)
                    {
                        var r = FreeIntegerTemporaryRegisters.First.Value;
                        FreeIntegerTemporaryRegisters.RemoveFirst();
                        FreeIntegerTemporaryRegisterDict.Remove(r);
                        StandingIntegerTemporaryRegisters.AddLast(r);
                        StandingIntegerTemporaryRegisterDict.Add(r, StandingIntegerTemporaryRegisters.Last);
                        return r;
                    }
                    if (FreeIntegerCalleeSavedRegisters.Count > 0)
                    {
                        var r = FreeIntegerCalleeSavedRegisters.First.Value;
                        FreeIntegerCalleeSavedRegisters.RemoveFirst();
                        FreeIntegerCalleeSavedRegisterDict.Remove(r);
                        StandingIntegerCalleeSavedRegisters.AddLast(r);
                        StandingIntegerCalleeSavedRegisterDict.Add(r, StandingIntegerCalleeSavedRegisters.Last);
                        if (!IntegerCalleeSavedRegisterSet.Contains(r))
                        {
                            IntegerCalleeSavedRegisters.Add(r);
                            IntegerCalleeSavedRegisterSet.Add(r);
                        }
                        return r;
                    }
                    if (StandingIntegerTemporaryRegisters.Count > 0)
                    {
                        var r = StandingIntegerTemporaryRegisters.First.Value;
                        var vc = RegisterToVariableContext[r];
                        SaveToLocal(vc);
                        AcquireTemporaryRegister(t, r);
                        return r;
                    }
                }
                throw new InvalidOperationException();
            }
            private void AcquireTemporaryRegister(PrimitiveType t, String r)
            {
                if (t == PrimitiveType.Real)
                {
                    if (FreeFloatTemporaryRegisterDict.ContainsKey(r))
                    {
                        FreeFloatTemporaryRegisters.Remove(FreeFloatTemporaryRegisterDict[r]);
                        FreeFloatTemporaryRegisterDict.Remove(r);
                        StandingFloatTemporaryRegisters.AddLast(r);
                        StandingFloatTemporaryRegisterDict.Add(r, StandingFloatTemporaryRegisters.Last);
                        return;
                    }
                    if (FreeFloatCalleeSavedRegisterDict.ContainsKey(r))
                    {
                        FreeFloatCalleeSavedRegisters.Remove(FreeFloatCalleeSavedRegisterDict[r]);
                        FreeFloatCalleeSavedRegisterDict.Remove(r);
                        StandingFloatCalleeSavedRegisters.AddLast(r);
                        StandingFloatCalleeSavedRegisterDict.Add(r, StandingFloatCalleeSavedRegisters.Last);
                        if (!FloatCalleeSavedRegisterSet.Contains(r))
                        {
                            FloatCalleeSavedRegisters.Add(r);
                            FloatCalleeSavedRegisterSet.Add(r);
                        }
                        return;
                    }
                }
                else
                {
                    if (FreeIntegerTemporaryRegisterDict.ContainsKey(r))
                    {
                        FreeIntegerTemporaryRegisters.Remove(FreeIntegerTemporaryRegisterDict[r]);
                        FreeIntegerTemporaryRegisterDict.Remove(r);
                        StandingIntegerTemporaryRegisters.AddLast(r);
                        StandingIntegerTemporaryRegisterDict.Add(r, StandingIntegerTemporaryRegisters.Last);
                        return;
                    }
                    if (FreeIntegerCalleeSavedRegisterDict.ContainsKey(r))
                    {
                        FreeIntegerCalleeSavedRegisters.Remove(FreeIntegerCalleeSavedRegisterDict[r]);
                        FreeIntegerCalleeSavedRegisterDict.Remove(r);
                        StandingIntegerCalleeSavedRegisters.AddLast(r);
                        StandingIntegerCalleeSavedRegisterDict.Add(r, StandingIntegerCalleeSavedRegisters.Last);
                        if (!IntegerCalleeSavedRegisterSet.Contains(r))
                        {
                            IntegerCalleeSavedRegisters.Add(r);
                            IntegerCalleeSavedRegisterSet.Add(r);
                        }
                        return;
                    }
                }
                throw new InvalidOperationException();
            }
            private void ReleaseVariable(VariableContext v)
            {
                if (v.UseCount <= 0) { throw new InvalidOperationException(); }
                v.UseCount -= 1;
                if ((v.UseCount == 0) && (v.Offset.OnNone))
                {
                    ReleaseRegister(v.Type, v.RegisterName);
                }
            }
            private void ReleaseRegister(PrimitiveType t, String Reg)
            {
                RegisterToVariableContext.Remove(Reg);
                if (t == PrimitiveType.Real)
                {
                    if (StandingFloatTemporaryRegisterDict.ContainsKey(Reg))
                    {
                        StandingFloatTemporaryRegisters.Remove(StandingFloatTemporaryRegisterDict[Reg]);
                        StandingFloatTemporaryRegisterDict.Remove(Reg);
                        FreeFloatTemporaryRegisters.AddLast(Reg);
                        FreeFloatTemporaryRegisterDict.Add(Reg, FreeFloatTemporaryRegisters.Last);
                    }
                    else if (StandingFloatCalleeSavedRegisterDict.ContainsKey(Reg))
                    {
                        StandingFloatCalleeSavedRegisters.Remove(StandingFloatCalleeSavedRegisterDict[Reg]);
                        StandingFloatCalleeSavedRegisterDict.Remove(Reg);
                        FreeFloatCalleeSavedRegisters.AddLast(Reg);
                        FreeFloatCalleeSavedRegisterDict.Add(Reg, FreeFloatCalleeSavedRegisters.Last);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (StandingIntegerTemporaryRegisterDict.ContainsKey(Reg))
                    {
                        StandingIntegerTemporaryRegisters.Remove(StandingIntegerTemporaryRegisterDict[Reg]);
                        StandingIntegerTemporaryRegisterDict.Remove(Reg);
                        FreeIntegerTemporaryRegisters.AddLast(Reg);
                        FreeIntegerTemporaryRegisterDict.Add(Reg, FreeIntegerTemporaryRegisters.Last);
                    }
                    else if (StandingIntegerCalleeSavedRegisterDict.ContainsKey(Reg))
                    {
                        StandingIntegerCalleeSavedRegisters.Remove(StandingIntegerCalleeSavedRegisterDict[Reg]);
                        StandingIntegerCalleeSavedRegisterDict.Remove(Reg);
                        FreeIntegerCalleeSavedRegisters.AddLast(Reg);
                        FreeIntegerCalleeSavedRegisterDict.Add(Reg, FreeIntegerCalleeSavedRegisters.Last);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
            private void SaveToLocal(VariableContext v)
            {
                if (v.Offset.OnSome) { return; }
                StackOffsetForLocal += 8;
                v.Offset = -StackOffsetForLocal;
                StackSizeForLocal = Math.Max(StackSizeForLocal, StackOffsetForLocal);
                var Reg = v.RegisterName;
                var Offset = v.Offset.Value;
                if (v.Type == PrimitiveType.Real)
                {
                    Emit(() => $"fsd\t{Reg}, {Offset - StackSizeForSavedRegisters}(fp)");
                }
                else
                {
                    Emit(() => $"sd\t{Reg}, {Offset - StackSizeForSavedRegisters}(fp)");
                }
                v.RegisterName = "fp";
                ReleaseRegister(v.Type, Reg);
                RegisterToVariableContext.Remove(Reg);
            }
            private void LoadArgument(PrimitiveType pt, VariableContext Argument)
            {
                if (pt != Argument.Type) { throw new InvalidOperationException(); }
                if (Argument.Offset.OnNone) { return; }
                if (Argument.RegisterName != "fp") { throw new InvalidOperationException(); }
                var Offset = Argument.Offset.Value;
                String Reg;
                if (pt == PrimitiveType.Real)
                {
                    Reg = AcquireTemporaryRegister(PrimitiveType.Real);
                    Emit(() => $"fld\t{Reg}, {(Offset >= 0 ? Offset : Offset - StackSizeForSavedRegisters)}(fp)");
                }
                else
                {
                    Reg = AcquireTemporaryRegister(PrimitiveType.Int);
                    Emit(() => $"ld\t{Reg}, {(Offset >= 0 ? Offset : Offset - StackSizeForSavedRegisters)}(fp)");
                }
                Argument.RegisterName = Reg;
                Argument.Offset = Optional<int>.Empty;
                RegisterToVariableContext.Add(Reg, Argument);
                //TODO: reuse local variable space
            }
            private void CopyArgumentToRegister(PrimitiveType pt, VariableContext Argument, String Reg)
            {
                if (pt != Argument.Type) { throw new InvalidOperationException(); }
                if (Argument.Offset.OnNone && (Argument.RegisterName == Reg)) { throw new InvalidOperationException(); }
                if (Argument.Offset.OnNone)
                {
                    if (pt == PrimitiveType.Real)
                    {
                        Emit($"fsgnj.d\t{Reg}, {Argument.RegisterName}, {Argument.RegisterName}");
                    }
                    else
                    {
                        Emit($"add\t{Reg}, {Argument.RegisterName}, zero");
                    }
                }
                else
                {
                    if (Argument.RegisterName != "fp") { throw new InvalidOperationException(); }
                    var Offset = Argument.Offset.Value;
                    if (pt == PrimitiveType.Real)
                    {
                        Emit(() => $"fld\t{Reg}, {(Offset >= 0 ? Offset : Offset - StackSizeForSavedRegisters)}(fp)");
                    }
                    else
                    {
                        Emit(() => $"ld\t{Reg}, {(Offset >= 0 ? Offset : Offset - StackSizeForSavedRegisters)}(fp)");
                    }
                }
            }
            private void EnsureStackSizeForChildArguments(int Size)
            {
                StackSizeForChildArguments = Math.Max(Size, StackSizeForChildArguments);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct DoubleInt64
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public double Float64Value;
            [System.Runtime.InteropServices.FieldOffset(0)]
            public Int64 Int64Value;
        }

        private static void ForEachExpr(Expr e, Action<Expr> a)
        {
            a(e);
            if (e.OnLiteral || e.OnVariable)
            {
            }
            else if (e.OnFunction)
            {
                foreach (var Argument in e.Function.Arguments)
                {
                    ForEachExpr(Argument, a);
                }
            }
            else if (e.OnIf)
            {
                ForEachExpr(e.If.Condition, a);
                ForEachExpr(e.If.TruePart, a);
                ForEachExpr(e.If.FalsePart, a);
            }
            else if (e.OnAndAlso)
            {
                ForEachExpr(e.AndAlso.Left, a);
                ForEachExpr(e.AndAlso.Right, a);
            }
            else if (e.OnOrElse)
            {
                ForEachExpr(e.OrElse.Left, a);
                ForEachExpr(e.OrElse.Right, a);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
