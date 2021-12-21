//==========================================================================
//
//  File:        ObjectSchemaLoader.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构加载器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Niveum.ObjectSchema
{
    public sealed class ObjectSchemaLoaderResult
    {
        public Schema Schema { get; init; }
        public Dictionary<Object, FileTextRange> Positions { get; init; }
    }

    public sealed class ObjectSchemaLoader
    {
        private Dictionary<Object, FileTextRange> Positions = new Dictionary<Object, FileTextRange>();
        private List<TypeDef> Types = new List<TypeDef>();
        private List<TypeDef> TypeRefs = new List<TypeDef>();
        private List<String> Imports = new List<String>();
        private Dictionary<TypeDef, List<String>> TypeToNamespace = new Dictionary<TypeDef, List<String>>();
        private Dictionary<TypeDef, List<List<String>>> TypeToNamespaceImports = new Dictionary<TypeDef, List<List<String>>>();

        public ObjectSchemaLoaderResult GetResult()
        {
            ResolveNamespaces();
            var oslr = new ObjectSchemaLoaderResult { Schema = new Schema { Types = Types, TypeRefs = TypeRefs, Imports = Imports }, Positions = Positions };
            oslr.Verify();
            return oslr;
        }

        public void LoadSchema(String TreePath)
        {
            LoadType(TreePath);
        }
        public void LoadSchema(String TreePath, String Content)
        {
            LoadType(TreePath, Content);
        }

        public void AddImport(String Import)
        {
            Imports.Add(Import);
        }

        public void LoadType(String TreePath)
        {
            if (Debugger.IsAttached)
            {
                LoadType(TreePath, Txt.ReadFile(TreePath));
            }
            else
            {
                try
                {
                    LoadType(TreePath, Txt.ReadFile(TreePath));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidSyntaxException("", new FileTextRange { Text = new Text { Path = TreePath, Lines = new List<TextLine> { } }, Range = TreeFormat.Optional<TextRange>.Empty }, ex);
                }
            }
        }
        public void LoadType(String TreePath, String Content)
        {
            var t = TokenParser.BuildText(Content, TreePath);
            var fpr = FileParser.ParseFile(t);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
            }
            Types.AddRange(fpr.Types);
            TypeRefs.AddRange(fpr.TypeRefs);
            Imports.AddRange(fpr.Imports);
            foreach (var p in fpr.TypeToNamespace)
            {
                TypeToNamespace.Add(p.Key, p.Value);
            }
            foreach (var p in fpr.TypeToNamespaceImports)
            {
                TypeToNamespaceImports.Add(p.Key, p.Value);
            }
        }
        public void LoadTypeRef(String TreePath)
        {
            if (Debugger.IsAttached)
            {
                LoadTypeRef(TreePath, Txt.ReadFile(TreePath));
            }
            else
            {
                try
                {
                    LoadTypeRef(TreePath, Txt.ReadFile(TreePath));
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidSyntaxException("", new FileTextRange { Text = new Text { Path = TreePath, Lines = new List<TextLine> { } }, Range = TreeFormat.Optional<TextRange>.Empty }, ex);
                }
            }
        }
        public void LoadTypeRef(String TreePath, String Content)
        {
            var t = TokenParser.BuildText(Content, TreePath);
            var fpr = FileParser.ParseFile(t);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
            }
            TypeRefs.AddRange(fpr.Types);
            TypeRefs.AddRange(fpr.TypeRefs);
            Imports.AddRange(fpr.Imports);
            foreach (var p in fpr.TypeToNamespace)
            {
                TypeToNamespace.Add(p.Key, p.Value);
            }
            foreach (var p in fpr.TypeToNamespaceImports)
            {
                TypeToNamespaceImports.Add(p.Key, p.Value);
            }
        }

        private void ResolveNamespaces()
        {
            var TypeDictionary = new HashSet<String>();
            List<String> ResolveTypeDefName(TypeDef t, List<String> Name, String Version)
            {
                var FullName = Name;
                if (TypeToNamespace.ContainsKey(t))
                {
                    FullName = TypeToNamespace[t].Concat(Name).ToList();
                }
                TypeDictionary.Add((new TypeRef { Name = FullName, Version = Version }).VersionedName());
                return FullName;
            }
            List<String> ResolveTypeRefName(TypeDef t, TypeRef Ref, List<String> Name, String Version)
            {
                if (TypeToNamespace.ContainsKey(t))
                {
                    var CandidateFullName = TypeToNamespace[t].Concat(Name).ToList();
                    if (TypeDictionary.Contains((new TypeRef { Name = CandidateFullName, Version = Version }).VersionedName()))
                    {
                        return CandidateFullName;
                    }
                }
                if (TypeToNamespaceImports.ContainsKey(t))
                {
                    foreach (var Namespace in TypeToNamespaceImports[t])
                    {
                        var CandidateFullName = Namespace.Concat(Name).ToList();
                        if (TypeDictionary.Contains((new TypeRef { Name = CandidateFullName, Version = Version }).VersionedName()))
                        {
                            return CandidateFullName;
                        }
                    }
                }
                return Name;
            }

            var conf = new TypeMapConfiguration
            {
                MapTypeDefKernel = t =>
                {
                    if (t.OnPrimitive)
                    {
                        var p = t.Primitive;
                        return TypeDef.CreatePrimitive(new PrimitiveDef { Name = ResolveTypeDefName(t, p.Name, ""), GenericParameters = p.GenericParameters, Attributes = p.Attributes, Description = p.Description });
                    }
                    else if (t.OnAlias)
                    {
                        var a = t.Alias;
                        return TypeDef.CreateAlias(new AliasDef { Name = ResolveTypeDefName(t, a.Name, a.Version), Version = a.Version, GenericParameters = a.GenericParameters, Type = a.Type, Attributes = a.Attributes, Description = a.Description });
                    }
                    else if (t.OnRecord)
                    {
                        var r = t.Record;
                        return TypeDef.CreateRecord(new RecordDef { Name = ResolveTypeDefName(t, r.Name, r.Version), Version = r.Version, GenericParameters = r.GenericParameters, Fields = r.Fields, Attributes = r.Attributes, Description = r.Description });
                    }
                    else if (t.OnTaggedUnion)
                    {
                        var tu = t.TaggedUnion;
                        return TypeDef.CreateTaggedUnion(new TaggedUnionDef { Name = ResolveTypeDefName(t, tu.Name, tu.Version), Version = tu.Version, GenericParameters = tu.GenericParameters, Alternatives = tu.Alternatives, Attributes = tu.Attributes, Description = tu.Description });
                    }
                    else if (t.OnEnum)
                    {
                        var e = t.Enum;
                        return TypeDef.CreateEnum(new EnumDef { Name = ResolveTypeDefName(t, e.Name, e.Version), Version = e.Version, UnderlyingType = e.UnderlyingType, Literals = e.Literals, Attributes = e.Attributes, Description = e.Description });
                    }
                    else if (t.OnClientCommand)
                    {
                        var cc = t.ClientCommand;
                        return TypeDef.CreateClientCommand(new ClientCommandDef { Name = ResolveTypeDefName(t, cc.Name, cc.Version), Version = cc.Version, OutParameters = cc.OutParameters, InParameters = cc.InParameters, Attributes = cc.Attributes, Description = cc.Description });
                    }
                    else if (t.OnServerCommand)
                    {
                        var sc = t.ServerCommand;
                        return TypeDef.CreateServerCommand(new ServerCommandDef { Name = ResolveTypeDefName(t, sc.Name, sc.Version), Version = sc.Version, OutParameters = sc.OutParameters, Attributes = sc.Attributes, Description = sc.Description });
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                },
                TypeDefMarker = MarkTypeDef,
                TypeSpecMarker = Mark,
                VariableDefMarker = Mark
            };
            Types = Types.Select(t => t.MapType(conf)).ToList();
            TypeRefs = TypeRefs.Select(t => t.MapType(conf)).ToList();

            var confTypeSpec = new TypeMapConfiguration
            {
                MapTypeSpecKernel = (t, ts) =>
                {
                    if (ts.OnTypeRef)
                    {
                        return TypeSpec.CreateTypeRef(new TypeRef { Name = ResolveTypeRefName(t, ts.TypeRef, ts.TypeRef.Name, ts.TypeRef.Version), Version = ts.TypeRef.Version });
                    }
                    else if (ts.OnGenericParameterRef)
                    {
                        return ts;
                    }
                    else if (ts.OnTuple)
                    {
                        return ts;
                    }
                    else if (ts.OnGenericTypeSpec)
                    {
                        return ts;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                },
                TypeDefMarker = Mark,
                TypeSpecMarker = Mark,
                VariableDefMarker = Mark
            };
            Types = Types.Select(t => t.MapType(confTypeSpec)).ToList();
            TypeRefs = TypeRefs.Select(t => t.MapType(confTypeSpec)).ToList();

            TypeToNamespace = new Dictionary<TypeDef, List<String>>();
            TypeToNamespaceImports = new Dictionary<TypeDef, List<List<String>>>();
        }
        private T Mark<T>(T sOld, T sNew)
        {
            if (sOld == null) { throw new ArgumentNullException(nameof(sOld)); }
            if (sNew == null) { throw new ArgumentNullException(nameof(sNew)); }

            if (Positions.ContainsKey(sOld) && !Positions.ContainsKey(sNew))
            {
                Positions.Add(sNew, Positions[sOld]);
            }
            return sNew;
        }
        private TypeDef MarkTypeDef(TypeDef sOld, TypeDef sNew)
        {
            if (Positions.ContainsKey(sOld) && !Positions.ContainsKey(sNew))
            {
                Positions.Add(sNew, Positions[sOld]);
            }
            if (TypeToNamespace.ContainsKey(sOld) && !TypeToNamespace.ContainsKey(sNew))
            {
                TypeToNamespace.Add(sNew, TypeToNamespace[sOld]);
            }
            if (TypeToNamespaceImports.ContainsKey(sOld) && !TypeToNamespaceImports.ContainsKey(sNew))
            {
                TypeToNamespaceImports.Add(sNew, TypeToNamespaceImports[sOld]);
            }
            return sNew;
        }
    }
}
