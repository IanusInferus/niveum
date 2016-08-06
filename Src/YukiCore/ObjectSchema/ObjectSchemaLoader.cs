//==========================================================================
//
//  File:        ObjectSchemaLoader.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构加载器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat.Syntax;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema
{
    public class ObjectSchemaLoaderResult
    {
        public Schema Schema;
        public Dictionary<Object, FileTextRange> Positions;
    }

    public sealed class ObjectSchemaLoader
    {
        private List<TypeDef> Types = new List<TypeDef>();
        private List<TypeDef> TypeRefs = new List<TypeDef>();
        private List<String> Imports = new List<String>();
        private Dictionary<Object, FileTextRange> Positions = new Dictionary<Object, FileTextRange>();

        public ObjectSchemaLoaderResult GetResult()
        {
            return new ObjectSchemaLoaderResult { Schema = new Schema { Types = Types, TypeRefs = TypeRefs, Imports = Imports }, Positions = Positions };
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
            Types.AddRange(fpr.Schema.Types);
            TypeRefs.AddRange(fpr.Schema.TypeRefs);
            Imports.AddRange(fpr.Schema.Imports);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
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
            TypeRefs.AddRange(fpr.Schema.Types);
            TypeRefs.AddRange(fpr.Schema.TypeRefs);
            Imports.AddRange(fpr.Schema.Imports);
            foreach (var p in fpr.Positions)
            {
                Positions.Add(p.Key, new FileTextRange { Text = t, Range = p.Value });
            }
        }

        public static String GetTypeFriendlyNameFromVersionedName(String VersionedName)
        {
            var r = TypeParser.ParseTypeRef(VersionedName);
            return (new TypeRef { Name = r.Name, Version = r.Version }).TypeFriendlyName();
        }
    }
}
