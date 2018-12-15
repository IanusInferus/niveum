//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

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

namespace Yuki.ObjectSchema.Python
{
    partial class Templates
    {
        public readonly List<String> Keywords = new List<String> {"False", "class", "finally", "is", "return", "None", "continue", "for", "lambda", "try", "True", "def", "from", "nonlocal", "while", "and", "del", "global", "not", "with", "as", "elif", "if", "or", "yield", "assert", "else", "import", "pass", "break", "except", "in", "raise"};
        public readonly Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String> {{"Unit", "None"}, {"Boolean", "bool"}, {"String", "str"}, {"Int", "int"}, {"Real", "float"}, {"Byte", "int"}, {"UInt8", "int"}, {"UInt16", "int"}, {"UInt32", "int"}, {"UInt64", "int"}, {"Int8", "int"}, {"Int16", "int"}, {"Int32", "int"}, {"Int64", "int"}, {"Float32", "float"}, {"Float64", "float"}, {"Type", "type"}, {"Optional", "Optional"}, {"List", "List"}, {"Set", "Set"}, {"Map", "Dict"}};
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
            foreach (var _Line in Combine(Combine(Combine(Begin(), "''' "), Description), " '''"))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> MultiLineXmlComment(List<String> Description)
        {
            yield return "'''";
            foreach (var _Line in Combine(Begin(), Description))
            {
                yield return _Line;
            }
            yield return "'''";
        }
        public IEnumerable<String> Primitive(String Name, String PlatformName)
        {
            foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(Name)), " = "), PlatformName))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Alias(AliasDef a)
        {
            var Name = GetEscapedIdentifier(a.TypeFriendlyName()) + GetGenericParameters(a.GenericParameters);
            var Type = GetTypeString(a.Type);
            foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Begin(), Name), " = "), Type))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Record(RecordDef r)
        {
            var Name = GetEscapedIdentifier(r.TypeFriendlyName()) + GetGenericParameters(r.GenericParameters);
            yield return "#Record";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), "(NamedTuple):"))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(r.Description)))
            {
                yield return _Line;
            }
            yield return "";
            foreach (var f in r.Fields)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(f.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(f.Name)), ": "), GetTypeString(f.Type)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
            }
            if (r.Fields.Count == 0)
            {
                yield return "    " + "pass";
            }
        }
        public IEnumerable<String> TaggedUnion(TaggedUnionDef tu)
        {
            var Name = GetEscapedIdentifier(tu.TypeFriendlyName()) + GetGenericParameters(tu.GenericParameters);
            var TagName = GetEscapedIdentifier(tu.TypeFriendlyName() + "Tag");
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), TagName), "(IntEnum):"))
            {
                yield return _Line;
            }
            var k = 0;
            foreach (var a in tu.Alternatives)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(a.Name)), " = "), k))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                k += 1;
            }
            if (tu.Alternatives.Count == 0)
            {
                yield return "    " + "pass";
            }
            yield return "#TaggedUnion";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), "(NamedTuple):"))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(tu.Description)))
            {
                yield return _Line;
            }
            yield return "";
            foreach (var _Line in Combine(Combine(Begin(), "    Tag_: "), TagName))
            {
                yield return _Line;
            }
            yield return "    Value_: Any";
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "def "), GetEscapedIdentifier(a.Name)), "(self) -> "), GetTypeString(a.Type)), ":"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(a.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    if self.Tag_ != "), TagName), "."), GetEscapedIdentifier(a.Name)), ":"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                yield return "    " + "        raise TypeError('TagMismatch')";
                yield return "    " + "    return self.Value_";
            }
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                if (a.Type.OnTypeRef && (a.Type.TypeRef.Name == "Unit") && (a.Type.TypeRef.Version == ""))
                {
                    yield return "    " + "@staticmethod";
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "def "), GetEscapedIdentifier(Combine(Combine(Begin(), "Create"), a.Name))), "() -> '"), Name), "':"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "    return "), Name), "("), TagName), "."), GetEscapedIdentifier(a.Name)), ", None)"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                else
                {
                    yield return "    " + "@staticmethod";
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "def "), GetEscapedIdentifier(Combine(Combine(Begin(), "Create"), a.Name))), "(Value: "), GetTypeString(a.Type)), ") -> '"), Name), "':"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "    return "), Name), "("), TagName), "."), GetEscapedIdentifier(a.Name)), ", Value)"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                foreach (var _Line in Combine(Combine(Combine(Begin(), "def "), GetEscapedIdentifier(Combine(Combine(Begin(), "On"), a.Name))), "(self) -> bool:"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(a.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), "    return self.Tag_ == "), TagName), "."), GetEscapedIdentifier(a.Name)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
            }
        }
        public IEnumerable<String> Enum(EnumDef e)
        {
            var Name = GetEscapedIdentifier(e.TypeFriendlyName());
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), "(IntFlag):"))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Begin(), "    "), GetXmlComment(e.Description)))
            {
                yield return _Line;
            }
            yield return "";
            var k = 0;
            foreach (var l in e.Literals)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(l.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(l.Name)), " = "), l.Value))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                k += 1;
            }
            if (e.Literals.Count == 0)
            {
                yield return "    " + "pass";
            }
        }
        public IEnumerable<String> Main(Schema Schema)
        {
            yield return "#!/usr/bin/python3";
            yield return "";
            yield return "#==========================================================================";
            yield return "#";
            yield return "#  Notice:      This file is automatically generated.";
            yield return "#               Please don't modify this file.";
            yield return "#";
            yield return "#==========================================================================";
            yield return "";
            yield return "from typing import Any";
            yield return "from typing import NamedTuple";
            yield return "from typing import Tuple";
            yield return "from typing import Optional";
            yield return "from typing import List";
            yield return "from typing import Set";
            yield return "from typing import Dict";
            yield return "from enum import IntEnum";
            yield return "from enum import IntFlag";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "from "), Schema.Imports), " import *"))
            {
                yield return _Line;
            }
            var Primitives = GetPrimitives(Schema);
            foreach (var _Line in Combine(Begin(), Primitives))
            {
                yield return _Line;
            }
            yield return "";
            var ComplexTypes = GetComplexTypes(Schema);
            foreach (var _Line in Combine(Begin(), ComplexTypes))
            {
                yield return _Line;
            }
            yield return "";
        }
    }
}
