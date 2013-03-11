//==========================================================================
//
//  File:        TypeBinder.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 类型绑定器
//  Version:     2013.03.11.
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
    public class TypeBinderResult
    {
        public Expr Semantics;
        public Dictionary<Expr, PrimitiveType> TypeDict;
    }

    public class FunctionSignature
    {
        public String Name;
        public PrimitiveType[] ParameterTypes;
        public PrimitiveType ReturnType;

        public FunctionSignature()
        {
        }
        public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt)
        {
            this.Name = Name;
            this.ParameterTypes = new PrimitiveType[] { pt0, pt1, pt2 };
            this.ReturnType = rt;
        }
        public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt)
        {
            this.Name = Name;
            this.ParameterTypes = new PrimitiveType[] { pt0, pt1 };
            this.ReturnType = rt;
        }
        public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType rt)
        {
            this.Name = Name;
            this.ParameterTypes = new PrimitiveType[] { pt0 };
            this.ReturnType = rt;
        }
        public FunctionSignature(String Name, PrimitiveType rt)
        {
            this.Name = Name;
            this.ParameterTypes = new PrimitiveType[] { };
            this.ReturnType = rt;
        }
    }

    public static class FunctionSignatureMap
    {
        public static Dictionary<String, List<FunctionSignature>> Map;

        static FunctionSignatureMap()
        {
            var l = new List<FunctionSignature>();

            //算术运算
            l.Add(new FunctionSignature("+", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("-", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("*", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("/", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Real));
            l.Add(new FunctionSignature("+", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("-", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("*", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("/", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("pow", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("pow", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("mod", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("div", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));

            //逻辑运算
            l.Add(new FunctionSignature("!", PrimitiveType.Boolean, PrimitiveType.Boolean));

            //关系运算
            l.Add(new FunctionSignature("<", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature(">", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("<=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature(">=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("==", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("!=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("<", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
            l.Add(new FunctionSignature(">", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("<=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
            l.Add(new FunctionSignature(">=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("==", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean));
            l.Add(new FunctionSignature("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean));

            //取整运算
            l.Add(new FunctionSignature("round", PrimitiveType.Real, PrimitiveType.Int));
            l.Add(new FunctionSignature("floor", PrimitiveType.Real, PrimitiveType.Int));
            l.Add(new FunctionSignature("ceil", PrimitiveType.Real, PrimitiveType.Int));
            l.Add(new FunctionSignature("round", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));
            l.Add(new FunctionSignature("floor", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));
            l.Add(new FunctionSignature("ceil", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));

            //范围限制运算
            l.Add(new FunctionSignature("min", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("max", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("min", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("max", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));

            //其他运算
            l.Add(new FunctionSignature("abs", PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("rand", PrimitiveType.Real));
            l.Add(new FunctionSignature("rand", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
            l.Add(new FunctionSignature("rand", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
            l.Add(new FunctionSignature("creal", PrimitiveType.Int, PrimitiveType.Real));

            Map = new Dictionary<String, List<FunctionSignature>>();
            foreach (var fs in l)
            {
                if (Map.ContainsKey(fs.Name))
                {
                    Map[fs.Name].Add(fs);
                }
                else
                {
                    Map.Add(fs.Name, new List<FunctionSignature> { fs });
                }
            }
        }
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

        public TypeBinderResult Bind(Func<String, PrimitiveType> VariableTypeProvider, PrimitiveType ReturnType, TextRange RangeInLine)
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

        private void BindExpr(Func<String, PrimitiveType> VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, Expr e)
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
                PrimitiveType t;
                try
                {
                    t = VariableTypeProvider(e.Variable.Name);
                }
                catch (Exception ex)
                {
                    throw new InvalidSyntaxException(String.Format("'{0}' : VariableNotExist", e.Variable.Name), new FileTextRange { Text = Text, Range = Positions[e] }, ex);
                }
                TypeDict.Add(e, t);
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

        private PrimitiveType BindFunctionExpr(Func<String, PrimitiveType> VariableTypeProvider, Dictionary<Expr, PrimitiveType> TypeDict, FunctionExpr fe)
        {
            foreach (var p in fe.Parameters)
            {
                BindExpr(VariableTypeProvider, TypeDict, p);
            }
            var ParameterTypes = fe.Parameters.Select(p => TypeDict[p]).ToArray();
            if (!FunctionSignatureMap.Map.ContainsKey(fe.Name))
            {
                throw new InvalidSyntaxException("'{0}' : FunctionNotExist".Formats(fe.Name), new FileTextRange { Text = Text, Range = Positions[fe] });
            }
            var sl = FunctionSignatureMap.Map[fe.Name];
            var Sorted = sl.Where(fs => IsOverloadSatisfied(ParameterTypes, fs)).GroupBy(fs => GetOverloadMatchPoint(ParameterTypes, fs)).OrderBy(g => g.Key).ToArray();
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
                var fspt = MostMatchedSignature.ParameterTypes[k];
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
            return MostMatchedSignature.ReturnType;
        }

        private static bool IsOverloadSatisfied(PrimitiveType[] ParameterTypes, FunctionSignature fs)
        {
            if (ParameterTypes.Length != fs.ParameterTypes.Length) { return false; }
            for (int k = 0; k < ParameterTypes.Length; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = fs.ParameterTypes[k];
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
        private static int GetOverloadMatchPoint(PrimitiveType[] ParameterTypes, FunctionSignature fs)
        {
            var Point = 0;
            for (int k = 0; k < ParameterTypes.Length; k += 1)
            {
                var pt = ParameterTypes[k];
                var fspt = fs.ParameterTypes[k];
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
