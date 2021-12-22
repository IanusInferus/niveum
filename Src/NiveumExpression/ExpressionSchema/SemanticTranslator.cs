//==========================================================================
//
//  File:        SemanticTranslator.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 语法表达式到语义表达式转换器
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
    public sealed class SemanticTranslatorResult
    {
        public Expr Semantics { get; init; }
    }

    public class SemanticTranslator
    {
        private Text Text;
        private Dictionary<Object, TextRange> Positions;
        private SyntaxParser sp;

        public SemanticTranslator(Text Text, Dictionary<Object, TextRange> Positions)
        {
            this.Text = Text;
            this.Positions = Positions;
            this.sp = new SyntaxParser(Text, Positions);
        }

        public SemanticTranslatorResult Translate(TextRange RangeInLine)
        {
            var Syntax = sp.Parse(RangeInLine).Syntax;
            var e = TranslateExpr(Syntax);
            return new SemanticTranslatorResult { Semantics = e };
        }

        private Expr TranslateExpr(SyntaxExpr e)
        {
            Expr m;
            if (e.OnLiteral)
            {
                m = Expr.CreateLiteral(TranslateLiteral(e.Literal.Literal));
            }
            else if (e.OnFunction)
            {
                var Parameters = new LinkedList<Expr>();
                var p = e.Function.ParameterList;
                if (p.OnNull)
                {
                }
                else if (p.OnNonnull)
                {
                    var n = p.Nonnull.NonnullParameterList;
                    while (true)
                    {
                        if (n.OnSingle)
                        {
                            Parameters.AddFirst(TranslateExpr(n.Single.Expr));
                            break;
                        }
                        else if (n.OnMultiple)
                        {
                            Parameters.AddFirst(TranslateExpr(n.Multiple.Expr));
                            n = n.Multiple.NonnullParameterList;
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var Name = e.Function.Identifier.Name;
                if (Name == "if" && Parameters.Count == 3)
                {
                    var ie = new IfExpr { Condition = Parameters.First.Value, TruePart = Parameters.First.Next.Value, FalsePart = Parameters.First.Next.Next.Value };
                    Positions.Add(ie, Positions[e]);
                    m = Expr.CreateIf(ie);
                }
                else
                {
                    var fe = new FunctionExpr { Name = Name, Parameters = Parameters.ToList() };
                    Positions.Add(fe, Positions[e]);
                    m = Expr.CreateFunction(fe);
                }
            }
            else if (e.OnVariable)
            {
                var ve = new VariableExpr { Name = e.Variable.Identifier.Name };
                Positions.Add(ve, Positions[e]);
                m = Expr.CreateVariable(ve);
            }
            else if (e.OnParen)
            {
                return TranslateExpr(e.Paren.Expr);
            }
            else if (e.OnUnaryOperator)
            {
                var fe = new FunctionExpr { Name = e.UnaryOperator.UnaryOperator.Name, Parameters = new List<Expr> { TranslateExpr(e.UnaryOperator.Expr) } };
                Positions.Add(fe, Positions[e]);
                m = Expr.CreateFunction(fe);
            }
            else if (e.OnBinaryOperator)
            {
                var Name = e.BinaryOperator.BinaryOperator.Name;
                if (Name == "&&")
                {
                    var aae = new AndAlsoExpr { Left = TranslateExpr(e.BinaryOperator.Left), Right = TranslateExpr(e.BinaryOperator.Right) };
                    Positions.Add(aae, Positions[e]);
                    m = Expr.CreateAndAlso(aae);
                }
                else if (Name == "||")
                {
                    var oee = new OrElseExpr { Left = TranslateExpr(e.BinaryOperator.Left), Right = TranslateExpr(e.BinaryOperator.Right) };
                    Positions.Add(oee, Positions[e]);
                    m = Expr.CreateOrElse(oee);
                }
                else
                {
                    var fe = new FunctionExpr { Name = Name, Parameters = new List<Expr> { TranslateExpr(e.BinaryOperator.Left), TranslateExpr(e.BinaryOperator.Right) } };
                    Positions.Add(fe, Positions[e]);
                    m = Expr.CreateFunction(fe);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
            Positions.Add(m, Positions[e]);
            return m;
        }

        private LiteralExpr TranslateLiteral(TokenLiteral l)
        {
            LiteralExpr m;
            if (l.OnBooleanValue)
            {
                m = LiteralExpr.CreateBooleanValue(l.BooleanValue);
            }
            else if (l.OnIntValue)
            {
                m = LiteralExpr.CreateIntValue(l.IntValue);
            }
            else if (l.OnRealValue)
            {
                m = LiteralExpr.CreateRealValue(l.RealValue);
            }
            else
            {
                throw new InvalidOperationException();
            }
            Positions.Add(m, Positions[l]);
            return m;
        }
    }
}
