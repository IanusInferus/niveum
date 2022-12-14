﻿//==========================================================================
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

namespace Niveum.ObjectSchema.Haxe
{
    partial class Templates
    {
        public readonly List<String> Keywords = new List<String> {"abstract", "break", "callback", "case", "cast", "catch", "class", "continue", "default", "do", "dynamic", "else", "enum", "extends", "extern", "false", "for", "function", "here", "if", "implements", "import", "in", "inline", "interface", "never", "new", "null", "override", "package", "private", "public", "return", "static", "super", "switch", "this", "throw", "trace", "true", "try", "typedef", "untyped", "using", "var", "while", "Dynamic"};
        public readonly Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String> {{"Unit", "Unit"}, {"Boolean", "Bool"}, {"String", "String"}, {"Int", "Int"}, {"Real", "Float"}, {"Byte", "Int"}, {"UInt8", "Int"}, {"UInt16", "Int"}, {"UInt32", "Int"}, {"UInt64", "haxe.Int64"}, {"Int8", "Int"}, {"Int16", "Int"}, {"Int32", "Int"}, {"Int64", "haxe.Int64"}, {"Float32", "Float"}, {"Float64", "Float"}, {"Type", "Error"}, {"Optional", "Null"}, {"List", "Array"}, {"Set", "Set"}, {"Map", "Map"}};
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
        public IEnumerable<String> SingleLineXmlComment(String Description)
        {
            foreach (var _Line in Combine(Combine(Combine(Begin(), "/** "), Description), " */"))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> MultiLineXmlComment(List<String> Description)
        {
            yield return "/**";
            foreach (var _Line in Combine(Combine(Begin(), "  * "), Description))
            {
                yield return _Line;
            }
            yield return "  * */";
        }
        public IEnumerable<String> Primitive(String Name, String PlatformName)
        {
            foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), "typedef "), GetEscapedIdentifier(Name)), " = "), PlatformName))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Primitive_Unit()
        {
            yield return "typedef Unit = {}";
        }
        public IEnumerable<String> Primitive_Set()
        {
            yield return "typedef Set<T> = Map<T, Unit>";
        }
        public IEnumerable<String> Alias(AliasDef a)
        {
            var Name = GetEscapedIdentifier(a.DefinitionName()) + GetGenericParameters(a.GenericParameters);
            var Type = GetTypeString(a.Type, a.NamespaceName());
            foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), "typedef "), Name), " = "), Type))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Record(RecordDef r)
        {
            var Name = GetEscapedIdentifier(r.DefinitionName()) + GetGenericParameters(r.GenericParameters);
            foreach (var _Line in Combine(Begin(), GetXmlComment(r.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Begin(), "typedef "), Name), " ="))
            {
                yield return _Line;
            }
            yield return "{";
            foreach (var f in r.Fields)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(f.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "var "), GetEscapedIdentifier(LowercaseCamelize(f.Name))), " : "), GetTypeString(f.Type, r.NamespaceName())), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
            }
            yield return "}";
        }
        public IEnumerable<String> TaggedUnion(TaggedUnionDef tu)
        {
            var Name = GetEscapedIdentifier(tu.DefinitionName()) + GetGenericParameters(tu.GenericParameters);
            foreach (var _Line in Combine(Begin(), GetXmlComment(tu.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Begin(), "enum "), Name))
            {
                yield return _Line;
            }
            yield return "{";
            foreach (var a in tu.Alternatives)
            {
                if (a.Type.OnTypeRef && a.Type.TypeRef.NameMatches("Unit"))
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Begin(), GetEscapedIdentifier(LowercaseCamelize(a.Name))), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                else
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), GetEscapedIdentifier(LowercaseCamelize(a.Name))), "(v : "), GetTypeString(a.Type, tu.NamespaceName())), ");"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "}";
        }
        public IEnumerable<String> Enum(EnumDef e)
        {
            var Name = GetEscapedIdentifier(e.DefinitionName());
            foreach (var _Line in Combine(Begin(), GetXmlComment(e.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "class "), Name), " /* "), GetEnumTypeString(e.UnderlyingType, e.NamespaceName())), " */"))
            {
                yield return _Line;
            }
            yield return "{";
            var k = 0;
            foreach (var l in e.Literals)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(l.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "public static inline var "), GetEscapedIdentifier(l.Name.ToUpperInvariant())), " : "), GetTypeString(e.UnderlyingType, e.NamespaceName())), " = "), l.Value), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                k += 1;
            }
            yield return "}";
        }
        public IEnumerable<String> Tuple(TypeSpec tp, String NamespaceName)
        {
            var Name = GetEscapedIdentifier(tp.SimpleName(NamespaceName));
            var Types = tp.Tuple;
            yield return "/* Tuple */";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "typedef "), Name), " ="))
            {
                yield return _Line;
            }
            yield return "{";
            var k = 0;
            foreach (var e in Types)
            {
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "var "), GetEscapedIdentifier(Combine(Combine(Begin(), "item"), k))), " : "), GetTypeString(e, NamespaceName)), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                k += 1;
            }
            yield return "}";
        }
        public IEnumerable<String> IApplicationClient(List<TypeDef> Commands, String NamespaceName)
        {
            yield return "interface IApplicationClient";
            yield return "{";
            yield return "    var hash(get, null) : String;";
            yield return "    function dequeueCallback(commandName : String) : Void;";
            yield return "";
            foreach (var c in Commands)
            {
                if (c.OnClientCommand)
                {
                    var Name = c.ClientCommand.GetTypeSpec().SimpleName(NamespaceName);
                    var Description = c.ClientCommand.Description;
                    var RequestTypeString = GetSuffixedTypeString(c.ClientCommand.Name, c.ClientCommand.Version, "Request", NamespaceName);
                    var ReplyTypeString = GetSuffixedTypeString(c.ClientCommand.Name, c.ClientCommand.Version, "Reply", NamespaceName);
                    foreach (var _Line in Combine(Begin(), GetXmlComment(Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "function "), GetEscapedIdentifier(LowercaseCamelize(Name))), "(r : "), RequestTypeString), ", _callback : "), ReplyTypeString), " -> Void) : Void;"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                else if (c.OnServerCommand)
                {
                    var Name = c.ServerCommand.GetTypeSpec().SimpleName(NamespaceName);
                    var Description = c.ServerCommand.Description;
                    var EventTypeString = GetSuffixedTypeString(c.ServerCommand.Name, c.ServerCommand.Version, "Event", NamespaceName);
                    foreach (var _Line in Combine(Begin(), GetXmlComment(Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "var "), GetEscapedIdentifier(LowercaseCamelize(Name))), " : "), EventTypeString), " -> Void;"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "}";
        }
        public IEnumerable<String> WrapModule(String NamespaceName, List<String> Imports, IEnumerable<String> Contents)
        {
            yield return "//==========================================================================";
            yield return "//";
            yield return "//  Notice:      This file is automatically generated.";
            yield return "//               Please don't modify this file.";
            yield return "//";
            yield return "//==========================================================================";
            yield return "";
            if (NamespaceName != "")
            {
                var n = String.Join(".", NamespaceName.Split('.').Select(NamespacePart => LowercaseCamelize(NamespacePart)));
                foreach (var _Line in Combine(Combine(Combine(Begin(), "package "), n), ";"))
                {
                    yield return _Line;
                }
                yield return "";
            }
            if (Imports.Count > 0)
            {
                foreach (var _Line in Combine(Combine(Combine(Begin(), "import "), Imports), ";"))
                {
                    yield return _Line;
                }
                yield return "";
            }
            foreach (var _Line in Combine(Begin(), Contents))
            {
                yield return _Line;
            }
            yield return "";
        }
    }
}
