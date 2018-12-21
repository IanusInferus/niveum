//==========================================================================
//
//  File:        PlainObjectSchemaGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 简单对象类型结构生成器
//  Version:     2018.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Niveum.ObjectSchema;
using OS = Niveum.ObjectSchema;
using RS = Yuki.RelationSchema;

namespace Yuki.RelationSchema
{
    public static class PlainObjectSchemaGenerator
    {
        public static OS.Schema Generate(RS.Schema Schema, String NamespaceName, Boolean ForcePrimitive = true)
        {
            var s = (new Generator { Schema = Schema, NamespaceParts = NamespaceName.Split('.').ToList(), AdditionalTypeRefs = new List<OS.TypeDef> { } }).Generate(ForcePrimitive);
            return s;
        }

        private class Generator
        {
            public RS.Schema Schema;
            public List<String> NamespaceParts;
            public List<OS.TypeDef> AdditionalTypeRefs;
            private HashSet<String> PrimitiveNames;
                      
            public OS.Schema Generate(Boolean ForcePrimitive)
            {
                PrimitiveNames = new HashSet<String>(Schema.TypeRefs.Where(t => t.OnPrimitive).Select(t => t.Primitive.Name).Distinct());
                var TypeRefs = Schema.TypeRefs.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToList();
                var Types = Schema.Types.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToList();
                if ((ForcePrimitive || UnitUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("Unit")).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Unit" }, GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || ByteUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("Byte")).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Byte" }, GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if (ForcePrimitive && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("Int")).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Int" }, GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || ListUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("List")).Any())
                {
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "List" }, GenericParameters = new List<OS.VariableDef> { GenericParameter }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || TypeUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("Type")).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Type" }, GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || OptionalUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.NameMatches("Optional")).Any())
                {
                    var GenericParameters = new List<OS.VariableDef> { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Optional" }, GenericParameters = GenericParameters, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                return new OS.Schema { Types = Types, TypeRefs = TypeRefs, Imports = Schema.Imports };
            }

            private List<OS.TypeDef> TranslateTypeDef(RS.TypeDef t)
            {
                if (t.OnPrimitive)
                {
                    return new List<OS.TypeDef> { OS.TypeDef.CreatePrimitive(TranslatePrimitive(t.Primitive)) };
                }
                else if (t.OnEntity)
                {
                    return new List<OS.TypeDef> { OS.TypeDef.CreateRecord(TranslateRecord(t.Entity)) };
                }
                else if (t.OnEnum)
                {
                    return new List<OS.TypeDef> { OS.TypeDef.CreateEnum(TranslateEnum(t.Enum)) };
                }
                else if (t.OnQueryList)
                {
                    return new List<OS.TypeDef> { };
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private OS.PrimitiveDef TranslatePrimitive(RS.PrimitiveDef e)
            {
                if (e.Name.Equals("List", StringComparison.OrdinalIgnoreCase))
                {
                    TypeUsed = true;
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                    return new OS.PrimitiveDef { Name = new List<String> { "List" }, GenericParameters = new List<OS.VariableDef> { GenericParameter }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                }
                else if (e.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase))
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    var GenericParameters = new List<OS.VariableDef> { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } };
                    return new OS.PrimitiveDef { Name = new List<String> { "Optional" }, GenericParameters = GenericParameters, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                }
                else
                {
                    return new OS.PrimitiveDef { Name = new List<String> { e.Name }, GenericParameters = new List<OS.VariableDef> { }, Attributes = e.Attributes, Description = e.Description };
                }
            }

            private OS.LiteralDef TranslateLiteral(RS.LiteralDef l)
            {
                return new OS.LiteralDef { Name = l.Name, Value = l.Value, Attributes = l.Attributes, Description = l.Description };
            }
            private OS.EnumDef TranslateEnum(RS.EnumDef e)
            {
                return new OS.EnumDef { Name = NamespaceParts.Concat(new List<String> { e.Name }).ToList(), Version = "", UnderlyingType = TranslateTypeSpec(e.UnderlyingType), Literals = e.Literals.Select(l => TranslateLiteral(l)).ToList(), Attributes = e.Attributes, Description = e.Description };
            }

            private Boolean UnitUsed = false;
            private Boolean ByteUsed = false;
            private Boolean ListUsed = false;
            private Boolean TypeUsed = false;
            private Boolean OptionalUsed = false;
            private OS.TypeSpec TranslateTypeSpec(RS.TypeSpec t)
            {
                if (t.OnTypeRef)
                {
                    if (t.TypeRef.Value.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        ByteUsed = true;
                        ListUsed = true;
                        TypeUsed = true;
                        var tBinary = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "List" }, Version = "" });
                        var tByte = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Byte" }, Version = "" });
                        return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = tBinary, ParameterValues = new List<OS.TypeSpec> { tByte } });
                    }
                    else if (PrimitiveNames.Contains(t.TypeRef.Value))
                    {
                        return OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { t.TypeRef.Value }, Version = "" });
                    }
                    else
                    {
                        return OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = NamespaceParts.Concat(new List<String> { t.TypeRef.Value }).ToList(), Version = "" });
                    }
                }
                else if (t.OnOptional)
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    OptionalUsed = true;
                    var Underlying = TranslateTypeSpec(RS.TypeSpec.CreateTypeRef(t.Optional));
                    return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "Optional" }, Version = "" }), ParameterValues = new List<OS.TypeSpec> { Underlying } });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private OS.VariableDef TranslateField(RS.VariableDef f)
            {
                return new OS.VariableDef { Name = f.Name, Type = TranslateTypeSpec(f.Type), Attributes = f.Attributes, Description = f.Description };
            }
            private OS.RecordDef TranslateRecord(RS.EntityDef r)
            {
                var Fields = r.Fields.Where(f => f.Attribute.OnColumn).Select(f => TranslateField(f)).ToList();
                return new OS.RecordDef { Name = NamespaceParts.Concat(new List <String> { r.Name }).ToList(), Version = "", GenericParameters = new List<OS.VariableDef> { }, Fields = Fields, Attributes = r.Attributes, Description = r.Description };
            }
        }
    }
}
