//==========================================================================
//
//  File:        RelationSchemaDiffGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异生成器
//  Version:     2015.02.15.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Yuki.RelationSchema;
using Yuki.RelationValue;

namespace Yuki.RelationSchemaDiff
{
    public sealed class RelationSchemaDiffGenerator
    {
        public static List<AlterEntity> Generate(Schema Old, Schema New)
        {
            var OldTypes = Old.GetMap().ToDictionary(t => t.Key, t => t.Value);
            var NewTypes = New.GetMap().ToDictionary(t => t.Key, t => t.Value);

            var l = new List<AlterEntity>();
            foreach (var t in OldTypes)
            {
                if (t.Value.OnEntity && (!NewTypes.ContainsKey(t.Key) || !NewTypes[t.Key].OnEntity))
                {
                    l.Add(new AlterEntity { EntityName = t.Key, Method = AlterEntityMethod.CreateDelete() });
                }
            }
            foreach (var t in NewTypes)
            {
                if (t.Value.OnEntity)
                {
                    if (!OldTypes.ContainsKey(t.Key) || !OldTypes[t.Key].OnEntity)
                    {
                        l.Add(new AlterEntity { EntityName = t.Key, Method = AlterEntityMethod.CreateCreate() });
                    }
                }
            }
            foreach (var t in NewTypes)
            {
                if (t.Value.OnEntity)
                {
                    if (!OldTypes.ContainsKey(t.Key) || !OldTypes[t.Key].OnEntity)
                    {
                    }
                    else
                    {
                        var ot = OldTypes[t.Key];
                        var OldFields = ot.Entity.Fields.ToDictionary(f => f.Name);
                        var NewFields = t.Value.Entity.Fields.ToDictionary(f => f.Name);

                        foreach (var f in OldFields)
                        {
                            if (f.Value.Attribute.OnColumn && (!NewFields.ContainsKey(f.Key) || !NewFields[f.Key].Attribute.OnColumn))
                            {
                                l.Add(new AlterEntity { EntityName = t.Key, Method = AlterEntityMethod.CreateField(new AlterField { FieldName = f.Key, Method = AlterFieldMethod.CreateDelete() }) });
                            }
                        }
                        foreach (var f in NewFields)
                        {
                            if (f.Value.Attribute.OnColumn)
                            {
                                if (!OldFields.ContainsKey(f.Key) || !OldFields[f.Key].Attribute.OnColumn)
                                {
                                    l.Add(new AlterEntity { EntityName = t.Key, Method = AlterEntityMethod.CreateField(new AlterField { FieldName = f.Key, Method = AlterFieldMethod.CreateCreate(GetDefaultValue(f.Value.Type)) }) });
                                }
                                else
                                {
                                    var of = OldFields[f.Key];
                                    if (!Equals(f.Value.Type, of.Type))
                                    {
                                        l.Add(new AlterEntity { EntityName = t.Key, Method = AlterEntityMethod.CreateField(new AlterField { FieldName = f.Key, Method = AlterFieldMethod.CreateChangeType() }) });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return l;
        }

        private static Optional<PrimitiveVal> GetDefaultValue(TypeSpec t)
        {
            if (t.OnTypeRef)
            {
                return GetDefaultValue(t.TypeRef);
            }
            else if (t.OnOptional)
            {
                return Optional<PrimitiveVal>.Empty;
            }
            else if (t.OnList)
            {
                if (t.List.Value == "Byte")
                {
                    return GetDefaultValue(new TypeRef { Value = "Binary" });
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
        private static PrimitiveVal GetDefaultValue(TypeRef r)
        {
            if (r == "Boolean")
            {
                return PrimitiveVal.CreateBooleanValue(false);
            }
            else if (r == "String")
            {
                return PrimitiveVal.CreateStringValue("");
            }
            else if (r == "Int")
            {
                return PrimitiveVal.CreateIntValue(0);
            }
            else if (r == "Real")
            {
                return PrimitiveVal.CreateRealValue(0);
            }
            else if (r == "Binary")
            {
                return PrimitiveVal.CreateBinaryValue(new List<Byte> { });
            }
            else if (r == "Int64")
            {
                return PrimitiveVal.CreateInt64Value(0);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Boolean Equals(TypeSpec a, TypeSpec b)
        {
            if (a.OnTypeRef && b.OnTypeRef)
            {
                return a.TypeRef.Value.Equals(b.TypeRef.Value);
            }
            else if (a.OnList && b.OnList)
            {
                return a.List.Value.Equals(b.List.Value);
            }
            else if (a.OnOptional && b.OnOptional)
            {
                return a.Optional.Value.Equals(b.Optional.Value);
            }
            return false;
        }
    }
}
