//==========================================================================
//
//  File:        RelationSchemaDiffVerifier.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 关系类型结构差异验证器
//  Version:     2015.02.14.
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
        public static void Verifiy(Schema Old, Schema New, List<AlterEntity> l)
        {
            var OldTypes = Old.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);
            var NewTypes = New.GetMap().Where(t => t.Value.OnEntity).ToDictionary(t => t.Key, t => t.Value);

            var EntityNameCountAppearedInCreate = l.Where(ae => ae.Method.OnCreate).GroupBy(ae => ae.EntityName).ToDictionary(g => g.Key, g => g.Count());
            var EntityNameCountAppearedInDelete = l.Where(ae => ae.Method.OnDelete).GroupBy(ae => ae.EntityName).ToDictionary(g => g.Key, g => g.Count());
            var EntityNameCountAppearedInRenameSource = l.Where(ae => ae.Method.OnRename).GroupBy(ae => ae.EntityName).ToDictionary(g => g.Key, g => g.Count());
            var EntityNameCountAppearedInRenameDestination = l.Where(ae => ae.Method.OnRename).GroupBy(ae => ae.Method.Rename).ToDictionary(g => g.Key, g => g.Count());
            var AltersInEntity = l.Where(ae => ae.Method.OnField).GroupBy(ae => ae.EntityName).ToDictionary(g => g.Key, g => g.Select(ae => ae.Method.Field).ToList());
            var EntityNameCountAppearedInAlter = AltersInEntity.ToDictionary(g => g.Key, g => g.Value.Count);

            var EntityNameAppearedInCreateMany = EntityNameCountAppearedInCreate.Where(c => c.Value > 1).ToList();
            if (EntityNameAppearedInCreateMany.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInCreateMany: " + String.Join(", ", EntityNameAppearedInCreateMany));
            }
            var EntityNameAppearedInDeleteMany = EntityNameCountAppearedInDelete.Where(c => c.Value > 1).ToList();
            if (EntityNameAppearedInDeleteMany.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInDeleteMany: " + String.Join(", ", EntityNameAppearedInDeleteMany));
            }
            var EntityNameAppearedInRenameSourceMany = EntityNameCountAppearedInRenameSource.Where(c => c.Value > 1).ToList();
            if (EntityNameAppearedInRenameSourceMany.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInRenameSourceMany: " + String.Join(", ", EntityNameAppearedInRenameSourceMany));
            }
            var EntityNameAppearedInRenameDestinationMany = EntityNameCountAppearedInRenameDestination.Where(c => c.Value > 1).ToList();
            if (EntityNameAppearedInRenameDestinationMany.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInRenameDestinationMany: " + String.Join(", ", EntityNameAppearedInRenameDestinationMany));
            }

            var EntityNameAppearedInBothCreateAndRenameDestination = EntityNameCountAppearedInCreate.Select(c => c.Key).Intersect(EntityNameCountAppearedInRenameDestination.Select(c => c.Key)).ToList();
            if (EntityNameAppearedInBothCreateAndRenameDestination.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInBothCreateAndRenameDestination: " + String.Join(", ", EntityNameAppearedInBothCreateAndRenameDestination));
            }
            var EntityNameAppearedInBothDeleteAndRenameSource = EntityNameCountAppearedInDelete.Select(c => c.Key).Intersect(EntityNameCountAppearedInRenameSource.Select(c => c.Key)).ToList();
            if (EntityNameAppearedInBothDeleteAndRenameSource.Count > 0)
            {
                throw new InvalidOperationException("EntityNameAppearedInBothDeleteAndRenameSource: " + String.Join(", ", EntityNameAppearedInBothDeleteAndRenameSource));
            }

            foreach (var p in AltersInEntity)
            {
                var FieldNameCountAppearedInCreate = p.Value.Where(af => af.Method.OnCreate).GroupBy(af => af.FieldName).ToDictionary(g => g.Key, g => g.Count());
                var FieldNameCountAppearedInDelete = p.Value.Where(af => af.Method.OnDelete).GroupBy(af => af.FieldName).ToDictionary(g => g.Key, g => g.Count());
                var FieldNameCountAppearedInRenameSource = p.Value.Where(af => af.Method.OnRename).GroupBy(af => af.FieldName).ToDictionary(g => g.Key, g => g.Count());
                var FieldNameCountAppearedInRenameDestination = p.Value.Where(af => af.Method.OnRename).GroupBy(af => af.Method.Rename).ToDictionary(g => g.Key, g => g.Count());
                var FieldNameCountAppearedInChangeType = p.Value.Where(af => af.Method.OnChangeType).GroupBy(af => af.FieldName).ToDictionary(g => g.Key, g => g.Count());

                var FieldNameAppearedInCreateMany = FieldNameCountAppearedInCreate.Where(c => c.Value > 1).ToList();
                if (FieldNameAppearedInCreateMany.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInCreateMany: " + String.Join(", ", FieldNameAppearedInCreateMany));
                }
                var FieldNameAppearedInDeleteMany = FieldNameCountAppearedInDelete.Where(c => c.Value > 1).ToList();
                if (FieldNameAppearedInDeleteMany.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInDeleteMany: " + String.Join(", ", FieldNameAppearedInDeleteMany));
                }
                var FieldNameAppearedInRenameSourceMany = FieldNameCountAppearedInRenameSource.Where(c => c.Value > 1).ToList();
                if (FieldNameAppearedInRenameSourceMany.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInRenameSourceMany: " + String.Join(", ", FieldNameAppearedInRenameSourceMany));
                }
                var FieldNameAppearedInRenameDestinationMany = FieldNameCountAppearedInRenameDestination.Where(c => c.Value > 1).ToList();
                if (FieldNameAppearedInRenameDestinationMany.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInRenameDestinationMany: " + String.Join(", ", FieldNameAppearedInRenameDestinationMany));
                }
                var FieldNameAppearedInChangeTypeMany = FieldNameCountAppearedInChangeType.Where(c => c.Value > 1).ToList();
                if (FieldNameAppearedInChangeTypeMany.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInChangeTypeMany: " + String.Join(", ", FieldNameAppearedInChangeTypeMany));
                }

                var FieldNameAppearedInAnyTwoOfCreateAndRenameDestinationAndChangeType =
                    (FieldNameCountAppearedInCreate.Select(c => c.Key).Intersect(FieldNameCountAppearedInRenameDestination.Select(c => c.Key)))
                    .Union(FieldNameCountAppearedInRenameDestination.Select(c => c.Key).Intersect(FieldNameCountAppearedInChangeType.Select(c => c.Key)))
                    .Union(FieldNameCountAppearedInChangeType.Select(c => c.Key).Intersect(FieldNameCountAppearedInCreate.Select(c => c.Key))).ToList();
                if (FieldNameAppearedInAnyTwoOfCreateAndRenameDestinationAndChangeType.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInAnyTwoOfCreateAndRenameDestinationAndChangeType: " + String.Join(", ", FieldNameAppearedInAnyTwoOfCreateAndRenameDestinationAndChangeType));
                }
                var FieldNameAppearedInAnyTwoOfDeleteAndRenameSourceAndChangeType =
                    (FieldNameCountAppearedInDelete.Select(c => c.Key).Intersect(FieldNameCountAppearedInRenameSource.Select(c => c.Key)))
                    .Union(FieldNameCountAppearedInRenameSource.Select(c => c.Key).Intersect(FieldNameCountAppearedInChangeType.Select(c => c.Key)))
                    .Union(FieldNameCountAppearedInChangeType.Select(c => c.Key).Intersect(FieldNameCountAppearedInDelete.Select(c => c.Key))).ToList();
                if (FieldNameAppearedInAnyTwoOfDeleteAndRenameSourceAndChangeType.Count > 0)
                {
                    throw new InvalidOperationException("FieldNameAppearedInDeleteAndRenameSource: " + String.Join(", ", FieldNameAppearedInAnyTwoOfDeleteAndRenameSourceAndChangeType));
                }
            }

            var EntityNameAppearedInBothNonAlter =
                EntityNameCountAppearedInCreate.Select(c => c.Key)
                .Union(EntityNameCountAppearedInDelete.Select(c => c.Key))
                .Union(EntityNameCountAppearedInRenameSource.Select(c => c.Key))
                .Union(EntityNameCountAppearedInRenameDestination.Select(c => c.Key)).ToList();
            var AppliedTypes = OldTypes.Where(t => !EntityNameAppearedInBothNonAlter.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value);

            foreach (var ae in l)
            {
                if (ae.Method.OnCreate)
                {
                    if (!NewTypes.ContainsKey(ae.EntityName)) { throw new InvalidOperationException("NewNotExist: " + ae.EntityName); }
                    AppliedTypes.Add(ae.EntityName, NewTypes[ae.EntityName]);
                }
                else if (ae.Method.OnDelete)
                {
                    if (!OldTypes.ContainsKey(ae.EntityName)) { throw new InvalidOperationException("OldNotExist: " + ae.EntityName); }
                }
                else if (ae.Method.OnRename)
                {
                    var EntityNameDestination = ae.Method.Rename;
                    if (!OldTypes.ContainsKey(ae.EntityName)) { throw new InvalidOperationException("OldNotExist: " + ae.EntityName); }
                    if (!NewTypes.ContainsKey(EntityNameDestination)) { throw new InvalidOperationException("NewNotExist: " + EntityNameDestination); }
                    var oe = OldTypes[ae.EntityName].Entity;
                    var ne = NewTypes[EntityNameDestination].Entity;
                    var e = new EntityDef { Name = ne.Name, CollectionName = ne.CollectionName, Fields = oe.Fields, Description = oe.Description, PrimaryKey = oe.PrimaryKey, UniqueKeys = oe.UniqueKeys, NonUniqueKeys = oe.NonUniqueKeys };
                    AppliedTypes.Add(EntityNameDestination, TypeDef.CreateEntity(e));
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

            foreach (var p in AltersInEntity)
            {
                if (!AppliedTypes.ContainsKey(p.Key)) { throw new InvalidOperationException("AppliedNotExist: " + p.Key); }
                if (!NewTypes.ContainsKey(p.Key)) { throw new InvalidOperationException("NewNotExist: " + p.Key); }

                var oe = AppliedTypes[p.Key].Entity;
                var ne = NewTypes[p.Key].Entity;
                var OldFields = oe.Fields.ToDictionary(f => f.Name);
                var NewFields = ne.Fields.ToDictionary(f => f.Name);
                var AppliedFields = OldFields.ToDictionary(f => f.Key, f => f.Value);

                foreach (var af in p.Value)
                {
                    if (af.Method.OnDelete)
                    {
                        if (!OldFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("OldNotExist: " + p.Key + "." + af.FieldName); }
                        AppliedFields.Remove(af.FieldName);
                    }
                    else if (af.Method.OnRename)
                    {
                        if (!OldFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("OldNotExist: " + p.Key + "." + af.FieldName); }
                        AppliedFields.Remove(af.FieldName);
                    }
                }
                foreach (var af in p.Value)
                {
                    if (af.Method.OnCreate)
                    {
                        if (!NewFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("NewNotExist: " + p.Key + "." + af.FieldName); }
                        if (AppliedFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("AppliedExist: " + p.Key + "." + af.FieldName); }
                        AppliedFields.Add(af.FieldName, NewFields[af.FieldName]);
                    }
                    else if (af.Method.OnDelete)
                    {
                    }
                    else if (af.Method.OnRename)
                    {
                        if (!NewFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("NewNotExist: " + p.Key + "." + af.FieldName); }
                        if (AppliedFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("AppliedExist: " + p.Key + "." + af.FieldName); }
                        AppliedFields.Add(af.FieldName, NewFields[af.FieldName]);
                    }
                    else if (af.Method.OnChangeType)
                    {
                        if (!OldFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("OldNotExist: " + p.Key + "." + af.FieldName); }
                        if (!NewFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("NewNotExist: " + p.Key + "." + af.FieldName); }
                        if (!AppliedFields.ContainsKey(af.FieldName)) { throw new InvalidOperationException("AppliedNotExist: " + p.Key + "." + af.FieldName); }
                        AppliedFields[af.FieldName] = NewFields[af.FieldName];
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                var MissingFields = NewFields.Keys.Except(AppliedFields.Keys).ToList();
                if (MissingFields.Count > 0)
                {
                    throw new InvalidOperationException("MissingFields: " + String.Join(", ", MissingFields));
                }
                var RedundantFields = AppliedFields.Keys.Except(NewFields.Keys).ToList();
                if (RedundantFields.Count > 0)
                {
                    throw new InvalidOperationException("RedundantFields: " + String.Join(", ", RedundantFields));
                }

                foreach (var pf in AppliedFields)
                {
                    var f = pf.Value;
                    var nf = NewFields[pf.Key];
                    if (!Equals(f.Type, nf.Type))
                    {
                        throw new InvalidOperationException("TypeIncompatible: " + p.Key + "." + pf.Key);
                    }
                }

                var e = new EntityDef { Name = oe.Name, CollectionName = oe.CollectionName, Fields = AppliedFields.Select(f => f.Value).ToList(), Description = oe.Description, PrimaryKey = oe.PrimaryKey, UniqueKeys = oe.UniqueKeys, NonUniqueKeys = oe.NonUniqueKeys };
                AppliedTypes[p.Key] = TypeDef.CreateEntity(e);
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
