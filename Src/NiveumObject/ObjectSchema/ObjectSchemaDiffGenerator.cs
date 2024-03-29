﻿//==========================================================================
//
//  File:        ObjectSchemaDiffGenerator.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构差异生成器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema
{
    public sealed class ObjectSchemaDiffResult
    {
        public Schema Patch { get; init; }
        public Schema Revert { get; init; }
    }

    public sealed class ObjectSchemaDiffGenerator
    {
        public ObjectSchemaDiffGenerator()
        {
        }

        public ObjectSchemaDiffResult Generate(Schema Left, Schema Right)
        {
            foreach (var t in Left.Types)
            {
                if (t.Version() != "") { throw new InvalidOperationException(String.Format("VersionedTypeIsNotAllowed: {0}", t.FullName())); }
            }
            foreach (var t in Right.Types)
            {
                if (t.Version() != "") { throw new InvalidOperationException(String.Format("VersionedTypeIsNotAllowed: {0}", t.FullName())); }
            }

            Func<Schema, Func<TypeDef, Schema>> GetGen = s =>
            {
                var g = s.GetSchemaClosureGenerator();
                return t => g.GetSubSchema(new List<TypeDef> { t }, new List<TypeSpec> { });
            };

            var LeftGen = GetGen(Left);
            var RightGen = GetGen(Right);
            var LeftTypeRefBinaries = Left.TypeRefs.ToDictionary(t => t.FullName(), t => LeftGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var RightTypeRefBinaries = Right.TypeRefs.ToDictionary(t => t.FullName(), t => RightGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var CommonTypeRefs = LeftTypeRefBinaries.Keys.Intersect(RightTypeRefBinaries.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            if ((LeftTypeRefBinaries.Count > CommonTypeRefs.Count) || (RightTypeRefBinaries.Count > CommonTypeRefs.Count))
            {
                var LeftTypeRefExceptRightTypeRef = LeftTypeRefBinaries.Keys.Except(CommonTypeRefs, StringComparer.OrdinalIgnoreCase).ToList();
                var RightTypeRefExceptLeftTypeRef = RightTypeRefBinaries.Keys.Except(CommonTypeRefs, StringComparer.OrdinalIgnoreCase).ToList();
                throw new InvalidOperationException(String.Format("TypeRefNotMatch: {0} - {1}", String.Join(" ", LeftTypeRefExceptRightTypeRef), String.Join(" ", RightTypeRefExceptLeftTypeRef)));
            }

            var ChangedTypeRefs = new List<String>();
            foreach (var Name in LeftTypeRefBinaries.Keys)
            {
                var LeftBinary = LeftTypeRefBinaries[Name];
                var RightBinary = RightTypeRefBinaries[Name];
                if (!LeftBinary.SequenceEqual(RightBinary))
                {
                    ChangedTypeRefs.Add(Name);
                }
            }
            if (ChangedTypeRefs.Count > 0)
            {
                throw new InvalidOperationException(String.Format("TypeRefChanged: {0}", String.Join(" ", ChangedTypeRefs)));
            }

            var LeftTypeBinaries = Left.Types.ToDictionary(t => t.FullName(), t => LeftGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var RightTypeBinaries = Right.Types.ToDictionary(t => t.FullName(), t => RightGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var CommonTypes = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
            foreach (var Name in LeftTypeBinaries.Keys.Intersect(RightTypeBinaries.Keys))
            {
                var LeftBinary = LeftTypeBinaries[Name];
                var RightBinary = RightTypeBinaries[Name];

                if (LeftBinary.SequenceEqual(RightBinary))
                {
                    CommonTypes.Add(Name);
                }
            }

            var Patch = new Schema
            {
                Types = Right.Types.Where(t => !CommonTypes.Contains(t.FullName())).ToList(),
                TypeRefs = Right.TypeRefs.Concat(Right.Types.Where(t => CommonTypes.Contains(t.FullName()))).ToList(),
                Imports = Right.Imports.Except(Left.Imports, StringComparer.OrdinalIgnoreCase).ToList()
            };
            var Revert = new Schema
            {
                Types = Left.Types.Where(t => !CommonTypes.Contains(t.FullName())).ToList(),
                TypeRefs = Left.TypeRefs.Concat(Left.Types.Where(t => CommonTypes.Contains(t.FullName()))).ToList(),
                Imports = Left.Imports.Except(Right.Imports, StringComparer.OrdinalIgnoreCase).ToList()
            };

            return new ObjectSchemaDiffResult
            {
                Patch = Patch,
                Revert = Revert
            };
        }

        public List<TypeDef> GetCompatibleTypes(List<KeyValuePair<String, Schema>> VersionAndSchemaList)
        {
            var ResultTypes = new List<TypeDef> { };
            foreach (var k in Enumerable.Range(0, VersionAndSchemaList.Count - 1))
            {
                var Old = VersionAndSchemaList[k];
                var New = VersionAndSchemaList[k + 1];
                var AddtionalTypesToBePreserved = Generate(Old.Value, New.Value).Revert;
                var h = new HashSet<String>(AddtionalTypesToBePreserved.Types.Select(t => t.VersionedName()), StringComparer.OrdinalIgnoreCase);
                var MapConf = new TypeMapConfiguration { MapTypeSpecKernel = (d, ts) =>
                {
                    if (ts.OnTypeRef && h.Contains(ts.TypeRef.VersionedName()))
                    {
                        return TypeSpec.CreateTypeRef(new TypeRef { Name = ts.TypeRef.Name, Version = Old.Key });
                    }
                    else
                    {
                        return ts;
                    }
                }};
                ResultTypes = ResultTypes.Select(t => t.MapType(MapConf)).Concat(AddtionalTypesToBePreserved.GetTypesVersioned(Old.Key).Types).ToList();
            }
            return ResultTypes;
        }
    }
}
