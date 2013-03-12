//==========================================================================
//
//  File:        TypeBinder.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 类型绑定器
//  Version:     2013.03.12.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;
using Firefly.Texting.TreeFormat.Semantics;

namespace Yuki.ExpressionSchema
{
    public interface IVariableTypeProvider
    {
        PrimitiveType[][] GetOverloads(String Name);
        PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes);
    }

    public class VariableTypeProviderCombiner : IVariableTypeProvider
    {
        private IVariableTypeProvider[] Providers;

        public VariableTypeProviderCombiner(params IVariableTypeProvider[] Providers)
        {
            this.Providers = Providers;
        }

        public PrimitiveType[][] GetOverloads(String Name)
        {
            return Providers.SelectMany(p => p.GetOverloads(Name)).ToArray();
        }

        public PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes)
        {
            return Providers.SelectMany(p => p.GetMatched(Name, ParameterTypes)).ToArray();
        }
    }

    public class SimpleVariableTypeProvider : IVariableTypeProvider
    {
        private Dictionary<String, PrimitiveType> d;

        public SimpleVariableTypeProvider(Dictionary<String, PrimitiveType> d)
        {
            this.d = d;
        }

        public PrimitiveType[][] GetOverloads(String Name)
        {
            if (d.ContainsKey(Name))
            {
                return new PrimitiveType[][] { new PrimitiveType[] { d[Name] } };
            }
            else
            {
                return new PrimitiveType[][] { };
            }
        }

        public PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes)
        {
            if (ParameterTypes.Length == 0 && d.ContainsKey(Name))
            {
                return new PrimitiveType[] { d[Name] };
            }
            return new PrimitiveType[] { };
        }
    }

    public class TypeBinderResult
    {
        public Expr Semantics;
        public Dictionary<Expr, PrimitiveType> TypeDict;
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
            BindExpr(VariableTypeProvider, TypeDict, Result.Semantics);
            var tbr = new TypeBinderResult
            {
                Semantics = Result.Semantics,
                TypeDict = TypeDict
            };
            return tbr;
        }
        public TypeBinderResult Bind(IVariableTypeProvider VariableTypeProvider, PrimitiveType ReturnType, TextRange RangeInLine)
        {
            var Result = st.Translate(RangeInLine);

            var TypeDict = new Dictionary<Expr, PrimitiveType>();
            BindExpr(VariableTypeProvider, TypeDict, Result.Semantics);
            var rt = TypeDict[Result.Semantics];
            if (rt == PrimitiveType.Int && ReturnType == PrimitiveType.Real)
            {
                var feCReal = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { Result.Semantics } };
                var CReal = Expr.CreateFunction(feCReal);
                var Range = Positions[Result.Semantics];
                Positions.Add(feCReal, Range);
                Positions.Add(CReal, Range);
                TypeDict.Add(CReal, PrimitiveType.Real);
                Result.Semantics = CReal;
            }
            else if (rt != ReturnType)
            {
                throw new InvalidSyntaxException("TypeMismatch : '{0}' expected.".Formats(GetTypeString(ReturnType)), new FileTextRange { Text = Text, Range = Positions[Result.Semantics] });
            }

            var tbr = new TypeBinderResult
            {
                Semantics = Result.Semantics,
                TypeDict = TypeDict
            };
            return tbr;
        }

        private void BindExpr(IVariableTypeProvider VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, Expr e)
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
            }
            else if (e.OnVariable)
            {
                PrimitiveType[][] t;
                try
                {
                    t = VariableTypeProvider.GetOverloads(e.Variable.Name).Where(fs => fs.Length == 1).ToArray();
                }
                catch (Exception ex)
                {
                    throw new InvalidSyntaxException(String.Format("'{0}' : VariableNotExist", e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] }, ex);
                }
                if (t.Length == 0)
                {
                    throw new InvalidSyntaxException("'{0}' : VariableNotExist".Formats(e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                if (t.Length > 1)
                {
                    throw new InvalidSyntaxException("'{0}' : VariableFunctionOverloadExist".Formats(e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                TypeDict.Add(e, t.Single().Single());
            }
            else if (e.OnFunction)
            {
                var t = BindFunctionExpr(VariableTypeProvider, TypeDict, e.Function);
                TypeDict.Add(e, t);
            }
            else if (e.OnIf)
            {
                var Condition = e.If.Condition;
                var TruePart = e.If.TruePart;
                var FalsePart = e.If.FalsePart;

                BindExpr(VariableTypeProvider, TypeDict, Condition);
                var ConditionType = TypeDict[Condition];
                if (ConditionType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Condition] });
                }
                BindExpr(VariableTypeProvider, TypeDict, TruePart);
                var TruePartType = TypeDict[TruePart];
                BindExpr(VariableTypeProvider, TypeDict, FalsePart);
                var FalsePartType = TypeDict[FalsePart];
                PrimitiveType ReturnType;
                if (TruePartType == PrimitiveType.Int && FalsePartType == PrimitiveType.Real)
                {
                    var fe = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { TruePart } };
                    var CReal = Expr.CreateFunction(fe);
                    var Range = Positions[TruePart];
                    Positions.Add(fe, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    e.If.TruePart = CReal;
                    ReturnType = PrimitiveType.Real;
                }
                else if (TruePartType == PrimitiveType.Real && FalsePartType == PrimitiveType.Int)
                {
                    var fe = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { FalsePart } };
                    var CReal = Expr.CreateFunction(fe);
                    var Range = Positions[FalsePart];
                    Positions.Add(fe, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    e.If.FalsePart = CReal;
                    ReturnType = PrimitiveType.Real;
                }
                else if (TruePartType != FalsePartType)
                {
                    throw new InvalidSyntaxException("TypeMismatch: true_part is '{0}' and false_part is '{1}'.".Formats(GetTypeString(TruePartType), GetTypeString(FalsePartType)), new FileTextRange { Text = Text, Range = Positions[e] });
                }
                else
                {
                    ReturnType = TruePartType;
                }
                TypeDict.Add(e, ReturnType);
            }
            else if (e.OnAndAlso)
            {
                var Left = e.AndAlso.Left;
                var Right = e.AndAlso.Right;
                BindExpr(VariableTypeProvider, TypeDict, Left);
                var LeftType = TypeDict[Left];
                if (LeftType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Left] });
                }
                BindExpr(VariableTypeProvider, TypeDict, Right);
                var RightType = TypeDict[Right];
                if (RightType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Right] });
                }
                TypeDict.Add(e, PrimitiveType.Boolean);
            }
            else if (e.OnOrElse)
            {
                var Left = e.OrElse.Left;
                var Right = e.OrElse.Right;
                BindExpr(VariableTypeProvider, TypeDict, Left);
                var LeftType = TypeDict[Left];
                if (LeftType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Left] });
                }
                BindExpr(VariableTypeProvider, TypeDict, Right);
                var RightType = TypeDict[Right];
                if (RightType != PrimitiveType.Boolean)
                {
                    throw new InvalidSyntaxException("TypeMismatch : 'Boolean' expected.", new FileTextRange { Text = Text, Range = Positions[Right] });
                }
                TypeDict.Add(e, PrimitiveType.Boolean);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private PrimitiveType BindFunctionExpr(IVariableTypeProvider VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, FunctionExpr fe)
        {
            foreach (var p in fe.Parameters)
            {
                BindExpr(VariableTypeProvider, TypeDict, p);
            }
            var ParameterTypes = fe.Parameters.Select(p => TypeDict[p]).ToArray();
            PrimitiveType[][] Functions;
            try
            {
                Functions = VariableTypeProvider.GetOverloads(fe.Name);
            }
            catch (Exception ex)
            {
                throw new InvalidSyntaxException("'{0}' : FunctionNotExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] }, ex);
            }
            var Sorted = Functions.Where(fs => IsOverloadSatisfied(ParameterTypes, fs)).GroupBy(fs => GetOverloadTypeConversionPoint(ParameterTypes, fs)).OrderBy(g => g.Key).ToArray();
            if (Sorted.Length == 0)
            {
                throw new InvalidSyntaxException("'{0}' : FunctionNotExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] });
            }
            var MostMatchedGroup = Sorted.First().ToArray();
            if (MostMatchedGroup.Length > 1)
            {
                throw new InvalidSyntaxException("'{0}' : MultipleFunctionOverloadExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] });
            }
            var MostMatchedSignature = MostMatchedGroup.Single();
            for (int k = 0; k < ParameterTypes.Length; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = MostMatchedSignature[k];
                if (pt == PrimitiveType.Int && fspt == PrimitiveType.Real)
                {
                    var feCReal = new FunctionExpr { Name = "creal", Parameters = new List<Expr> { fe.Parameters[k] } };
                    var CReal = Expr.CreateFunction(feCReal);
                    var Range = Positions[fe.Parameters[k]];
                    Positions.Add(feCReal, Range);
                    Positions.Add(CReal, Range);
                    TypeDict.Add(CReal, PrimitiveType.Real);
                    fe.Parameters[k] = CReal;
                }
            }
            return MostMatchedSignature.Last();
        }

        private static bool IsOverloadSatisfied(PrimitiveType[] ParameterTypes, PrimitiveType[] fs)
        {
            if (ParameterTypes.Length != fs.Length - 1) { return false; }
            for (int k = 0; k < ParameterTypes.Length; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = fs[k];
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
        private static int GetOverloadTypeConversionPoint(PrimitiveType[] ParameterTypes, PrimitiveType[] fs)
        {
            var Point = 0;
            for (int k = 0; k < ParameterTypes.Length; k += 1)
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
