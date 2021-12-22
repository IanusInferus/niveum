//==========================================================================
//
//  File:        TypeBinder.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 类型绑定器
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;

namespace Niveum.ExpressionSchema
{
    public sealed class FunctionParameterAndReturnTypes
    {
        public Optional<List<PrimitiveType>> ParameterTypes { get; init; }
        public PrimitiveType ReturnType { get; init; }
    }

    public interface IVariableTypeProvider
    {
        List<FunctionParameterAndReturnTypes> GetOverloads(String Name);
        List<PrimitiveType> GetMatched(String Name, Optional<List<PrimitiveType>> ParameterTypes);
    }

    public class VariableTypeProviderCombiner : IVariableTypeProvider
    {
        private List<IVariableTypeProvider> Providers;

        public VariableTypeProviderCombiner(params IVariableTypeProvider[] Providers)
        {
            this.Providers = Providers.ToList();
        }

        public List<FunctionParameterAndReturnTypes> GetOverloads(String Name)
        {
            return Providers.SelectMany(p => p.GetOverloads(Name)).ToList();
        }

        public List<PrimitiveType> GetMatched(String Name, Optional<List<PrimitiveType>> ParameterTypes)
        {
            return Providers.SelectMany(p => p.GetMatched(Name, ParameterTypes)).ToList();
        }
    }

    public class SimpleVariableTypeProvider : IVariableTypeProvider
    {
        private Dictionary<String, PrimitiveType> d;

        public SimpleVariableTypeProvider(Dictionary<String, PrimitiveType> d)
        {
            this.d = d;
        }

        public List<FunctionParameterAndReturnTypes> GetOverloads(String Name)
        {
            if (d.ContainsKey(Name))
            {
                return new List<FunctionParameterAndReturnTypes> { new FunctionParameterAndReturnTypes { ParameterTypes = Optional<List<PrimitiveType>>.Empty, ReturnType = d[Name] } };
            }
            else
            {
                return new List<FunctionParameterAndReturnTypes> { };
            }
        }

        public List<PrimitiveType> GetMatched(String Name, Optional<List<PrimitiveType>> ParameterTypes)
        {
            if (ParameterTypes.OnSome && (ParameterTypes.Value.Count == 0) && d.ContainsKey(Name))
            {
                return new List<PrimitiveType> { d[Name] };
            }
            return new List<PrimitiveType> { };
        }
    }

    public sealed class TypeBinderResult
    {
        public Expr Semantics { get; init; }
        public Dictionary<Expr, PrimitiveType> TypeDict { get; init; }
    }

    public class TypeBinder
    {
        private Text Text;
        private Dictionary<Object, TextRange> Positions;
        private SemanticTranslator st;

        public TypeBinder(Text Text, Dictionary<Object, TextRange> Positions)
        {
            this.Text = Text;
            this.Positions = Positions;
            this.st = new SemanticTranslator(Text, Positions);
        }

        public TypeBinderResult Bind(IVariableTypeProvider VariableTypeProvider, TextRange RangeInLine)
        {
            var Result = st.Translate(RangeInLine);

            var TypeDict = new Dictionary<Expr, PrimitiveType>();
            var Semantics = BindExpr(VariableTypeProvider, TypeDict, Result.Semantics);
            var tbr = new TypeBinderResult
            {
                Semantics = Semantics,
                TypeDict = TypeDict
            };
            return tbr;
        }
        public TypeBinderResult Bind(IVariableTypeProvider VariableTypeProvider, PrimitiveType ReturnType, TextRange RangeInLine)
        {
            var Result = st.Translate(RangeInLine);

            var TypeDict = new Dictionary<Expr, PrimitiveType>();
            var Semantics = BindExpr(VariableTypeProvider, TypeDict, Result.Semantics);
            var rt = TypeDict[Semantics];
            if (rt == PrimitiveType.Int && ReturnType == PrimitiveType.Real)
            {
                var feCReal = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { Result.Semantics } };
                var CReal = Expr.CreateFunction(feCReal);
                var Range = Positions[Semantics];
                Positions.Add(feCReal, Range);
                Positions.Add(CReal, Range);
                TypeDict.Add(CReal, PrimitiveType.Real);
                Semantics = CReal;
            }
            else if (rt != ReturnType)
            {
                throw new InvalidSyntaxException("TypeMismatch : '{0}' expected.".Formats(GetTypeString(ReturnType)), new FileTextRange { Text = Text, Range = Positions[Result.Semantics] });
            }

