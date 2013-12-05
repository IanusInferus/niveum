//==========================================================================
//
//  File:        ObjectSchemaDiffGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构差异生成器
//  Version:     2013.12.05.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yuki.ObjectSchema
{
    public class ObjectSchemaDiffResult
    {
        public Schema Patch;
        public Schema Revert;
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
                if (t.Version() != "") { throw new InvalidOperationException(String.Format("VersionedTypeIsNotAllowed: {0}", t.Name())); }
            }
            foreach (var t in Right.Types)
            {
                if (t.Version() != "") { throw new InvalidOperationException(String.Format("VersionedTypeIsNotAllowed: {0}", t.Name())); }
            }

            Func<Schema, Func<TypeDef, Schema>> GetGen = s =>
            {
                var g = s.GetSubSchemaGenerator();
                return t => g(new TypeDef[] { t }, new TypeSpec[] { });
            };

            var LeftGen = GetGen(Left);
            var RightGen = GetGen(Right);
            var LeftTypeRefBinaries = Left.TypeRefs.ToDictionary(t => t.Name(), t => LeftGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var RightTypeRefBinaries = Right.TypeRefs.ToDictionary(t => t.Name(), t => RightGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
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

            var LeftTypeBinaries = Left.Types.ToDictionary(t => t.Name(), t => LeftGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
            var RightTypeBinaries = Right.Types.ToDictionary(t => t.Name(), t => RightGen(t).GetUnifiedBinaryRepresentation(), StringComparer.OrdinalIgnoreCase);
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
                Types = Right.Types.Where(t => !CommonTypes.Contains(t.Name())).ToArray(),
                TypeRefs = Right.TypeRefs.Concat(Right.Types.Where(t => CommonTypes.Contains(t.Name()))).ToArray(),
                Imports = Right.Imports.Except(Left.Imports, StringComparer.OrdinalIgnoreCase).ToArray(),
                TypePaths = Right.TypePaths.Where(tp => !CommonTypes.Contains(tp.Name)).ToArray()
            };
            var Revert = new Schema
            {
                Types = Left.Types.Where(t => !CommonTypes.Contains(t.Name())).ToArray(),
                TypeRefs = Left.TypeRefs.Concat(Left.Types.Where(t => CommonTypes.Contains(t.Name()))).ToArray(),
                Imports = Left.Imports.Except(Right.Imports, StringComparer.OrdinalIgnoreCase).ToArray(),
                TypePaths = Right.TypePaths.Where(tp => !CommonTypes.Contains(tp.Name)).ToArray()
            };

            return new ObjectSchemaDiffResult
            {
                Patch = Patch,
                Revert = Revert
            };
        }
    }
}
