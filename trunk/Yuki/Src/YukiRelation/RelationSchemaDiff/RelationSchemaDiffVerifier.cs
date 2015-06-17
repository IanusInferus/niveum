//==========================================================================
//
//  File:        RelationSchemaDiffVerifier.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异验证器
//  Version:     2015.06.17.
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
    public sealed class RelationSchemaDiffVerifier
    {
        public static void Verifiy(Schema Old, Schema New, List<EntityMapping> l)
        {
            var OldTypes = Old.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);
            var NewTypes = New.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);

            var EntityNameAppearedInManyNewOrCopy = l.Where(m => m.Method.OnNew).Concat(l.Where(m => m.Method.OnCopy)).GroupBy(m => m.EntityName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (EntityNameAppearedInManyNewOrCopy.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInManyNewOrCopy: " + String.Join(", ", EntityNameAppearedInManyNewOrCopy));
            }

            foreach (var fg in l.Where(m => m.Method.OnField).GroupBy(m => m.EntityName))
            {
                var EntityName = fg.Key;
                var FieldMappings = fg.Select(m => m.Method.Field).ToList();

                var FieldNameAppearedInManyNewOrCopy = FieldMappings.Where(m => m.Method.OnNew).Concat(FieldMappings.Where(m => m.Method.OnCopy)).GroupBy(m => m.FieldName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (FieldNameAppearedInManyNewOrCopy.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInManyNewOrCopy: " + EntityName + ".(" + String.Join(", ", FieldNameAppearedInManyNewOrCopy) + ")");
                }
            }

            var AppliedTypes = new Dictionary<String, TypeDef>();
            foreach (var m in l)
            {
                if (m.Method.OnNew)
                {
                    if (!NewTypes.ContainsKey(m.EntityName)) { throw new InvalidOperationException("NewNotExist: " + m.EntityName); }
                    AppliedTypes.Add(m.EntityName, NewTypes[m.EntityName]);
                }
                else if (m.Method.OnCopy)
                {
                    var EntityNameSource = m.Method.Copy;
                    if (!NewTypes.ContainsKey(m.EntityName)) { throw new InvalidOperationException("NewNotExist: " + m.EntityName); }
                    if (!OldTypes.ContainsKey(EntityNameSource)) { throw new InvalidOperationException("OldNotExist: " + EntityNameSource); }
                    var ne = NewTypes[m.EntityName].Entity;
                    var oe = OldTypes[EntityNameSource].Entity;
                    var e = new EntityDef { Name = ne.Name, CollectionName = ne.CollectionName, Fields = oe.Fields, Description = oe.Description, PrimaryKey = oe.PrimaryKey, UniqueKeys = oe.UniqueKeys, NonUniqueKeys = oe.NonUniqueKeys };
                    AppliedTypes.Add(m.EntityName, TypeDef.CreateEntity(e));
                }
            }
            foreach (var t in NewTypes)
            {
                if (AppliedTypes.ContainsKey(t.Key)) { continue; }
                if (OldTypes.ContainsKey(t.Key))
                {
                    AppliedTypes.Add(t.Key, OldTypes[t.Key]);
                }
            }
            var MissingEntities = NewTypes.Keys.Except(AppliedTypes.Keys).ToList();
            if (MissingEntities.Count > 0)
            {
                throw new InvalidOperationException("MissingEntities: " + String.Join(", ", MissingEntities));
            }
            var RedundantEntities = AppliedTypes.Keys.Except(NewTypes.Keys).ToList();
            if (RedundantEntities.Count > 0)
            {
                throw new InvalidOperationException("RedundantEntities: " + String.Join(", ", RedundantEntities));
            }

            foreach (var fg in l.Where(m => m.Method.OnField).GroupBy(m => m.EntityName))
            {
                var EntityName = fg.Key;
                var FieldMappings = fg.Select(m => m.Method.Field).ToList();

                if (!AppliedTypes.ContainsKey(EntityName)) { throw new InvalidOperationException("AppliedNotExist: " + EntityName); }
                if (!NewTypes.ContainsKey(EntityName)) { throw new InvalidOperationException("NewNotExist: " + EntityName); }

                var oe = AppliedTypes[EntityName].Entity;
                var ne = NewTypes[EntityName].Entity;
                var OldFields = oe.Fields.ToDictionary(f => f.Name);
                var NewFields = ne.Fields.ToDictionary(f => f.Name);
                var AppliedFields = new Dictionary<String, VariableDef>();
                foreach (var fm in FieldMappings)
                {
                    if (fm.Method.OnNew)
                    {
                        if (!NewFields.ContainsKey(fm.FieldName)) { throw new InvalidOperationException("NewNotExist: " + EntityName + "." + fm.FieldName); }
                        if (AppliedFields.ContainsKey(fm.FieldName)) { throw new InvalidOperationException("AppliedExist: " + EntityName + "." + fm.FieldName); }
                        AppliedFields.Add(fm.FieldName, NewFields[fm.FieldName]);
                    }
                    else if (fm.Method.OnCopy)
                    {
                        if (!NewFields.ContainsKey(fm.FieldName)) { throw new InvalidOperationException("NewNotExist: " + EntityName + "." + fm.FieldName); }
                        if (!OldFields.ContainsKey(fm.Method.Copy)) { throw new InvalidOperationException("OldNotExist: " + EntityName + "." + fm.Method.Copy); }
                        if (AppliedFields.ContainsKey(fm.FieldName)) { throw new InvalidOperationException("AppliedExist: " + EntityName + "." + fm.FieldName); }
                        AppliedFields.Add(fm.FieldName, NewFields[fm.FieldName]);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                foreach (var f in ne.Fields)
                {
                    if (AppliedFields.ContainsKey(f.Name)) { continue; }
                    if (OldFields.ContainsKey(f.Name))
                    {
                        AppliedFields.Add(f.Name, OldFields[f.Name]);
                    }
                }

                var e = new EntityDef { Name = oe.Name, CollectionName = oe.CollectionName, Fields = AppliedFields.Select(f => f.Value).ToList(), Description = oe.Description, PrimaryKey = oe.PrimaryKey, UniqueKeys = oe.UniqueKeys, NonUniqueKeys = oe.NonUniqueKeys };
                AppliedTypes[EntityName] = TypeDef.CreateEntity(e);
            }

            foreach (var EntityName in l.Select(m => m.EntityName).Distinct())
            {
                var oe = AppliedTypes[EntityName].Entity;
                var ne = NewTypes[EntityName].Entity;
                var AppliedFields = oe.Fields.ToDictionary(f => f.Name);
                var NewFields = ne.Fields.ToDictionary(f => f.Name);

                var MissingFields = NewFields.Keys.Except(AppliedFields.Keys).ToList();
                if (MissingFields.Count > 0)
                {
                    throw new InvalidOperationException("MissingFields: " + EntityName + ".(" + String.Join(", ", MissingFields) + ")");
                }
                var RedundantFields = AppliedFields.Keys.Except(NewFields.Keys).ToList();
                if (RedundantFields.Count > 0)
                {
                    throw new InvalidOperationException("RedundantFields: " + EntityName + ".(" + String.Join(", ", RedundantFields) + ")");
                }

                foreach (var pf in AppliedFields)
                {
                    var f = pf.Value;
                    var nf = NewFields[pf.Key];
                    if (!Equals(f.Type, nf.Type))
                    {
                        throw new InvalidOperationException("TypeIncompatible: " + EntityName + "." + pf.Key);
                    }
                }
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
