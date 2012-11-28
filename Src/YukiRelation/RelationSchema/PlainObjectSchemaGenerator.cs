//==========================================================================
//
//  File:        PlainObjectSchemaGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 简单对象类型结构生成器
//  Version:     2012.11.27.
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
            var s = (new Generator { Schema = Schema, AdditionalTypeRefs = new OS.TypeDef[] { } }).Generate(ForcePrimitive);
            return s;
        }
        public static OS.Schema TrimAsRelationSchema(OS.Schema Schema)
        {
            var RelationSchema = RS.RelationSchemaTranslator.Translate(Schema);
            var s = (new Generator { Schema = RelationSchema, AdditionalTypeRefs = Schema.TypeRefs }).Generate(true);
            var PlainSchema = new OS.Schema { Types = s.Types, TypeRefs = Schema.TypeRefs, Imports = Schema.Imports, TypePaths = Schema.TypePaths };
            return s;
        }

        private class Generator
        {
            public RS.Schema Schema;
            public OS.TypeDef[] AdditionalTypeRefs;

            public OS.Schema Generate(Boolean ForcePrimitive)
            {
                var TypeRefs = Schema.TypeRefs.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToArray();
                var Types = Schema.Types.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).SelectMany(t => TranslateTypeDef(t)).ToList();
                if ((ForcePrimitive || UnitUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Unit", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if ((ForcePrimitive || ByteUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Byte", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Byte", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if (ForcePrimitive && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Int", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Int", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if ((ForcePrimitive || ListUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("List", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "List", GenericParameters = new OS.VariableDef[] { GenericParameter }, Description = "" }));
                }
                if ((ForcePrimitive || TypeUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Type", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Type", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if ((ForcePrimitive || OptionalUsed) && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameters = new OS.VariableDef[] { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" } };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Optional", GenericParameters = GenericParameters, Description = "" }));
                }
                return new OS.Schema { Types = Types.ToArray(), TypeRefs = TypeRefs, Imports = Schema.Imports.ToArray(), TypePaths = new OS.TypePath[] { } };
            }

            private OS.TypeDef[] TranslateTypeDef(RS.TypeDef t)
            {
                if (t.OnPrimitive)
                {
                    return new OS.TypeDef[] { OS.TypeDef.CreatePrimitive(TranslatePrimitive(t.Primitive)) };
                }
                else if (t.OnEntity)
                {
                    return new OS.TypeDef[] { OS.TypeDef.CreateRecord(TranslateRecord(t.Entity)) };
                }
                else if (t.OnEnum)
                {
                    return new OS.TypeDef[] { OS.TypeDef.CreateEnum(TranslateEnum(t.Enum)) };
                }
                else if (t.OnQueryList)
                {
                    return new OS.TypeDef[] { };
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
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" };
                    return new OS.PrimitiveDef { Name = "List", GenericParameters = new OS.VariableDef[] { GenericParameter }, Description = "" };
                }
                else if (e.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase))
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    var GenericParameters = new OS.VariableDef[] { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" } };
                    return new OS.PrimitiveDef { Name = "Optional", GenericParameters = GenericParameters, Description = "" };
                }
                else
                {
                    return new OS.PrimitiveDef { Name = e.Name, GenericParameters = new OS.VariableDef[] { }, Description = e.Description };
                }
            }

            private OS.LiteralDef TranslateLiteral(RS.LiteralDef l)
            {
                return new OS.LiteralDef { Name = l.Name, Value = l.Value, Description = l.Description };
            }
            private OS.EnumDef TranslateEnum(RS.EnumDef e)
            {
                return new OS.EnumDef { Name = e.Name, Version = "", UnderlyingType = TranslateTypeSpec(e.UnderlyingType), Literals = e.Literals.Select(l => TranslateLiteral(l)).ToArray(), Description = e.Description };
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
                        return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = tBinary, GenericParameterValues = new OS.GenericParameterValue[] { OS.GenericParameterValue.CreateTypeSpec(tByte) } });
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
                    return OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Optional", Version = "" }), GenericParameterValues = new OS.GenericParameterValue[] { OS.GenericParameterValue.CreateTypeSpec(Underlying) } });
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private OS.VariableDef TranslateField(RS.VariableDef f)
            {
                return new OS.VariableDef { Name = f.Name, Type = TranslateTypeSpec(f.Type), Description = f.Description };
            }
            private OS.RecordDef TranslateRecord(RS.EntityDef r)
            {
                var Fields = r.Fields.Where(f => f.Attribute.OnColumn).Select(f => TranslateField(f)).ToArray();
                return new OS.RecordDef { Name = r.Name, Version = "", GenericParameters = new OS.VariableDef[] { }, Fields = Fields, Description = r.Description };
            }
        }
    }
}
