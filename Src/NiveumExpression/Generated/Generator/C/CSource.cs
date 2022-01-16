//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;
using UInt8 = System.Byte;
using UInt16 = System.UInt16;
using UInt32 = System.UInt32;
using UInt64 = System.UInt64;
using Int8 = System.SByte;
using Int16 = System.Int16;
using Int32 = System.Int32;
using Int64 = System.Int64;
using Float32 = System.Single;
using Float64 = System.Double;

namespace Niveum.ExpressionSchema.CSource
{
    partial class Templates
    {
        public readonly Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String> {{"Boolean", "bool"}, {"Int", "int"}, {"Real", "double"}};
        private IEnumerable<String> Begin()
        {
            yield return "";
        }
        private IEnumerable<String> Combine(IEnumerable<String> Left, String Right)
        {
            foreach (var vLeft in Left)
            {
                yield return vLeft + Right;
            }
        }
        private IEnumerable<String> Combine(IEnumerable<String> Left, Object Right)
        {
            foreach (var vLeft in Left)
            {
                yield return vLeft + Convert.ToString(Right, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        private IEnumerable<String> Combine(IEnumerable<String> Left, IEnumerable<String> Right)
        {
            foreach (var vLeft in Left)
            {
                foreach (var vRight in Right)
                {
                    yield return vLeft + vRight;
                }
            }
        }
        private IEnumerable<String> Combine<T>(IEnumerable<String> Left, IEnumerable<T> Right)
        {
            foreach (var vLeft in Left)
            {
                foreach (var vRight in Right)
                {
                    yield return vLeft + Convert.ToString(vRight, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        private IEnumerable<String> GetEscapedIdentifier(IEnumerable<String> IdentifierValues)
        {
            foreach (var Identifier in IdentifierValues)
            {
                yield return GetEscapedIdentifier(Identifier);
            }
        }
        public IEnumerable<String> GenerateHeader(Schema Schema, String NamespaceName)
        {
            yield return "//==========================================================================";
            yield return "//";
            yield return "//  Notice:      This file is automatically generated.";
            yield return "//               Please don't modify this file.";
            yield return "//";
            yield return "//==========================================================================";
            yield return "";
            yield return "#pragma once";
            yield return "";
            foreach (var _Line in Combine(Combine(Begin(), "#include "), Schema.Imports.Where(i => IsInclude(i))))
            {
                yield return _Line;
            }
            yield return "#include <stdbool.h>";
            yield return "";
            foreach (var m in Schema.Modules)
            {
                foreach (var f in m.Functions)
                {
                    var ParameterList = f.Parameters.Count == 0 ? "void" : String.Join(", ", f.Parameters.Select(p => PrimitiveMapping[p.Type.ToString()] + " " + GetEscapedIdentifier(p.Name)));
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Begin(), PrimitiveMapping[f.ReturnValue.ToString()]), " "), GetEscapedIdentifier(Combine(Combine(Combine(Combine(Combine(Begin(), NamespaceName), "_"), m.Name), "_"), f.Name))), "("), ParameterList), ");"))
                    {
                        yield return _Line;
                    }
                }
            }
            yield return "";
        }
        public IEnumerable<String> GenerateSource(Schema Schema, String NamespaceName, Niveum.ExpressionSchema.Assembly a)
        {
            yield return "//==========================================================================";
            yield return "//";
            yield return "//  Notice:      This file is automatically generated.";
            yield return "//               Please don't modify this file.";
            yield return "//";
            yield return "//==========================================================================";
            yield return "";
            yield return "#pragma once";
            yield return "";
            foreach (var _Line in Combine(Combine(Begin(), "#include "), Schema.Imports.Where(i => IsInclude(i))))
            {
                yield return _Line;
            }
            yield return "#include \"NiveumExpressionRuntime.h\"";
            yield return "#include <stdbool.h>";
            yield return "";
            foreach (var m in Schema.Modules)
            {
                foreach (var f in m.Functions)
                {
                    var ParameterList = f.Parameters.Count == 0 ? "void" : String.Join(", ", f.Parameters.Select(p => PrimitiveMapping[p.Type.ToString()] + " " + GetEscapedIdentifier(p.Name)));
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Begin(), PrimitiveMapping[f.ReturnValue.ToString()]), " "), GetEscapedIdentifier(Combine(Combine(Combine(Combine(Combine(Begin(), NamespaceName), "_"), m.Name), "_"), f.Name))), "("), ParameterList), ");"))
                    {
                        yield return _Line;
                    }
                }
            }

            foreach (var m in a.Modules)
            {
                foreach (var f in m.Functions)
                {
                    var ParameterList = f.Parameters.Count == 0 ? "void" : String.Join(", ", f.Parameters.Select(p => PrimitiveMapping[p.Type.ToString()] + " " + GetEscapedIdentifier(p.Name)));
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Begin(), PrimitiveMapping[f.ReturnValue.ToString()]), " "), GetEscapedIdentifier(Combine(Combine(Combine(Combine(Combine(Begin(), NamespaceName), "_"), m.Name), "_"), f.Name))), "("), ParameterList), ")"))
                    {
                        yield return _Line;
                    }
                    yield return "{";
                    foreach (var _Line in Combine(Combine(Begin(), "    "), BuildBody(f)))
                    {
                        yield return _Line;
                    }
                    yield return "}";
                }
            }
            yield return "";
        }
    }
}
