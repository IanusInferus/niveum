//==========================================================================
//
//  File:        RelationSchemaDiffTranslator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异转换器
//  Version:     2015.06.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Yuki.RelationSchema;
using Yuki.RelationValue;

namespace Yuki.RelationSchemaDiff
{
    public class RelationSchemaDiffTranslator
    {
        private Dictionary<String, TypeDef> OldTypes;
        private Dictionary<String, TypeDef> NewTypes;
        private Dictionary<String, Optional<String>> NewEntityNameToOldEntityName = new Dictionary<String, Optional<String>>();
        private HashSet<String> NewEntityNameHasExceptionTranslator = new HashSet<String>();
        private Dictionary<String, List<FieldMapping>> NewEntityNameToFieldMappings = new Dictionary<String, List<FieldMapping>>();

        public RelationSchemaDiffTranslator(Schema Old, Schema New, List<EntityMapping> l)
        {
            OldTypes = Old.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);
            NewTypes = New.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);
            foreach (var m in l)
            {
                if (m.Method.OnNew)
                {
                    NewEntityNameToOldEntityName.Add(m.EntityName, Optional<String>.Empty);
                    NewEntityNameHasExceptionTranslator.Add(m.EntityName);
                }
                else if (m.Method.OnCopy)
                {
                    var EntityNameSource = m.Method.Copy;
                    NewEntityNameToOldEntityName.Add(m.EntityName, EntityNameSource);
                }
                else if (m.Method.OnField)
                {
                    if (!NewEntityNameToFieldMappings.ContainsKey(m.EntityName))
                    {
                        NewEntityNameToOldEntityName.Add(m.EntityName, m.EntityName);
                        NewEntityNameToFieldMappings.Add(m.EntityName, new List<FieldMapping> { m.Method.Field });
                    }
                    else
                    {
                        NewEntityNameToFieldMappings[m.EntityName].Add(m.Method.Field);
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var t in NewTypes.Values)
            {
                if (!NewEntityNameToOldEntityName.ContainsKey(t.Entity.Name))
                {
                    NewEntityNameToOldEntityName.Add(t.Entity.Name, t.Entity.Name);
                }
            }
        }

        public Optional<String> GetOldEntityName(String NewEntityName)
        {
            return NewEntityNameToOldEntityName[NewEntityName];
        }

        public Func<RowVal, RowVal> GetTranslator(String NewEntityName)
        {
            if (!NewEntityNameToOldEntityName.ContainsKey(NewEntityName)) { throw new InvalidOperationException(); }
            if (!NewEntityNameToFieldMappings.ContainsKey(NewEntityName))
            {
                if (NewEntityNameHasExceptionTranslator.Contains(NewEntityName))
                {
                    return r => { throw new InvalidOperationException(); };
                }
                else
                {
                    return r => r;
                }
            }

            var OldEntityName = NewEntityNameToOldEntityName[NewEntityName].ValueOrDefault(null);
            if (OldEntityName == null) { throw new InvalidOperationException(); }

            var FieldMappings = NewEntityNameToFieldMappings[NewEntityName];
            var OldColumns = OldTypes[OldEntityName].Entity.Fields.Where(f => f.Attribute.OnColumn).ToList();
            var NewColumns = NewTypes[NewEntityName].Entity.Fields.Where(f => f.Attribute.OnColumn).ToList();

            var OldColumnIndices = OldColumns.Select((c, i) => new KeyValuePair<String, int>(c.Name, i)).ToDictionary(p => p.Key, p => p.Value);
            var NewColumnIndices = NewColumns.Select((c, i) => new KeyValuePair<String, int>(c.Name, i)).ToDictionary(p => p.Key, p => p.Value);

            var NewColumnIndexToOldColumnIndex = new Dictionary<int, int>();
            var NewColumnIndexToCreator = new Dictionary<int, Optional<PrimitiveVal>>();

            foreach (var fm in FieldMappings)
            {
                if (fm.Method.OnNew)
                {
                    NewColumnIndexToCreator.Add(NewColumnIndices[fm.FieldName], fm.Method.New);
                }
                else if (fm.Method.OnCopy)
                {
                    NewColumnIndexToOldColumnIndex.Add(NewColumnIndices[fm.FieldName], OldColumnIndices[fm.Method.Copy]);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            foreach (var v in NewColumns)
            {
                var NewColumnIndex = NewColumnIndices[v.Name];
                if (!(NewColumnIndexToOldColumnIndex.ContainsKey(NewColumnIndex) || NewColumnIndexToCreator.ContainsKey(NewColumnIndex)))
                {
                    NewColumnIndexToOldColumnIndex.Add(NewColumnIndex, OldColumnIndices[v.Name]);
                }
            }

            var ColumnFetchers = new List<Func<RowVal, ColumnVal>>();
            foreach (var p in NewColumns.Select((c, i) => new KeyValuePair<int, VariableDef>(i, c)))
            {
                var i = p.Key;
                var c = p.Value;
                if (NewColumnIndexToCreator.ContainsKey(i))
                {
                    var v = NewColumnIndexToCreator[i];
                    var nc = NewColumns[i];
                    if (nc.Type.OnTypeRef)
                    {
                        if (v.OnNotHasValue) { throw new InvalidOperationException(); }
                        var vv = ColumnVal.CreatePrimitive(v.HasValue);
                        ColumnFetchers.Add(r => vv);
                    }
                    else if (nc.Type.OnOptional)
                    {
                        if (v.OnNotHasValue)
                        {
                            var vv = ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                            ColumnFetchers.Add(r => vv);
                        }
                        else
                        {
                            var vv = ColumnVal.CreateOptional(v.HasValue);
                            ColumnFetchers.Add(r => vv);
                        }
                    }
                    else if (nc.Type.OnList && (nc.Type.List.Value == "Byte"))
                    {
                        if (v.OnNotHasValue) { throw new InvalidOperationException(); }
                        var vv = ColumnVal.CreatePrimitive(v.HasValue);
                        ColumnFetchers.Add(r => vv);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (NewColumnIndexToOldColumnIndex.ContainsKey(i))
                {
                    var OldColumnIndex = NewColumnIndexToOldColumnIndex[i];
                    var nc = NewColumns[i];
                    var oc = OldColumns[OldColumnIndex];

                    if (Equals(nc.Type, oc.Type))
                    {
                        ColumnFetchers.Add(r => r.Columns[OldColumnIndex]);
                    }
                    else
                    {
                        var t = GetColumnTranslator(oc.Type, nc.Type);
                        ColumnFetchers.Add(r => t(r.Columns[OldColumnIndex]));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            return r => new RowVal { Columns = ColumnFetchers.Select(f => f(r)).ToList() };
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
        private static Func<ColumnVal, ColumnVal> GetColumnTranslator(TypeSpec OldType, TypeSpec NewType)
        {
            if (OldType.OnList)
            {
                if (OldType.List.Value == "Byte")
                {
                    return GetColumnTranslator(TypeSpec.CreateTypeRef(new TypeRef { Value = "Binary" }), NewType);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            if (NewType.OnList)
            {
                if (NewType.List.Value == "Byte")
                {
                    return GetColumnTranslator(OldType, TypeSpec.CreateTypeRef(new TypeRef { Value = "Binary" }));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            if (OldType.OnTypeRef && NewType.OnTypeRef)
            {
                var t = GetPrimitiveTranslator(OldType.TypeRef, NewType.TypeRef);
                return cv =>
                {
                    if (!cv.OnPrimitive)
                    {
                        throw new InvalidOperationException();
                    }
                    return ColumnVal.CreatePrimitive(t(cv.Primitive));
                };
            }
            else if (OldType.OnOptional && NewType.OnOptional)
            {
                var t = GetPrimitiveTranslator(OldType.Optional, NewType.Optional);
                return cv =>
                {
                    if (!cv.OnOptional)
                    {
                        throw new InvalidOperationException();
                    }
                    if (cv.Optional.OnNotHasValue)
                    {
                        return ColumnVal.CreateOptional(Optional<PrimitiveVal>.Empty);
                    }
                    else
                    {
                        return ColumnVal.CreateOptional(t(cv.Optional.HasValue));
                    }
                };
            }
            else if (OldType.OnTypeRef && NewType.OnOptional)
            {
                var t = GetPrimitiveTranslator(OldType.TypeRef, NewType.Optional);
                return cv =>
                {
                    if (!cv.OnPrimitive)
                    {
                        throw new InvalidOperationException();
                    }
                    return ColumnVal.CreateOptional(t(cv.Primitive));
                };
            }
            else if (OldType.OnOptional && NewType.OnTypeRef)
            {
                var t = GetPrimitiveTranslator(OldType.Optional, NewType.TypeRef);
                return cv =>
                {
                    if (!cv.OnOptional)
                    {
                        throw new InvalidOperationException();
                    }
                    if (cv.Optional.OnNotHasValue)
                    {
                        throw new InvalidOperationException();
                    }
                    else
                    {
                        return ColumnVal.CreatePrimitive(t(cv.Optional.HasValue));
                    }
                };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        private static Func<PrimitiveVal, PrimitiveVal> GetPrimitiveTranslator(TypeRef OldType, TypeRef NewType)
        {
            if (OldType.Value == NewType.Value)
            {
                return v => v;
            }
            if (OldType.Value == "Boolean")
            {
                if (NewType.Value == "String")
                {
                    return v =>
                    {
                        if (!v.OnBooleanValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateStringValue(v.BooleanValue ? "True" : "False");
                    };
                }
                else if (NewType.Value == "Int")
                {
                    return v =>
                    {
                        if (!v.OnBooleanValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateIntValue(v.BooleanValue ? -1 : 0);
                    };
                }
                else if (NewType.Value == "Real")
                {
                    return v =>
                    {
                        if (!v.OnBooleanValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateRealValue(v.BooleanValue ? -1 : 0);
                    };
                }
                else if (NewType.Value == "Binary")
                {
                    return v => { throw new InvalidOperationException(); };
                }
                else if (NewType.Value == "Int64")
                {
                    return v =>
                    {
                        if (!v.OnBooleanValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateInt64Value(v.BooleanValue ? -1 : 0);
                    };
                }
            }
            else if (OldType.Value == "String")
            {
                if (NewType.Value == "Boolean")
                {
                    return v =>
                    {
                        if (!v.OnStringValue) { throw new InvalidOperationException(); };
                        var b = false;
                        if (v.StringValue == "True")
                        {
                            b = true;
                        }
                        else if (v.StringValue == "False")
                        {
                            b = false;
                        }
                        return PrimitiveVal.CreateBooleanValue(b);
                    };
                }
                else if (NewType.Value == "Int")
                {
                    return v =>
                    {
                        if (!v.OnStringValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateIntValue(NumericStrings.InvariantParseInt32(v.StringValue));
                    };
                }
                else if (NewType.Value == "Real")
                {
                    return v =>
                    {
                        if (!v.OnStringValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateRealValue(NumericStrings.InvariantParseFloat64(v.StringValue));
                    };
                }
                else if (NewType.Value == "Binary")
                {
                    return v => { throw new InvalidOperationException(); };
                }
                else if (NewType.Value == "Int64")
                {
                    return v =>
                    {
                        if (!v.OnStringValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateInt64Value(NumericStrings.InvariantParseInt64(v.StringValue));
                    };
                }
            }
            else if (OldType.Value == "Int")
            {
                if (NewType.Value == "Boolean")
                {
                    return v =>
                    {
                        if (!v.OnIntValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateBooleanValue(v.IntValue != 0);
                    };
                }
                else if (NewType.Value == "String")
                {
                    return v =>
                    {
                        if (!v.OnIntValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateStringValue(v.IntValue.ToInvariantString());
                    };
                }
                else if (NewType.Value == "Real")
                {
                    return v =>
                    {
                        if (!v.OnIntValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateRealValue(v.IntValue);
                    };
                }
                else if (NewType.Value == "Binary")
                {
                    return v => { throw new InvalidOperationException(); };
                }
                else if (NewType.Value == "Int64")
                {
                    return v =>
                    {
                        if (!v.OnIntValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateInt64Value(v.IntValue);
                    };
                }
            }
            else if (OldType.Value == "Real")
            {
                if (NewType.Value == "Boolean")
                {
                    return v =>
                    {
                        if (!v.OnRealValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateBooleanValue(v.RealValue != 0.0);
                    };
                }
                else if (NewType.Value == "String")
                {
                    return v =>
                    {
                        if (!v.OnRealValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateStringValue(v.RealValue.ToInvariantString());
                    };
                }
                else if (NewType.Value == "Int")
                {
                    return v =>
                    {
                        if (!v.OnRealValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateIntValue(Convert.ToInt32(v.RealValue));
                    };
                }
                else if (NewType.Value == "Binary")
                {
                    return v => { throw new InvalidOperationException(); };
                }
                else if (NewType.Value == "Int64")
                {
                    return v =>
                    {
                        if (!v.OnRealValue) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateInt64Value(Convert.ToInt64(v.RealValue));
                    };
                }
            }
            else if (OldType.Value == "Binary")
            {
                return v => { throw new InvalidOperationException(); };
            }
            else if (OldType.Value == "Int64")
            {
                if (NewType.Value == "Boolean")
                {
                    return v =>
                    {
                        if (!v.OnInt64Value) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateBooleanValue(v.Int64Value != 0);
                    };
                }
                else if (NewType.Value == "String")
                {
                    return v =>
                    {
                        if (!v.OnInt64Value) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateStringValue(v.Int64Value.ToInvariantString());
                    };
                }
                else if (NewType.Value == "Int")
                {
                    return v =>
                    {
                        if (!v.OnInt64Value) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateIntValue((int)(v.Int64Value));
                    };
                }
                else if (NewType.Value == "Real")
                {
                    return v =>
                    {
                        if (!v.OnInt64Value) { throw new InvalidOperationException(); };
                        return PrimitiveVal.CreateRealValue(v.Int64Value);
                    };
                }
                else if (NewType.Value == "Binary")
                {
                    return v => { throw new InvalidOperationException(); };
                }
            }

            throw new InvalidOperationException();
        }
    }
}
