using System;
using System.Collections.Generic;
using System.Linq;

namespace Nivea.Template.Semantics
{
    public static class Extensions
    {

        public static String Name(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return t.Primitive.Name;
            }
            else if (t.OnAlias)
            {
                return t.Alias.Name;
            }
            else if (t.OnRecord)
            {
                return t.Record.Name;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Name;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Name;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static String Version(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return "";
            }
            else if (t.OnAlias)
            {
                return t.Alias.Version;
            }
            else if (t.OnRecord)
            {
                return t.Record.Version;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Version;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Version;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static String VersionedName(this PrimitiveDef t)
        {
            var Name = t.Name;
            var Version = "";
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this AliasDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this RecordDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TaggedUnionDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this EnumDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TypeDef t)
        {
            var Name = t.Name();
            var Version = t.Version();
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }
        public static String VersionedName(this TypeRef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "[" + Version + "]";
        }

        public static String Description(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return t.Primitive.Description;
            }
            else if (t.OnAlias)
            {
                return t.Alias.Description;
            }
            else if (t.OnRecord)
            {
                return t.Record.Description;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.Description;
            }
            else if (t.OnEnum)
            {
                return t.Enum.Description;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static List<VariableDef> GenericParameters(this TypeDef t)
        {
            if (t.OnPrimitive)
            {
                return t.Primitive.GenericParameters;
            }
            else if (t.OnAlias)
            {
                return t.Alias.GenericParameters;
            }
            else if (t.OnRecord)
            {
                return t.Record.GenericParameters;
            }
            else if (t.OnTaggedUnion)
            {
                return t.TaggedUnion.GenericParameters;
            }
            else if (t.OnEnum)
            {
                return new List<VariableDef> { };
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static String TypeFriendlyName(this PrimitiveDef t)
        {
            var Name = t.Name;
            var Version = "";
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this AliasDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this RecordDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TaggedUnionDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this EnumDef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeDef t)
        {
            var Name = t.Name();
            var Version = t.Version();
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeRef t)
        {
            var Name = t.Name;
            var Version = t.Version;
            if (Version == "") { return Name; }
            return Name + "At" + Version;
        }
        public static String TypeFriendlyName(this TypeSpec t)
        {
            return TypeFriendlyName(t, gpr => gpr);
        }
        public static String TypeFriendlyName(this TypeSpec t, Func<String, String> EvaluateGenericParameterRef)
        {
            return TypeFriendlyName(t, EvaluateGenericParameterRef, TypeFriendlyName);
        }
        public static String TypeFriendlyName(this TypeSpec Type, Func<String, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<String, String>, String> Kernel)
        {
            if (Type.OnTypeRef)
            {
                return Type.TypeRef.TypeFriendlyName();
            }
            else if (Type.OnGenericParameterRef)
            {
                return EvaluateGenericParameterRef(Type.GenericParameterRef);
            }
            else if (Type.OnTuple)
            {
                return "TupleOf" + String.Join("And", Type.Tuple.Select(t => Kernel(t, EvaluateGenericParameterRef)));
            }
            else if (Type.OnGenericTypeSpec)
            {
                return Kernel(Type.GenericTypeSpec.TypeSpec, EvaluateGenericParameterRef) + "Of" + String.Join("And", Type.GenericTypeSpec.ParameterValues.Select(t => TypeFriendlyName(t, EvaluateGenericParameterRef, Kernel)));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
