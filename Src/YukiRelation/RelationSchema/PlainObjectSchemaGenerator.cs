//==========================================================================
//
//  File:        PlainObjectSchemaGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 简单对象类型结构生成器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OS = Yuki.ObjectSchema;
using RS = Yuki.RelationSchema;

namespace Yuki.RelationSchema
{
    public static class PlainObjectSchemaGenerator
    {
        public static OS.Schema Generate(RS.Schema Schema, Boolean ForcePrimitive = true)
        {
            var s = (new Generator { Schema = Schema, AdditionalTypeRefs = new List<OS.TypeDef> { } }).Generate(ForcePrimitive);
            return s;
        }

        private class Generator
        {
            public RS.Schema Schema;
            public List<OS.TypeDef> AdditionalTypeRefs;

            public OS.Schema Generate(Boolean ForcePrimitive)
            {
                var TypeRefs = Schema.TypeRefs.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToList();
                var Types = Schema.Types.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToList();
                if ((ForcePrimitive || UnitUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Unit", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || ByteUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Byte", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Byte", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if (ForcePrimitive && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Int", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Int", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || ListUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("List", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "List", GenericParameters = new List<OS.VariableDef> { GenericParameter }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || TypeUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Type", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Type", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
                }
                if ((ForcePrimitive || OptionalUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameters = new List<OS.VariableDef> { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Optional", GenericParameters = GenericParameters, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }));
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
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                    return new OS.PrimitiveDef { Name = "List", GenericParameters = new List<OS.VariableDef> { GenericParameter }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                }
                else if (e.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase))
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    var GenericParameters = new List<OS.VariableDef> { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } };
                    return new OS.PrimitiveDef { Name = "Optional", GenericParameters = GenericParameters, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                }
                else
                {
                    return new OS.PrimitiveDef { Name = e.Name, GenericParameters = new List<OS.VariableDef> { }, Attributes = e.Attributes, Description = e.Description };
                }
            }

            private OS.LiteralDef TranslateLiteral(RS.LiteralDef l)
            {
                return new OS.LiteralDef { Name = l.Name, Value = l.Value, Attributes = l.Attributes, Description = l.Description };
            }
            private OS.EnumDef TranslateEnum(RS.EnumDef e)
            {
                return new OS.EnumDef { Name = e.Name, Version = "", UnderlyingType = TranslateTypeSpec(e.UnderlyingType), Literals = e.Literals.Select(l => TranslateLiteral(l)).ToList(), Attributes = e.Attributes, Description = e.Description };
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
                        var tBinary = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "List", Version = "" });
                        var tByte = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Byte", Version = "" });
                        return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = tBinary, ParameterValues = new List<OS.TypeSpec> { tByte } });
                    }
                    else
                    {
                        return OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = t.TypeRef.Value, Version = "" });
                    }
                }
                else if (t.OnOptional)
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    OptionalUsed = true;
                    var Underlying = TranslateTypeSpec(RS.TypeSpec.CreateTypeRef(t.Optional));
                    return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Optional", Version = "" }), ParameterValues = new List<OS.TypeSpec> { Underlying } });
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
                return new OS.RecordDef { Name = r.Name, Version = "", GenericParameters = new List<OS.VariableDef> { }, Fields = Fields, Attributes = r.Attributes, Description = r.Description };
            }
        }
    }
}
