﻿//==========================================================================
//
//  File:        EmbeddedCSharpGenerator.cs
//  Location:    Nivea <Visual C#>
//  Description: 嵌入C#代码生成器
//  Version:     2016.08.03.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Texting.TreeFormat.Semantics;
using Nivea.Template.Semantics;
using Nivea.Template.Syntax;

namespace Nivea.Generator
{
    public class EmbeddedCSharpGenerator
    {
        private CSharpType.Templates Templates = new CSharpType.Templates();

        public IEnumerable<String> Generate(File File)
        {
            yield return "//==========================================================================";
            yield return "//";
            yield return "//  Notice:      This file is automatically generated.";
            yield return "//               Please don't modify this file.";
            yield return "//";
            yield return "//==========================================================================";
            yield return "";
            yield return "using System;";
            yield return "using System.Collections.Generic;";
            foreach (var s in File.Sections)
            {
                if (s.OnImport)
                {
                    foreach (var Namespace in s.Import)
                    {
                        var NamespaceStr = String.Join(".", Namespace.Select(Part => GetEscapedIdentifier(Part)));
                        if ((NamespaceStr == "System") || (NamespaceStr == "System.Collections.Generic")) { continue; }
                        yield return "using " + NamespaceStr + ";";
                    }
                }
            }
            yield return "using Boolean = System.Boolean;";
            yield return "using String = System.String;";
            yield return "using Type = System.Type;";
            yield return "using Int = System.Int32;";
            yield return "using Real = System.Double;";
            yield return "using Byte = System.Byte;";
            yield return "using UInt8 = System.Byte;";
            yield return "using UInt16 = System.UInt16;";
            yield return "using UInt32 = System.UInt32;";
            yield return "using UInt64 = System.UInt64;";
            yield return "using Int8 = System.SByte;";
            yield return "using Int16 = System.Int16;";
            yield return "using Int32 = System.Int32;";
            yield return "using Int64 = System.Int64;";
            yield return "using Float32 = System.Single;";
            yield return "using Float64 = System.Double;";
            yield return "";

            var IndentSpaceCount = 0;
            Func<String> GetIndentSpace = () => new String(' ', IndentSpaceCount);

            var IsInNamespace = false;
            var IsInTemplatesClass = false;

            var CurrentNamespace = "";
            var NamespaceGenerated = new HashSet<String> { };

            foreach (var s in File.Sections)
            {
                if (s.OnNamespace)
                {
                    if (IsInTemplatesClass)
                    {
                        IsInTemplatesClass = false;
                        IndentSpaceCount -= 4;
                        yield return GetIndentSpace() + "}";
                    }
                    if (IsInNamespace)
                    {
                        IsInNamespace = false;
                        IndentSpaceCount -= 4;
                        yield return GetIndentSpace() + "}";
                    }
                    var Namespace = String.Join(".", s.Namespace.Select(Part => GetEscapedIdentifier(Part)));
                    yield return GetIndentSpace() + "namespace " + Namespace;
                    yield return GetIndentSpace() + "{";
                    IsInNamespace = true;
                    IndentSpaceCount += 4;
                    CurrentNamespace = Namespace;
                }
                else if (s.OnTemplate)
                {
                    if (!IsInTemplatesClass)
                    {
                        yield return GetIndentSpace() + "partial class Templates";
                        yield return GetIndentSpace() + "{";
                        IsInTemplatesClass = true;
                        IndentSpaceCount += 4;
                    }
                    if (!NamespaceGenerated.Contains(CurrentNamespace))
                    {
                        NamespaceGenerated.Add(CurrentNamespace);
                        yield return GetIndentSpace() + "private IEnumerable<String> Begin()";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    yield return \"\";";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> Combine(IEnumerable<String> Left, String Right)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var vLeft in Left)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        yield return vLeft + Right;";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> Combine(IEnumerable<String> Left, Object Right)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var vLeft in Left)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        yield return vLeft + Convert.ToString(Right, System.Globalization.CultureInfo.InvariantCulture);";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> Combine(IEnumerable<String> Left, IEnumerable<String> Right)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var vLeft in Left)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        foreach (var vRight in Right)";
                        yield return GetIndentSpace() + "        {";
                        yield return GetIndentSpace() + "            yield return vLeft + vRight;";
                        yield return GetIndentSpace() + "        }";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> Combine(IEnumerable<String> Left, IEnumerable<Object> Right)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var vLeft in Left)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        foreach (var vRight in Right)";
                        yield return GetIndentSpace() + "        {";
                        yield return GetIndentSpace() + "            yield return vLeft + Convert.ToString(vRight, System.Globalization.CultureInfo.InvariantCulture);";
                        yield return GetIndentSpace() + "        }";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> GetEscapedIdentifier(Object Identifier)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    yield return GetEscapedIdentifier(Convert.ToString(Identifier, System.Globalization.CultureInfo.InvariantCulture));";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> GetEscapedIdentifier(IEnumerable<String> Identifiers)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var v in Identifiers)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        yield return GetEscapedIdentifier(v);";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                        yield return GetIndentSpace() + "private IEnumerable<String> GetEscapedIdentifier(IEnumerable<Object> Identifiers)";
                        yield return GetIndentSpace() + "{";
                        yield return GetIndentSpace() + "    foreach (var v in Identifiers)";
                        yield return GetIndentSpace() + "    {";
                        yield return GetIndentSpace() + "        yield return GetEscapedIdentifier(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));";
                        yield return GetIndentSpace() + "    }";
                        yield return GetIndentSpace() + "}";
                    }
                    var t = s.Template;
                    yield return GetIndentSpace() + "public IEnumerable<String> " + GetEscapedIdentifier(t.Signature.Name) + "(" + String.Join(", ", t.Signature.Parameters.Select(p => GetTypeString(p.Type) + " " + GetEscapedIdentifier(p.Name))) + ")";
                    yield return GetIndentSpace() + "{";
                    IndentSpaceCount += 4;
                    bool AnyLineGenerated = false;
                    foreach (var Line in GetTemplateExprs(t.Body, 0))
                    {
                        yield return GetIndentSpace() + Line;
                        AnyLineGenerated = true;
                    }
                    if (!AnyLineGenerated)
                    {
                        yield return GetIndentSpace() + "yield break;";
                    }
                    IndentSpaceCount -= 4;
                    yield return GetIndentSpace() + "}";
                }
                else if (s.OnConstant)
                {
                    if (!IsInTemplatesClass)
                    {
                        yield return GetIndentSpace() + "partial class Templates";
                        yield return GetIndentSpace() + "{";
                        IsInTemplatesClass = true;
                        IndentSpaceCount += 4;
                    }
                    yield return GetIndentSpace() + "public readonly " + GetTypeString(s.Constant.Type) + " " + GetEscapedIdentifier(s.Constant.Name) + " = " + GetValueLiteral(s.Constant.Value, s.Constant.Type) + ";";
                }
                else if (s.OnType)
                {
                    if (IsInTemplatesClass)
                    {
                        IsInTemplatesClass = false;
                        IndentSpaceCount -= 4;
                        yield return GetIndentSpace() + "}";
                    }
                    var t = s.Type;
                    if (t.OnPrimitive)
                    {
                        if (t.Primitive.Name == "Unit")
                        {
                            foreach (var Line in Templates.Primitive_Unit())
                            {
                                yield return GetIndentSpace() + Line;
                            }
                        }
                        else if (t.Primitive.Name == "Optional")
                        {
                            foreach (var Line in Templates.Primitive_Optional())
                            {
                                yield return GetIndentSpace() + Line;
                            }
                        }
                    }
                    else if (t.OnAlias)
                    {
                        var a = t.Alias;
                        foreach (var Line in Templates.Alias(a.Name, a.Type, a.Description))
                        {
                            yield return GetIndentSpace() + Line;
                        }
                    }
                    else if (t.OnRecord)
                    {
                        var r = t.Record;
                        foreach (var Line in Templates.Record(r.Name, r.Fields, r.Description))
                        {
                            yield return GetIndentSpace() + Line;
                        }
                    }
                    else if (t.OnTaggedUnion)
                    {
                        var tu = t.TaggedUnion;
                        foreach (var Line in Templates.TaggedUnion(tu.Name, tu.Alternatives, tu.Description))
                        {
                            yield return GetIndentSpace() + Line;
                        }
                    }
                    else if (t.OnEnum)
                    {
                        var e = t.Enum;
                        foreach (var Line in Templates.Enum(e.Name, e.UnderlyingType, e.Literals, e.Description))
                        {
                            yield return GetIndentSpace() + Line;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    //TODO
                }
            }
            if (IsInTemplatesClass)
            {
                IsInTemplatesClass = false;
                IndentSpaceCount -= 4;
                yield return GetIndentSpace() + "}";
            }
            if (IsInNamespace)
            {
                IsInNamespace = false;
                IndentSpaceCount -= 4;
                yield return GetIndentSpace() + "}";
                CurrentNamespace = "";
            }
            yield return "";
        }

