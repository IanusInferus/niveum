//==========================================================================
//
//  File:        ExpressionParser.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 表达式解析器
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat.Syntax;

namespace Niveum.ExpressionSchema
{
    public sealed class ExpressionParserDeclarationResult
    {
        public FunctionDecl Declaration { get; init; }
        public Dictionary<Object, TextRange> Positions { get; init; }
    }

    public sealed class ExpressionParserExprResult
    {
        public Expr Body { get; init; }
        public Dictionary<Expr, PrimitiveType> TypeDict { get; init; }
        public Dictionary<Object, TextRange> Positions { get; init; }
    }

    public sealed class ExpressionParserResult
    {
        public FunctionDef Definition { get; init; }
        public Dictionary<Expr, PrimitiveType> TypeDict { get; init; }
        public Dictionary<Object, TextRange> Positions { get; init; }
    }

    public static class ExpressionParser
    {
        public static ExpressionParserDeclarationResult ParseSignature(String Signature)
        {
            var LinesSignature = new List<TextLine>();
            LinesSignature.Add(new TextLine { Text = Signature, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Signature.Length, Row = 1, Column = Signature.Length + 1 } } });
            var tSignature = new Text { Path = "Signature", Lines = LinesSignature };
            var Positions = new Dictionary<Object, TextRange>();
            return Parser.ParseDeclaration(tSignature, LinesSignature.Single().Range, Positions);
        }
        public static ExpressionParserExprResult ParseBody(IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, String Body)
        {
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tBody = new Text { Path = "Body", Lines = LinesBody };
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(tBody, Positions);
            return Parser.ParseBody(tb, tBody, VariableTypeProvider, Declaration, LinesBody.Single().Range, Positions);
        }
        public static ExpressionParserExprResult ParseExpr(IVariableTypeProvider VariableTypeProvider, String Body)
        {
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tBody = new Text { Path = "Body", Lines = LinesBody };
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(tBody, Positions);
            return Parser.ParseExpr(tb, VariableTypeProvider, LinesBody.Single().Range, Positions);
        }
        public static ExpressionParserResult ParseFunction(IVariableTypeProvider VariableTypeProvider, String Signature, String Body)
        {
            var LinesSignature = new List<TextLine>();
            LinesSignature.Add(new TextLine { Text = Signature, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Signature.Length, Row = 1, Column = Signature.Length + 1 } } });
            var LinesBody = new List<TextLine>();
            LinesBody.Add(new TextLine { Text = Body, Range = new TextRange { Start = new TextPosition { CharIndex = 0, Row = 1, Column = 1 }, End = new TextPosition { CharIndex = Body.Length, Row = 1, Column = Body.Length + 1 } } });
            var tSignature = new Text { Path = "Signature", Lines = LinesSignature };
            var tBody = new Text { Path = "Body", Lines = LinesBody };
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(tBody, Positions);
            return Parser.ParseFunction(tb, tSignature, tBody, VariableTypeProvider, LinesSignature.Single().Range, LinesBody.Single().Range, Positions);
        }

        public static ExpressionParserDeclarationResult ParseDeclaration(Text SignatureText, TextRange Signature)
        {
            var Positions = new Dictionary<Object, TextRange>();
            return Parser.ParseDeclaration(SignatureText, Signature, Positions);
        }
        public static ExpressionParserExprResult ParseBody(Text BodyText, IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
        {
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(BodyText, Positions);
            return Parser.ParseBody(tb, BodyText, VariableTypeProvider, Declaration, Body, Positions);
        }
        public static ExpressionParserExprResult ParseExpr(Text BodyText, IVariableTypeProvider VariableTypeProvider, TextRange Body)
        {
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(BodyText, Positions);
            return Parser.ParseExpr(tb, VariableTypeProvider, Body, Positions);
        }
        public static ExpressionParserResult ParseFunction(Text BodyText, IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body)
        {
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(BodyText, Positions);
            return Parser.ParseFunction(tb, BodyText, VariableTypeProvider, Declaration, Body, Positions);
        }
        public static ExpressionParserResult ParseFunction(Text SignatureText, Text BodyText, IVariableTypeProvider VariableTypeProvider, TextRange Signature, TextRange Body)
        {
            var Positions = new Dictionary<Object, TextRange>();
            var tb = new TypeBinder(BodyText, Positions);
            return Parser.ParseFunction(tb, SignatureText, BodyText, VariableTypeProvider, Signature, Body, Positions);
        }

        private static class Parser
        {
            private static Regex rSignature = new Regex(@"^ *(?<Name>[A-Za-z_][A-Za-z0-9_]*) *\((?<ParameterList>.*?)\) *: *(?<ReturnType>[A-Za-z_][A-Za-z0-9_]*) *$", RegexOptions.ExplicitCapture);
            private static Regex rEmptyParameterList = new Regex(@"^ *$", RegexOptions.ExplicitCapture);
            private static Regex rVariable = new Regex(@"^ *(?<Name>[A-Za-z_][A-Za-z0-9_]*) *: *(?<Type>[A-Za-z_][A-Za-z0-9_]*) *$", RegexOptions.ExplicitCapture);
            public static ExpressionParserDeclarationResult ParseDeclaration(Text SignatureText, TextRange Signature, Dictionary<Object, TextRange> Positions)
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
                    Parameters = ParameterList.Split(',').Select(p => ParseVariable(SignatureText, p, Signature)).ToList();
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

            public static ExpressionParserExprResult ParseBody(TypeBinder tb, Text BodyText, IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body, Dictionary<Object, TextRange> Positions)
            {
                var d = Declaration;
                var dParameters = d.Parameters.ToDictionary(p => p.Name, p => p.Type);
                var vtp = new VariableTypeProviderCombiner(new SimpleVariableTypeProvider(dParameters), VariableTypeProvider);
                var br = BindExpr(tb, vtp, d.ReturnValue, Body);

                var epr = new ExpressionParserExprResult
                {
                    Body = br.Semantics,
                    TypeDict = br.TypeDict,
                    Positions = Positions
                };
                return epr;
            }
            public static ExpressionParserExprResult ParseExpr(TypeBinder tb, IVariableTypeProvider VariableTypeProvider, TextRange Body, Dictionary<Object, TextRange> Positions)
            {
                var br = BindExpr(tb, VariableTypeProvider, Body);

                var epr = new ExpressionParserExprResult
                {
                    Body = br.Semantics,
                    TypeDict = br.TypeDict,
                    Positions = Positions
                };
                return epr;
            }
            public static ExpressionParserResult ParseFunction(TypeBinder tb, Text BodyText, IVariableTypeProvider VariableTypeProvider, FunctionDecl Declaration, TextRange Body, Dictionary<Object, TextRange> Positions)
            {
                var d = Declaration;
                var epbr = ParseBody(tb, BodyText, VariableTypeProvider, d, Body, Positions);

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
            public static ExpressionParserResult ParseFunction(TypeBinder tb, Text SignatureText, Text BodyText, IVariableTypeProvider VariableTypeProvider, TextRange Signature, TextRange Body, Dictionary<Object, TextRange> Positions)
            {
                var epdr = ParseDeclaration(SignatureText, Signature, Positions);
                var d = epdr.Declaration;
                var epbr = ParseBody(tb, BodyText, VariableTypeProvider, d, Body, Positions);

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

            private static VariableDef ParseVariable(Text SignatureText, String e, TextRange Signature)
            {
                var m = rVariable.Match(e);
                if (!m.Success) { throw new InvalidSyntaxException("VariableInvalid: " + e, new FileTextRange { Text = SignatureText, Range = Signature }); }
                var Name = m.Result("${Name}");
                var Type = m.Result("${Type}");
                return new VariableDef { Name = Name, Type = ParseType(Type, new FileTextRange { Text = SignatureText, Range = Signature }) };
            }

            private static PrimitiveType ParseType(String e, FileTextRange r)
            {
                if (e.Equals("Boolean", StringComparison.Ordinal)) { return PrimitiveType.Boolean; }
                if (e.Equals("Int", StringComparison.Ordinal)) { return PrimitiveType.Int; }
                if (e.Equals("Real", StringComparison.Ordinal)) { return PrimitiveType.Real; }
                throw new InvalidSyntaxException("TypeInvalid: " + e, r);
            }

            private static TypeBinderResult BindExpr(TypeBinder tb, IVariableTypeProvider VariableTypeProvider, PrimitiveType ReturnType, TextRange RangeInLine)
            {
                var r = tb.Bind(VariableTypeProvider, ReturnType, RangeInLine);
                return r;
            }
            private static TypeBinderResult BindExpr(TypeBinder tb, IVariableTypeProvider VariableTypeProvider, TextRange RangeInLine)
            {
                var r = tb.Bind(VariableTypeProvider, RangeInLine);
                return r;
            }
        }
    }
}
