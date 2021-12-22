//==========================================================================
//
//  File:        ExpressionSchemaLoader.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 表达式Schema加载器
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using Semantics = Firefly.Texting.TreeFormat.Semantics;
using TreeFormat = Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;

namespace Niveum.ExpressionSchema
{
    public class ExpressionSchemaLoader
    {
        private List<ModuleDecl> Modules = new List<ModuleDecl>();
        private List<String> Imports = new List<String>();

        public ExpressionSchemaLoader()
        {
        }

        public Schema GetResult()
        {
            var Result = new Schema { Modules = Modules, Imports = Imports };
            return Result;
        }
        
        public void AddImport(String Import)
        {
            Imports.Add(Import);
        }

        public void LoadType(String TreePath)
        {
            Load(TreePath);
        }
        public void LoadType(String TreePath, StreamReader Reader)
        {
            Load(TreePath, Reader);
        }
        private void Load(String TreePath)
        {
            using (var Reader = Txt.CreateTextReader(TreePath))
            {
                if (Debugger.IsAttached)
                {
                    Load(TreePath, Reader);
                }
                else
                {
                    try
                    {
                        Load(TreePath, Reader);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new List<Syntax.TextLine> { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        private void Load(String TreePath, StreamReader Reader)
        {
            var ps = new TreeFormatParseSetting
            {
                IsTableParameterFunction = Name => Name == "Module"
            };

            var es = new TreeFormatEvaluateSetting
            {
                FunctionCallEvaluator = (f, nm) =>
                {
                    if (f.Parameters.Count < 1 || f.Parameters.Count > 2) { throw new Syntax.InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                    var Name = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");
                    String Description = "";
                    if (f.Parameters.Count == 2) { Description = GetLeafNodeValue(f.Parameters[1], nm, "InvalidDescription"); }

                    var ContentLines = new List<Syntax.TextLine> { };
                    if (f.Content.OnSome)
                    {
                        var ContentValue = f.Content.Value;
                        if (!ContentValue.OnLineContent) { throw new Syntax.InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                        ContentLines = ContentValue.LineContent.Lines;
                    }

                    if (f.Name.Text == "Module")
                    {
                        var Functions = new List<FunctionDecl>();
                        foreach (var Line in ContentLines)
                        {
                            var Trimmed = Line.Text.Trim();
                            if (Trimmed == "") { continue; }
                            if (Trimmed.StartsWith("//")) { continue; }
                            var epdr = ExpressionParser.ParseDeclaration(nm.Text, Line.Range);
                            Functions.Add(epdr.Declaration);
                        }
                        Modules.Add(new ModuleDecl { Name = Name, Description = Description, Functions = Functions });
                    }
                    else
                    {
                        throw new Syntax.InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                    }
                    return new List<Node> { };
                }
            };
            var pr = TreeFile.ReadRaw(Reader, TreePath, ps);
            var tfe = new TreeFormatEvaluator(es, pr);
            tfe.Evaluate();
        }

        private static String GetLeafNodeValue(Semantics.Node n, ISemanticsNodeMaker nm, String ErrorCause)
        {
            if (!n.OnLeaf) { throw new Syntax.InvalidEvaluationException(ErrorCause, nm.GetFileRange(n), n); }
            return n.Leaf;
        }
    }
}
