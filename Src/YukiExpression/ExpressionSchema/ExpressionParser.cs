//==========================================================================
//
//  File:        ExpressionParser.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式解析器
//  Version:     2013.03.12.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;

namespace Yuki.ExpressionSchema
{
    public class ExpressionParserDeclarationResult
    {
        public FunctionDecl Declaration;
        public Dictionary<Object, TextRange> Positions;
    }

    public class ExpressionParserExprResult
    {
        public Expr Body;
        public Dictionary<Expr, PrimitiveType> TypeDict;
        public Dictionary<Object, TextRange> Positions;
    }

    public class ExpressionParserResult
    {
        public FunctionDef Definition;
        public Dictionary<Expr, PrimitiveType> TypeDict;
        public Dictionary<Object, TextRange> Positions;
    }

    public class ExpressionParser
    {
        private Text SignatureText = null;
        private Text BodyText = null;
        public ExpressionParser(Text SignatureText, Text BodyText)
        {
            this.SignatureText = SignatureText;
            this.BodyText = BodyText;
        }

        public static ExpressionParserDeclarationResult ParseSignature(String Signature)
        {
            var LinesSignature = new List<TextLine>();
            LinesSignature.Add(new TextLine { Text = Signature, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Signature.Length, Row = 1, Column = Signature.Length + 1 } } });
            var tSignature = new Text { Path = "Signature", Lines = LinesSignature.ToArray() };
            var p = new Parser(tSignature, null);
            return p.ParseDeclaration(LinesSignature.Single().Range);
        }
        public static ExpressionParserExprResult ParseBody(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, String Body)
        {
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tBody = new Text { Path = "Body", Lines = LinesBody.ToArray() };
            var p = new Parser(null, tBody);
            return p.ParseBody(VariableTypeProvider, Declaration, LinesBody.Single().Range);
        }
        public static ExpressionParserExprResult ParseExpr(IVariableTypeProvider VariableTypeProvider, String Body)
        {
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tBody = new Text { Path = "Body", Lines = LinesBody.ToArray() };
            var p = new Parser(null, tBody);
            return p.ParseExpr(VariableTypeProvider, LinesBody.Single().Range);
        }
        public static ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, String Signature, String Body)
        {
            var LinesSignature = new List<TextLine>();
            LinesSignature.Add(new TextLine { Text = Signature, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Signature.Length, Row = 1, Column = Signature.Length + 1 } } });
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tSignature = new Text { Path = "Signature", Lines = LinesSignature.ToArray() };
            var tBody = new Text { Path = "Body", Lines = LinesBody.ToArray() };
            var p = new Parser(tSignature, tBody);
            return p.ParseFunction(VariableTypeProvider, LinesSignature.Single().Range, LinesBody.Single().Range);
        }

        public ExpressionParserDeclarationResult ParseDeclaration(TextRange Signature)
        {
            var p = new Parser(SignatureText, BodyText);
            return p.ParseDeclaration(Signature);
        }
        public ExpressionParserExprResult ParseBody(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
        {
            var p = new Parser(SignatureText, BodyText);
            return p.ParseBody(VariableTypeProvider, Declaration, Body);
        }
        public ExpressionParserExprResult ParseExpr(IVariableTypeProvider VariableTypeProvider, TextRange Body)
        {
            var p = new Parser(SignatureText, BodyText);
            return p.ParseExpr(VariableTypeProvider, Body);
        }
        public ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
        {
            var p = new Parser(SignatureText, BodyText);
            return p.ParseFunction(VariableTypeProvider, Declaration, Body);
        }
        public ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, TextRange Signature, TextRange Body)
        {
            var p = new Parser(SignatureText, BodyText);
            return p.ParseFunction(VariableTypeProvider, Signature, Body);
        }

        private class Parser
        {
            private Text SignatureText;
            private Text BodyText;
            private Dictionary<Object, TextRange> Positions;
            private TypeBinder tb;

            public Parser(Text SignatureText, Text BodyText)
            {
                this.SignatureText = SignatureText;
                this.BodyText = BodyText;
                this.Positions = new Dictionary<Object, TextRange>();
                this.tb = new TypeBinder(BodyText, Positions);
            }

            private static Regex rSignature = new Regex(@"^ *(?<Name>[A-Za-z_][A-Za-z0-9_]*) *\((?<ParameterList>.*?)\) *: *(?<ReturnType>[A-Za-z_][A-Za-z0-9_]*) *$", RegexOptions.ExplicitCapture);
            private static Regex rEmptyParameterList = new Regex(@"^ *$", RegexOptions.ExplicitCapture);
            private static Regex rVariable = new Regex(@"^ *(?<Name>[A-Za-z_][A-Za-z0-9_]*) *: *(?<Type>[A-Za-z_][A-Za-z0-9_]*) *$", RegexOptions.ExplicitCapture);
            public ExpressionParserDeclarationResult ParseDeclaration(TextRange Signature)
            {
                var m = rSignature.Match(SignatureText.GetTextInLine(Signature));
                if (!m.Success) { throw new InvalidSyntaxException("SignatureInvalid: " + Signature, new FileTextRange { Text = SignatureText, Range = Signature }); }
                var Name = m.Result("${Name}");
                var ParameterList = m.Result("${ParameterList}");
                var ReturnType = m.Result("${ReturnType}");
                var rt = ParseType(ReturnType, new FileTextRange { Text = SignatureText, Range = Signature });

                List<VariableDef> Parameters;
                if (rEmptyParameterList.Match(ParameterList).Success)
                {
                    Parameters = new List<VariableDef>();
                }
                else
                {
                    Parameters = ParameterList.Split(',').Select(p => ParseVariable(p, Signature)).ToList();
                }
                var dParameters = Parameters.ToDictionary(p => p.Name, p => p.Type);

                var fd = new FunctionDecl
                {
                    Name = Name,
                    Parameters = Parameters,
                    ReturnValue = rt
                };

                var epr = new ExpressionParserDeclarationResult
                {
                    Declaration = fd,
                    Positions = Positions
                };
                return epr;
            }
            public ExpressionParserExprResult ParseBody(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
            {
                var d = Declaration;
                var dParameters = d.Parameters.ToDictionary(p => p.Name, p => p.Type);
                var vtp = new VariableTypeProviderCombiner(new SimpleVariableTypeProvider(dParameters), VariableTypeProvider);
                var br = BindExpr(vtp, d.ReturnValue, Body);

                var epr = new ExpressionParserExprResult
                {
                    Body = br.Semantics,
                    TypeDict = br.TypeDict,
                    Positions = Positions
                };
                return epr;
            }
            public ExpressionParserExprResult ParseExpr(IVariableTypeProvider VariableTypeProvider, TextRange Body)
            {
                var br = BindExpr(VariableTypeProvider, Body);

                var epr = new ExpressionParserExprResult
                {
                    Body = br.Semantics,
                    TypeDict = br.TypeDict,
                    Positions = Positions
                };
                return epr;
            }
            public ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
            {
                var d = Declaration;
                var epbr = ParseBody(VariableTypeProvider, d, Body);

                var fd = new FunctionDef
                {
                    Name = d.Name,
                    Parameters = d.Parameters,
                    ReturnValue = d.ReturnValue,
                    Body = epbr.Body
                };

                var epr = new ExpressionParserResult
                {
                    Definition = fd,
                    TypeDict = epbr.TypeDict,
                    Positions = Positions
                };
                return epr;
            }
            public ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, TextRange Signature, TextRange Body)
            {
                var epdr = ParseDeclaration(Signature);
                var d = epdr.Declaration;
                var epbr = ParseBody(VariableTypeProvider, d, Body);

                var fd = new FunctionDef
                {
                    Name = d.Name,
                    Parameters = d.Parameters,
                    ReturnValue = d.ReturnValue,
                    Body = epbr.Body
                };

                var epr = new ExpressionParserResult
                {
                    Definition = fd,
                    TypeDict = epbr.TypeDict,
                    Positions = Positions
                };
                return epr;
            }

            private VariableDef ParseVariable(String e, TextRange Signature)
            {
                var m = rVariable.Match(e);
                if (!m.Success) { throw new InvalidSyntaxException("VariableInvalid: " + e, new FileTextRange { Text = SignatureText, Range = Signature }); }
                var Name = m.Result("${Name}");
                var Type = m.Result("${Type}");
                return new VariableDef { Name = Name, Type = ParseType(Type, new FileTextRange { Text = SignatureText, Range = Signature }) };
            }

            private PrimitiveType ParseType(String e, FileTextRange r)
            {
                if (e.Equals("Boolean", StringComparison.Ordinal)) { return PrimitiveType.Boolean; }
                if (e.Equals("Int", StringComparison.Ordinal)) { return PrimitiveType.Int; }
                if (e.Equals("Real", StringComparison.Ordinal)) { return PrimitiveType.Real; }
                throw new InvalidSyntaxException("TypeInvalid: " + e, r);
            }

            private TypeBinderResult BindExpr(IVariableTypeProvider VariableTypeProvider, PrimitiveType ReturnType, TextRange RangeInLine)
            {
                var r = tb.Bind(VariableTypeProvider, ReturnType, RangeInLine);
                return r;
            }
            private TypeBinderResult BindExpr(IVariableTypeProvider VariableTypeProvider, TextRange RangeInLine)
            {
                var r = tb.Bind(VariableTypeProvider, RangeInLine);
                return r;
            }
        }
    }
}
