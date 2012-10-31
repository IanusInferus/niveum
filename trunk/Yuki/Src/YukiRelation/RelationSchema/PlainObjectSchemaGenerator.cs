//==========================================================================
//
//  File:        PlainObjectSchemaGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 简单对象类型结构生成器
//  Version:     2012.10.31.
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
        public static OS.Schema Generate(RS.Schema Schema)
        {
            var s = (new Generator { Schema = Schema, AdditionalTypeRefs = new OS.TypeDef[] { } }).Generate();
            return s;
        }
        public static OS.Schema TrimAsRelationSchema(OS.Schema Schema)
        {
            var RelationSchema = RS.RelationSchemaTranslator.Translate(Schema);
            var s = (new Generator { Schema = RelationSchema, AdditionalTypeRefs = Schema.TypeRefs }).Generate();
            var PlainSchema = new OS.Schema { Types = s.Types, TypeRefs = Schema.TypeRefs, Imports = Schema.Imports, TypePaths = Schema.TypePaths };
            return s;
        }

        private class Generator
        {
            public RS.Schema Schema;
            public OS.TypeDef[] AdditionalTypeRefs;

            public OS.Schema Generate()
            {
                var TypeRefs = Schema.TypeRefs.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).Select(t => TranslateTypeDef(t)).ToArray();
                var Types = Schema.Types.Where(t => !(t.OnPrimitive && t.Primitive.Name == "Binary")).Select(t => TranslateTypeDef(t)).ToList();
                if (UnitUsed && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Unit", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if (ByteUsed && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Byte", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Byte", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if (ListUsed && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("List", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameter = new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "List", GenericParameters = new OS.VariableDef[] { GenericParameter }, Description = "" }));
                }
                if (TypeUsed && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnPrimitive && t.Primitive.Name.Equals("Type", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Type", GenericParameters = new OS.VariableDef[] { }, Description = "" }));
                }
                if (OptionalUsed && !Types.Concat(TypeRefs).Concat(AdditionalTypeRefs).Where(t => t.OnTaggedUnion && t.TaggedUnion.Name.Equals("Optional", StringComparison.OrdinalIgnoreCase)).Any())
                {
                    var GenericParameters = new OS.VariableDef[] { new OS.VariableDef { Name = "T", Type = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Type", Version = "" }), Description = "" } };
                    Types.Add(OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = "Optional", GenericParameters = GenericParameters, Description = "" }));
                }
                return new OS.Schema { Types = Types.ToArray(), TypeRefs = TypeRefs, Imports = Schema.Imports.ToArray(), TypePaths = new OS.TypePath[] { } };
            }

            private OS.TypeDef TranslateTypeDef(RS.TypeDef t)
            {
                if (t.OnPrimitive)
                {
                    return OS.TypeDef.CreatePrimitive(TranslatePrimitive(t.Primitive));
                }
                else if (t.OnRecord)
                {
                    return OS.TypeDef.CreateRecord(TranslateRecord(t.Record));
                }
                else if (t.OnEnum)
                {
                    return OS.TypeDef.CreateEnum(TranslateEnum(t.Enum));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private OS.PrimitiveDef TranslatePrimitive(RS.PrimitiveDef e)
            {
                return new OS.PrimitiveDef { Name = e.Name, GenericParameters = new OS.VariableDef[] { }, Description = e.Description };
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
                else
                {
                    throw new InvalidOperationException();
                }
            }

            private OS.VariableDef TranslateField(RS.VariableDef f)
            {
                var fts = TranslateTypeSpec(f.Type);
                if (f.Attribute.OnColumn && f.Attribute.Column.IsNullable)
                {
                    UnitUsed = true;
                    TypeUsed = true;
                    OptionalUsed = true;
                    var ts = OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec { TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "Optional", Version = "" }), GenericParameterValues = new OS.GenericParameterValue[] { OS.GenericParameterValue.CreateTypeSpec(fts) } });
                    return new OS.VariableDef { Name = f.Name, Type = ts, Description = f.Description };
                }
                return new OS.VariableDef { Name = f.Name, Type = fts, Description = f.Description };
            }
            private OS.RecordDef TranslateRecord(RS.RecordDef r)
            {
                var Fields = r.Fields.Where(f => f.Attribute.OnColumn).Select(f => TranslateField(f)).ToArray();
                return new OS.RecordDef { Name = r.Name, Version = "", GenericParameters = new OS.VariableDef[] { }, Fields = Fields, Description = r.Description };
            }
        }
    }
}