            var tbr = new TypeBinderResult
            {
                Semantics = Semantics,
                TypeDict = TypeDict
            };
            return tbr;
        }

        private Expr BindExpr(IVariableTypeProvider VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, Expr e)
        {
            if (e.OnLiteral)
            {
                var l = e.Literal;
                if (l.OnBooleanValue)
                {
                    TypeDict.Add(e, PrimitiveType.Boolean);
                }
                else if (l.OnIntValue)
                {
                    TypeDict.Add(e, PrimitiveType.Int);
                }
                else if (l.OnRealValue)
                {
                    TypeDict.Add(e, PrimitiveType.Real);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return e;
            }
            else if (e.OnVariable)
            {
                List<FunctionParameterAndReturnTypes> t;
                try
                {
                    t = VariableTypeProvider.GetOverloads(e.Variable.Name).Where(fs => fs.ParameterTypes.OnNone).ToList();
                }
                catch (Exception ex)
                {
                    throw new InvalidSyntaxException(String.Format("'{0}' : VariableNotExist", e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] }, ex);
                }
                if (t.Count == 0)
                {
                    throw new InvalidSyntaxException("'{0}' : VariableNotExist".Formats(e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                if (t.Count > 1)
                {
                    throw new InvalidSyntaxException("'{0}' : VariableFunctionOverloadExist".Formats(e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                TypeDict.Add(e, t.Single().ReturnType);
                return e;
            }
            else if (e.OnFunction)
            {
                var (fe, t) = BindFunctionExpr(VariableTypeProvider, TypeDict, e.Function);
                var Result = Expr.CreateFunction(fe);
                Positions.Add(Result, Positions[e]);
                TypeDict.Add(Result, t);
                return Result;
            }
            else if (e.OnIf)
            {
                var Condition = BindExpr(VariableTypeProvider, TypeDict, e.If.Condition);
                var ConditionType = TypeDict[Condition];
                if (ConditionType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Condition] });
                }
                var TruePart = BindExpr(VariableTypeProvider, TypeDict, e.If.TruePart);
                var TruePartType = TypeDict[TruePart];
                var FalsePart = BindExpr(VariableTypeProvider, TypeDict, e.If.FalsePart);
                var FalsePartType = TypeDict[FalsePart];
                if (TruePartType == PrimitiveType.Int && FalsePartType == PrimitiveType.Real)
                {
                    var fe = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { TruePart } };
                    var CReal = Expr.CreateFunction(fe);
                    var Range = Positions[TruePart];
                    Positions.Add(fe, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    var ie = new IfExpr { Condition = Condition, TruePart = CReal, FalsePart = FalsePart };
                    Positions.Add(ie, Positions[e.If]);
                    var Result = Expr.CreateIf(ie);
                    Positions.Add(Result, Positions[e]);
                    TypeDict.Add(Result, PrimitiveType.Real);
                    return Result;
                }
                else if (TruePartType == PrimitiveType.Real && FalsePartType == PrimitiveType.Int)
                {
                    var fe = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { FalsePart } };
                    var CReal = Expr.CreateFunction(fe);
                    var Range = Positions[FalsePart];
                    Positions.Add(fe, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    var ie = new IfExpr { Condition = Condition, TruePart = TruePart, FalsePart = CReal };
                    Positions.Add(ie, Positions[e.If]);
                    var Result = Expr.CreateIf(ie);
                    Positions.Add(Result, Positions[e]);
                    TypeDict.Add(Result, PrimitiveType.Real);
                    return Result;
                }
                else if (TruePartType != FalsePartType)
                {
                    throw new InvalidSyntaxException("TypeMismatch: true_part is '{0}' and false_part is '{1}'.".Formats(GetTypeString(TruePartType), GetTypeString(FalsePartType)), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                else
                {
                    var ie = new IfExpr { Condition = Condition, TruePart = TruePart, FalsePart = FalsePart };
                    Positions.Add(ie, Positions[e.If]);
                    var Result = Expr.CreateIf(ie);
                    Positions.Add(Result, Positions[e]);
                    TypeDict.Add(Result, TruePartType);
                    return Result;
                }
            }
            else if (e.OnAndAlso)
            {
                var Left = BindExpr(VariableTypeProvider, TypeDict, e.AndAlso.Left);
                var LeftType = TypeDict[Left];
                if (LeftType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Left] });
                }
                var Right = BindExpr(VariableTypeProvider, TypeDict, e.AndAlso.Right);
                var RightType = TypeDict[Right];
                if (RightType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Right] });
                }
                var aae = new AndAlsoExpr { Left = Left, Right = Right };
                Positions.Add(aae, Positions[e.AndAlso]);
                var Result = Expr.CreateAndAlso(aae);
                Positions.Add(Result, Positions[e]);
                TypeDict.Add(Result, PrimitiveType.Boolean);
                return Result;
            }
            else if (e.OnOrElse)
            {
                var Left = BindExpr(VariableTypeProvider, TypeDict, e.OrElse.Left);
                var LeftType = TypeDict[Left];
                if (LeftType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Left] });
                }
                var Right = BindExpr(VariableTypeProvider, TypeDict, e.OrElse.Right);
                var RightType = TypeDict[Right];
                if (RightType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Right] });
                }
                var oee = new OrElseExpr { Left = Left, Right = Right };
                Positions.Add(oee, Positions[e.OrElse]);
                var Result = Expr.CreateOrElse(oee);
                Positions.Add(Result, Positions[e]);
                TypeDict.Add(Result, PrimitiveType.Boolean);
                return Result;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private Tuple<FunctionExpr, PrimitiveType> BindFunctionExpr(IVariableTypeProvider VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, FunctionExpr fe)
        {
            var Parameters = fe.Parameters.Select(p => BindExpr(VariableTypeProvider, TypeDict, p)).ToList();
            var ParameterTypes = Parameters.Select(p => TypeDict[p]).ToList();
            List<FunctionParameterAndReturnTypes> Functions;
            try
            {
                Functions = VariableTypeProvider.GetOverloads(fe.Name);
            }
            catch (Exception ex)
            {
                throw new InvalidSyntaxException("'{0}' : FunctionNotExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] }, ex);
            }
            var Sorted = Functions.Where(fs => IsOverloadSatisfied(ParameterTypes, fs.ParameterTypes)).GroupBy(fs => GetOverloadTypeConversionPoint(ParameterTypes, fs.ParameterTypes.Value)).OrderBy(g => g.Key).ToList();
            if (Sorted.Count == 0)
            {
                throw new InvalidSyntaxException("'{0}' : FunctionNotExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] });
            }
            var MostMatchedGroup = Sorted.First().ToList();
            if (MostMatchedGroup.Count > 1)
            {
                throw new InvalidSyntaxException("'{0}' : MultipleFunctionOverloadExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] });
            }
            var MostMatchedSignature = MostMatchedGroup.Single();
            for (int k = 0; k < ParameterTypes.Count; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = MostMatchedSignature.ParameterTypes.Value[k];
                if (pt == PrimitiveType.Int && fspt == PrimitiveType.Real)
                {
                    var feCReal = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { fe.Parameters[k] } };
                    var CReal = Expr.CreateFunction(feCReal);
                    var Range = Positions[fe.Parameters[k]];
                    Positions.Add(feCReal, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    Parameters[k] = CReal;
                }
            }

            var Result = new FunctionExpr { Name = fe.Name, Parameters = Parameters };
            Positions.Add(Result, Positions[fe]);
            return Tuple.Create(Result, MostMatchedSignature.ReturnType);
        }

        private static bool IsOverloadSatisfied(List<PrimitiveType> ParameterTypes, Optional<List<PrimitiveType>> fs)
        {
            if (fs.OnNone) { return false; }
            var fsValue = fs.Value;
            if (ParameterTypes.Count != fsValue.Count) { return false; }
            for (int k = 0; k < ParameterTypes.Count; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = fsValue[k];
                if (pt != fspt)
                {
                    if (pt == PrimitiveType.Int && fspt == PrimitiveType.Real)
                    {
                        continue;
                    }
                    return false;
                }
            }
            return true;
        }
        private static int GetOverloadTypeConversionPoint(List<PrimitiveType> ParameterTypes, List<PrimitiveType> fs)
        {
            var Point = 0;
            for (int k = 0; k < ParameterTypes.Count; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = fs[k];
                if (pt == PrimitiveType.Int && fspt == PrimitiveType.Real)
                {
                    Point += 1;
                }
            }
            return Point;
        }

        private static String GetTypeString(PrimitiveType t)
        {
            if (t == PrimitiveType.Boolean)
            {
                return "Boolean";
            }
            else if (t == PrimitiveType.Int)
            {
                return "Int";
            }
            else if (t == PrimitiveType.Real)
            {
                return "Real";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