        private IEnumerable<String> GetTemplateExprs(List<TemplateExpr> Exprs, int IndentSpaceCount)
        {
            foreach (var te in Exprs)
            {
                if (te.OnLine)
                {
                    var Line = te.Line;
                    if (Line.All(s => s.OnLiteral))
                    {
                        yield return "yield return " + (IndentSpaceCount == 0 ? "" : "\"" + new String(' ', IndentSpaceCount) + "\" + ") + String.Join(" + ", GetTemplateSpans(Line)) + ";";
                    }
                    else
                    {
                        yield return "foreach (var _Line in " + GetCombines(GetTemplateSpans(Line)) + ")";
                        yield return "{";
                        yield return "    yield return " + (IndentSpaceCount == 0 ? "" : "_Line == \"\" ? \"\" : \"" + new String(' ', IndentSpaceCount) + "\" + ") + "_Line;";
                        yield return "}";
                    }
                }
                else if (te.OnIndentedExpr)
                {
                    var e = te.IndentedExpr;
                    if (e.Expr.OnEmbedded)
                    {
                        foreach (var Embedded in e.Expr.Embedded)
                        {
                            if (Embedded.OnSpan)
                            {
                                throw new InvalidOperationException();
                            }
                            else if (Embedded.OnLine)
                            {
                                yield return Embedded.Line;
                            }
                            else if (Embedded.OnIndentedExpr)
                            {
                                var ie = Embedded.IndentedExpr;
                                if (ie.Expr.OnYieldTemplate)
                                {
                                    var t = ie.Expr.YieldTemplate;
                                    foreach (var Line in GetTemplateExprs(t, IndentSpaceCount + e.IndentSpace))
                                    {
                                        yield return new String(' ', ie.IndentSpace) + Line;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
        private String GetCombines(IEnumerable<String> Parts)
        {
            var l = new LinkedList<String>();
            l.AddLast("Begin()");
            foreach (var Part in Parts)
            {
                l.AddFirst("Combine(");
                l.AddLast(", ");
                l.AddLast(Part);
                l.AddLast(")");
            }
            return String.Join("", l);
        }
        private IEnumerable<String> GetTemplateSpans(List<TemplateSpan> Spans)
        {
            if (Spans.Count == 0) { yield return "\"\""; }
            foreach (var s in Spans)
            {
                if (s.OnLiteral)
                {
                    yield return GetEscapedStringLiteral(s.Literal);
                }
                else if (s.OnIdentifier)
                {
                    if (s.Identifier.All(i => i.OnLiteral) || (s.Identifier.Count == 1))
                    {
                        yield return "GetEscapedIdentifier(" + String.Join(" + ", GetTemplateSpans(s.Identifier)) + ")";
                    }
                    else
                    {
                        yield return "GetEscapedIdentifier(" + GetCombines(GetTemplateSpans(s.Identifier)) + ")";
                    }
                }
                else if (s.OnExpr)
                {
                    var e = s.Expr;
                    if (e.OnEmbedded)
                    {
                        var Embedded = e.Embedded;
                        if (Embedded.Count == 1)
                        {
                            var One = Embedded.Single();
                            if (!One.OnSpan) { throw new InvalidOperationException(); }
                            yield return One.Span;
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private String GetEscapedIdentifier(String Identifier)
        {
            return Templates.GetEscapedIdentifier(Identifier);
        }
        private String GetTypeString(TypeSpec Type)
        {
            return Templates.GetTypeString(Type);
        }
        private String GetEscapedStringLiteral(String s)
        {
            return "\"" + new String(s.SelectMany(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c == '\r' ? "\\r" : c == '\n' ? "\\n" : new String(c, 1)).ToArray()) + "\"";
        }
        private String GetValueLiteral(Expr Value, TypeSpec Type)
        {
            if (Value.OnNull)
            {
                return "null";
            }
            else if (Value.OnDefault)
            {
                return "default(" + GetTypeString(Type) + ")";
            }
            else if (Value.OnPrimitiveLiteral)
            {
                var t = Value.PrimitiveLiteral.Type;
                var ov = Value.PrimitiveLiteral.Value;
                if (!t.OnTypeRef || t.TypeRef.Version != "") { throw new NotSupportedException(GetTypeString(t)); }
                var Name = t.TypeRef.Name;
                if (Name == "Unit")
                {
                    return "default(Unit)";
                }
                var v = ov.Value;
                if (Name == "Boolean")
                {
                    if (v == "False")
                    {
                        return "false";
                    }
                    else if (v == "True")
                    {
                        return "true";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (Name == "String")
                {
                    return GetEscapedStringLiteral(v);
                }
                else if (Name == "Int")
                {
                    var i = TokenParser.TryParseInt64Literal(v);
                    if (i.OnNotHasValue) { throw new InvalidOperationException("ValueExceedRange: " + v); }
                    if ((i.Value < Int32.MinValue) || (i.Value > Int32.MaxValue)) { throw new InvalidOperationException("ValueExceedRange: " + v); }
                    return i.Value.ToInvariantString();
                }
                else if (Name == "Real")
                {
                    var i = TokenParser.TryParseFloat64Literal(v);
                    if (i.OnNotHasValue) { throw new InvalidOperationException("ValueExceedRange: " + v); }
                    return i.Value.ToInvariantString();
                }
                else if (Name == "Byte")
                {
                    return TokenParser.TryParseUInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "UInt8")
                {
                    return TokenParser.TryParseUInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "UInt16")
                {
                    return TokenParser.TryParseUInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "UInt32")
                {
                    return TokenParser.TryParseUInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "UInt64")
                {
                    return TokenParser.TryParseUInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "Int8")
                {
                    return TokenParser.TryParseInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "Int16")
                {
                    return TokenParser.TryParseInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "Int32")
                {
                    return TokenParser.TryParseInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "Int64")
                {
                    return TokenParser.TryParseInt64Literal(v).Value.ToInvariantString();
                }
                else if (Name == "Float32")
                {
                    return TokenParser.TryParseFloat64Literal(v).Value.ToInvariantString() + "f";
                }
                else if (Name == "Float64")
                {
                    return TokenParser.TryParseFloat64Literal(v).Value.ToInvariantString();
                }
                else
                {
                    throw new NotSupportedException(GetTypeString(Type));
                }
            }
            else if (Value.OnRecordLiteral)
            {
                throw new NotSupportedException(GetTypeString(Type));
            }
            else if (Value.OnTaggedUnionLiteral)
            {
                var t = Value.TaggedUnionLiteral.Type.Value;
                if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && (t.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional") && (t.GenericTypeSpec.TypeSpec.TypeRef.Version == "") && (t.GenericTypeSpec.ParameterValues.Count == 1))
                {
                    var AlternativeType = t.GenericTypeSpec.ParameterValues.Single();
                    return GetTypeString(t) + "." + GetEscapedIdentifier("Create" + Value.TaggedUnionLiteral.Alternative) + "(" + (Value.TaggedUnionLiteral.Expr.OnHasValue ? GetValueLiteral(Value.TaggedUnionLiteral.Expr.Value, AlternativeType) : "") + ")";
                }
                throw new NotSupportedException(GetTypeString(Type));
            }
            else if (Value.OnEnumLiteral)
            {
                return GetTypeString(Value.EnumLiteral.Type.Value) + "." + GetEscapedIdentifier(Value.EnumLiteral.Name);
            }
            else if (Value.OnTupleLiteral)
            {
                throw new NotSupportedException(GetTypeString(Type));
            }
            else if (Value.OnListLiteral)
            {
                var t = Value.ListLiteral.Type.Value;
                if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && ((t.GenericTypeSpec.TypeSpec.TypeRef.Name == "List") || (t.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set")) && (t.GenericTypeSpec.TypeSpec.TypeRef.Version == "") && (t.GenericTypeSpec.ParameterValues.Count == 1))
                {
                    var ElementType = t.GenericTypeSpec.ParameterValues.Single();
                    var l = new List<String> { };
                    foreach (var v in Value.ListLiteral.Elements)
                    {
                        l.Add(GetValueLiteral(v, ElementType));
                    }
                    return "new " + GetTypeString(Type) + " {" + String.Join(", ", l) + "}";
                }
                else if (t.OnGenericTypeSpec && t.GenericTypeSpec.TypeSpec.OnTypeRef && (t.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map") && (t.GenericTypeSpec.TypeSpec.TypeRef.Version == "") && (t.GenericTypeSpec.ParameterValues.Count == 2))
                {
                    var KeyType = t.GenericTypeSpec.ParameterValues[0];
                    var ValueType = t.GenericTypeSpec.ParameterValues[1];
                    var l = new List<String> { };
                    foreach (var v in Value.ListLiteral.Elements)
                    {
                        var KeyStr = GetValueLiteral(v.TupleLiteral.Elements[0], KeyType);
                        var ValueStr = GetValueLiteral(v.TupleLiteral.Elements[1], ValueType);
                        l.Add("{" + KeyStr + ", " + ValueStr + "}");
                    }
                    return "new " + GetTypeString(Type) + " {" + String.Join(", ", l) + "}";
                }
                throw new NotSupportedException(GetTypeString(Type));
            }
            else if (Value.OnTypeLiteral)
            {
                return "typeof(" + GetTypeString(Value.TypeLiteral) + ")";
            }
            else
            {
                throw new NotSupportedException(GetTypeString(Type));
            }
        }
    }
}
