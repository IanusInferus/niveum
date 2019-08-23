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

namespace Niveum.ObjectSchema.Cpp
{
    partial class Templates
    {
        public readonly List<String> Keywords = new List<String> {"__abstract", "__alignof", "__asm", "__assume", "__based", "__box", "__cdecl", "__declspec", "__delegate", "__event", "__except", "__fastcall", "__finally", "__forceinline", "__gc", "__hook", "__identifier", "__if_exists", "__if_not_exists", "__inline", "__int16", "__int32", "__int64", "__int8", "__interface", "__leave", "__m128", "__m128d", "__m128i", "__m64", "__multiple_inheritance", "__nogc", "__noop", "__pin", "__property", "__raise", "__sealed", "__single_inheritance", "__stdcall", "__super", "__thiscall", "__try", "__except", "__finally", "__try_cast", "__unaligned", "__unhook", "__uuidof", "__value", "__virtual_inheritance", "__w64", "__wchar_t", "wchar_t", "abstract", "array", "auto", "bool", "break", "case", "catch", "char", "class", "const", "const_cast", "continue", "decltype", "default", "delegate", "delete", "deprecated", "dllexport", "dllimport", "do", "double", "dynamic_cast", "else", "enum", "event", "explicit", "extern", "false", "finally", "float", "for", "each", "in", "friend", "friend_as", "gcnew", "generic", "goto", "if", "initonly", "inline", "int", "interface", "interior_ptr", "literal", "long", "mutable", "naked", "namespace", "new", "noinline", "noreturn", "nothrow", "novtable", "nullptr", "operator", "private", "property", "protected", "public", "ref", "register", "reinterpret_cast", "return", "safecast", "sealed", "selectany", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct", "switch", "template", "this", "thread", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned", "using", "uuid", "value", "virtual", "void", "volatile", "while"};
        public readonly Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String> {{"Unit", "Unit"}, {"Boolean", "bool"}, {"String", "std::u16string"}, {"Int", "std::int32_t"}, {"Real", "double"}, {"Byte", "std::uint8_t"}, {"UInt8", "std::uint8_t"}, {"UInt16", "std::uint16_t"}, {"UInt32", "std::uint32_t"}, {"UInt64", "std::uint64_t"}, {"Int8", "std::int8_t"}, {"Int16", "std::int16_t"}, {"Int32", "std::int32_t"}, {"Int64", "std::int64_t"}, {"Float32", "float"}, {"Float64", "double"}, {"Type", "std::u16string"}, {"Optional", "std::optional"}, {"List", "std::vector"}, {"Set", "std::unordered_set"}, {"Map", "std::unordered_map"}};
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
            foreach (var _Line in Combine(Combine(Combine(Begin(), "/// <summary>"), Description), "</summary>"))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> MultiLineXmlComment(List<String> Description)
        {
            yield return "/// <summary>";
            foreach (var _Line in Combine(Combine(Begin(), "/// "), Description))
            {
                yield return _Line;
            }
            yield return "/// </summary>";
        }
        public IEnumerable<String> Primitive(String Name, String PlatformName)
        {
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "typedef "), PlatformName), " "), GetEscapedIdentifier(Name)), ";"))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Primitive_Unit()
        {
            yield return "#ifndef _UNIT_TYPE_";
            yield return "    typedef struct {} Unit;";
            yield return "#   define _UNIT_TYPE_";
            yield return "#endif";
        }
        public IEnumerable<String> TypePredefinition(String Name, String MetaType, List<VariableDef> GenericParameters)
        {
            foreach (var _Line in Combine(Begin(), GetGenericParameterLine(GenericParameters)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), MetaType), " "), GetEscapedIdentifier(Name)), ";"))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> Alias(AliasDef a)
        {
            var Name = GetEscapedIdentifier(a.DefinitionName()) + GetGenericParameters(a.GenericParameters);
            var Type = GetTypeString(a.Type, a.NamespaceName());
            foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
            {
                yield return _Line;
            }
            yield return "/* Alias */";
            foreach (var _Line in Combine(Begin(), GetGenericParameterLine(a.GenericParameters)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), " final"))
            {
                yield return _Line;
            }
            yield return "{";
            yield return "public:";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "    "), Type), " Value;"))
            {
                yield return _Line;
            }
            yield return "";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "    "), Name), "()"))
            {
                yield return _Line;
            }
            yield return "    {";
            yield return "    }";
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    "), Name), "(const "), Type), " &v)"))
            {
                yield return _Line;
            }
            yield return "        : Value(v)";
            yield return "    {";
            yield return "    }";
            yield return "";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "    operator const "), Type), " &() const"))
            {
                yield return _Line;
            }
            yield return "    {";
            yield return "        return Value;";
            yield return "    }";
            yield return "};";
        }
        public IEnumerable<String> Record(RecordDef r)
        {
            var Name = GetEscapedIdentifier(r.DefinitionName());
            foreach (var _Line in Combine(Begin(), GetXmlComment(r.Description)))
            {
                yield return _Line;
            }
            yield return "/* Record */";
            foreach (var _Line in Combine(Begin(), GetGenericParameterLine(r.GenericParameters)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), " final"))
            {
                yield return _Line;
            }
            yield return "{";
            yield return "public:";
            foreach (var f in r.Fields)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(f.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), GetTypeString(f.Type, r.NamespaceName())), " "), GetEscapedIdentifier(f.Name)), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
            }
            yield return "};";
        }
        public IEnumerable<String> TaggedUnion(TaggedUnionDef tu)
        {
            var Name = GetEscapedIdentifier(tu.DefinitionName());
            var TagName = GetEscapedIdentifier(GetSuffixedTypeName(tu.Name, tu.Version, "Tag", tu.NamespaceName()));
            foreach (var _Line in Combine(Combine(Begin(), "enum class "), TagName))
            {
                yield return _Line;
            }
            yield return "{";
            var k = 0;
            foreach (var a in tu.Alternatives)
            {
                if (k == tu.Alternatives.Count - 1)
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(a.Name)), " = "), k))
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
                    foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), GetEscapedIdentifier(a.Name)), " = "), k), ","))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                k += 1;
            }
            yield return "};";
            foreach (var _Line in Combine(Begin(), GetXmlComment(tu.Description)))
            {
                yield return _Line;
            }
            yield return "/* TaggedUnion */";
            foreach (var _Line in Combine(Begin(), GetGenericParameterLine(tu.GenericParameters)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Begin(), "class "), Name), " final"))
            {
                yield return _Line;
            }
            yield return "{";
            yield return "public:";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "    /* Tag */ "), TagName), " _Tag;"))
            {
                yield return _Line;
            }
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), GetTypeString(a.Type, tu.NamespaceName())), " "), GetEscapedIdentifier(a.Name)), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
            }
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                if (a.Type.OnTypeRef && a.Type.TypeRef.NameMatches("Unit"))
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "static std::shared_ptr<class "), Name), "> "), GetEscapedIdentifier(Combine(Combine(Begin(), "Create"), a.Name))), "()"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    yield return "    " + "{";
                    foreach (var _Line in Combine(Combine(Combine(Begin(), "    auto r = std::make_shared<"), Name), ">();"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    r->_Tag = "), TagName), "::"), GetEscapedIdentifier(a.Name)), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Begin(), "    r->"), GetEscapedIdentifier(a.Name)), " = Unit();"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    yield return "    " + "    return r;";
                    yield return "    " + "}";
                }
                else
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "static std::shared_ptr<class "), Name), "> "), GetEscapedIdentifier(Combine(Combine(Begin(), "Create"), a.Name))), "("), GetTypeString(a.Type, tu.NamespaceName())), " Value)"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    yield return "    " + "{";
                    foreach (var _Line in Combine(Combine(Combine(Begin(), "    auto r = std::make_shared<"), Name), ">();"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    r->_Tag = "), TagName), "::"), GetEscapedIdentifier(a.Name)), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Begin(), "    r->"), GetEscapedIdentifier(a.Name)), " = Value;"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    yield return "    " + "    return r;";
                    yield return "    " + "}";
                }
            }
            yield return "";
            foreach (var a in tu.Alternatives)
            {
                foreach (var _Line in Combine(Begin(), GetXmlComment(a.Description)))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                foreach (var _Line in Combine(Combine(Combine(Begin(), "Boolean "), GetEscapedIdentifier(Combine(Combine(Begin(), "On"), a.Name))), "() const"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                yield return "    " + "{";
                foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    return _Tag == "), TagName), "::"), GetEscapedIdentifier(a.Name)), ";"))
                {
                    yield return _Line == "" ? "" : "    " + _Line;
                }
                yield return "    " + "}";
            }
            yield return "};";
        }
        public IEnumerable<String> Enum(EnumDef e)
        {
            var Name = GetEscapedIdentifier(e.DefinitionName());
            foreach (var _Line in Combine(Begin(), GetXmlComment(e.Description)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), "enum class "), Name), " : "), GetEnumTypeString(e.UnderlyingType, e.NamespaceName())))
            {
                yield return _Line;
            }
            yield return "{";
            var k = 0;
            foreach (var l in e.Literals)
            {
                if (k == e.Literals.Count - 1)
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(l.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Begin(), GetEscapedIdentifier(l.Name)), " = "), l.Value))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                else
                {
                    foreach (var _Line in Combine(Begin(), GetXmlComment(l.Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Begin(), GetEscapedIdentifier(l.Name)), " = "), l.Value), ","))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
                k += 1;
            }
            yield return "};";
        }
        public IEnumerable<String> EnumFunctor(EnumDef e)
        {
            var TypeString = GetTypeString(e.GetTypeSpec(), "std");
            yield return "template <>";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "struct hash<"), TypeString), ">"))
            {
                yield return _Line;
            }
            yield return "{";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "    size_t operator()(const "), TypeString), " &x) const"))
            {
                yield return _Line;
            }
            yield return "    {";
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "        return hash<"), GetTypeString(e.UnderlyingType, e.NamespaceName())), ">()(static_cast<"), GetTypeString(e.UnderlyingType, e.NamespaceName())), ">(x));"))
            {
                yield return _Line;
            }
            yield return "    }";
            yield return "};";
            yield return "template <>";
            foreach (var _Line in Combine(Combine(Combine(Begin(), "struct less<"), TypeString), ">"))
            {
                yield return _Line;
            }
            yield return "{";
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "    bool operator()(const "), TypeString), " &x, const "), TypeString), " &y) const"))
            {
                yield return _Line;
            }
            yield return "    {";
            foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "        return less<"), GetTypeString(e.UnderlyingType, "std")), ">()(static_cast<"), GetTypeString(e.UnderlyingType, "std")), ">(x), static_cast<"), GetTypeString(e.UnderlyingType, "std")), ">(y));"))
            {
                yield return _Line;
            }
            yield return "    }";
            yield return "};";
        }
        public IEnumerable<String> ClientCommand(ClientCommandDef c)
        {
            var RequestRef = GetSuffixedTypeRef(c.Name, c.Version, "Request");
            var ReplyRef = GetSuffixedTypeRef(c.Name, c.Version, "Reply");
            var Request = new RecordDef { Name = RequestRef.Name, Version = RequestRef.Version, GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description };
            var Reply = new TaggedUnionDef { Name = ReplyRef.Name, Version = ReplyRef.Version, GenericParameters = new List<VariableDef> { }, Alternatives = c.InParameters, Attributes = c.Attributes, Description = c.Description };
            foreach (var _Line in Combine(Begin(), Record(Request)))
            {
                yield return _Line;
            }
            foreach (var _Line in Combine(Begin(), TaggedUnion(Reply)))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> ServerCommand(ServerCommandDef c)
        {
            var EventRef = GetSuffixedTypeRef(c.Name, c.Version, "Event");
            var Event = new RecordDef { Name = EventRef.Name, Version = EventRef.Version, GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description };
            foreach (var _Line in Combine(Begin(), Record(Event)))
            {
                yield return _Line;
            }
        }
        public IEnumerable<String> IApplicationServer(List<TypeDef> Commands, String NamespaceName)
        {
            yield return "class IApplicationServer";
            yield return "{";
            yield return "public:";
            yield return "    virtual ~IApplicationServer() {}";
            yield return "";
            foreach (var c in Commands)
            {
                if (c.OnClientCommand)
                {
                    var Name = c.ClientCommand.GetTypeSpec().SimpleName(NamespaceName);
                    var Description = c.ClientCommand.Description;
                    var RequestTypeString = GetSuffixedTypeString(c.ClientCommand.Name, c.ClientCommand.Version, "Request", NamespaceName);
                    var ReplyTypeString = GetSuffixedTypeString(c.ClientCommand.Name, c.ClientCommand.Version, "Reply", NamespaceName);
                    if (c.ClientCommand.Attributes.Any(a => a.Key == "Async"))
                    {
                        foreach (var _Line in Combine(Begin(), GetXmlComment(Description)))
                        {
                            yield return _Line == "" ? "" : "    " + _Line;
                        }
                        foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "virtual void "), GetEscapedIdentifier(Name)), "("), RequestTypeString), " r, std::function<void("), ReplyTypeString), ")> Callback, std::function<void(const std::exception &)> OnFailure) = 0;"))
                        {
                            yield return _Line == "" ? "" : "    " + _Line;
                        }
                    }
                    else
                    {
                        foreach (var _Line in Combine(Begin(), GetXmlComment(Description)))
                        {
                            yield return _Line == "" ? "" : "    " + _Line;
                        }
                        foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "virtual "), ReplyTypeString), " "), GetEscapedIdentifier(Name)), "("), RequestTypeString), " r) = 0;"))
                        {
                            yield return _Line == "" ? "" : "    " + _Line;
                        }
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
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "std::function<void("), EventTypeString), ")> "), GetEscapedIdentifier(Name)), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "};";
        }
        public IEnumerable<String> IApplicationClient(List<TypeDef> Commands, String NamespaceName)
        {
            yield return "class IApplicationClient";
            yield return "{";
            yield return "public:";
            yield return "    virtual ~IApplicationClient() {}";
            yield return "";
            yield return "    virtual std::uint64_t Hash() = 0;";
            yield return "    virtual void DequeueCallback(std::u16string CommandName) = 0;";
            yield return "    virtual void NotifyErrorCommand(std::u16string CommandName, std::u16string Message) = 0;";
            yield return "    std::function<void(std::u16string CommandName, std::u16string Message)> GlobalErrorHandler;";
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
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Combine(Combine(Begin(), "virtual void "), GetEscapedIdentifier(Name)), "("), RequestTypeString), " r, std::function<void("), ReplyTypeString), ")> Callback, std::function<void(std::u16string)> OnError = nullptr) = 0;"))
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
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "std::function<void("), EventTypeString), ")> "), GetEscapedIdentifier(Name)), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "};";
        }
        public IEnumerable<String> IEventPump(List<TypeDef> Commands, String NamespaceName)
        {
            yield return "class IEventPump";
            yield return "{";
            yield return "public:";
            yield return "    virtual ~IEventPump() {}";
            yield return "";
            foreach (var c in Commands)
            {
                if (c.OnServerCommand)
                {
                    if (c.ServerCommand.Version != "") { continue; }
                    var Name = c.ServerCommand.GetTypeSpec().SimpleName(NamespaceName);
                    var Description = c.ServerCommand.Description;
                    var EventTypeString = GetSuffixedTypeString(c.ServerCommand.Name, c.ServerCommand.Version, "Event", NamespaceName);
                    foreach (var _Line in Combine(Begin(), GetXmlComment(Description)))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                    foreach (var _Line in Combine(Combine(Combine(Combine(Combine(Begin(), "std::function<void("), EventTypeString), ")> "), GetEscapedIdentifier(Name)), ";"))
                    {
                        yield return _Line == "" ? "" : "    " + _Line;
                    }
                }
            }
            yield return "};";
        }
        public IEnumerable<String> WrapNamespacePart(String NamespacePart, IEnumerable<String> Contents)
        {
            foreach (var _Line in Combine(Combine(Begin(), "namespace "), GetEscapedIdentifier(NamespacePart)))
            {
                yield return _Line;
            }
            yield return "{";
            foreach (var _Line in Combine(Combine(Begin(), "    "), Contents))
            {
                yield return _Line;
            }
            yield return "}";
        }
        public IEnumerable<String> Main(Schema Schema, String NamespaceName)
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
            yield return "#include <cstddef>";
            yield return "#include <cstdint>";
            yield return "#include <string>";
            yield return "#include <optional>";
            yield return "#include <vector>";
            yield return "#include <unordered_set>";
            yield return "#include <unordered_map>";
            yield return "#include <tuple>";
            yield return "#include <memory>";
            yield return "#include <functional>";
            yield return "#include <exception>";
            yield return "#include <stdexcept>";
            foreach (var _Line in Combine(Combine(Begin(), "#include "), Schema.Imports.Where(i => IsInclude(i))))
            {
                yield return _Line;
            }
            yield return "";
            foreach (var _Line in Combine(Begin(), GetTypes(Schema, NamespaceName)))
            {
                yield return _Line;
            }
            yield return "";
        }
    }
}
