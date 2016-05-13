//==========================================================================
//
//  File:        ExpressionAssemblyLoader.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式函数集加载器
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Firefly;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Syntax = Firefly.Texting.TreeFormat.Syntax;
using Semantics = Firefly.Texting.TreeFormat.Semantics;
using TreeFormat = Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;

namespace Yuki.ExpressionSchema
{
    public class ExpressionAssemblyLoader
    {
        private UInt64 Hash;
        private Dictionary<String, ModuleDecl> ModuleDecls;
        private Dictionary<String, Dictionary<String, FunctionDecl>> ModuleDeclDicts;
        private List<ModuleDef> Modules = new List<ModuleDef>();

        public ExpressionAssemblyLoader(Schema s)
        {
            Hash = s.Hash();
            ModuleDecls = s.Modules.ToDictionary(m => m.Name);
            ModuleDeclDicts = s.Modules.ToDictionary(m => m.Name, m => m.Functions.ToDictionary(f => f.Name));
        }

        public Assembly GetResult()
        {
            var Result = new Assembly { Hash = Hash, Modules = Modules };
            return Result;
        }

        public void LoadAssembly(String TreePath)
        {
            Load(TreePath);
        }
        public void LoadAssembly(String TreePath, StreamReader Reader)
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
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = new Syntax.Text { Path = TreePath, Lines = new Syntax.TextLine[] { } }, Range = TreeFormat.Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }

        private static Regex rFunctionBodyLine = new Regex(@"^ *(?<Name>[A-Za-z_][A-Za-z0-9_]*) +(?<Body>.*?)( +//.*)?$", RegexOptions.ExplicitCapture);
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
                    if (f.Parameters.Length < 1 || f.Parameters.Length > 2) { throw new Syntax.InvalidEvaluationException("InvalidParameterCount", nm.GetFileRange(f), f); }

                    var Name = GetLeafNodeValue(f.Parameters[0], nm, "InvalidName");

                    String Description = "";
                    if (f.Parameters.Length >= 2)
                    {
                        var DescriptionParameter = f.Parameters[1];
                        if (!DescriptionParameter.OnLeaf) { throw new Syntax.InvalidEvaluationException("InvalidDescription", nm.GetFileRange(DescriptionParameter), DescriptionParameter); }
                        Description = DescriptionParameter.Leaf;
                    }

                    var ContentLines = new Syntax.TextLine[] { };
                    if (f.Content.HasValue)
                    {
                        var ContentValue = f.Content.Value;
                        if (!ContentValue.OnLineContent) { throw new Syntax.InvalidEvaluationException("InvalidContent", nm.GetFileRange(ContentValue), ContentValue); }
                        ContentLines = ContentValue.LineContent.Lines;
                    }

                    if (f.Name.Text == "Module")
                    {
                        if (!ModuleDecls.ContainsKey(Name)) { throw new Syntax.InvalidEvaluationException("ModuleNotExist", nm.GetFileRange(f), f); }
                        var Module = ModuleDecls[Name];
                        var FunctionDeclDict = ModuleDeclDicts[Name];

                        var ReachedFunctions = new HashSet<String>();
                        var Functions = new Dictionary<String, FunctionDef>();
                        foreach (var Line in ContentLines)
                        {
                            var Trimmed = Line.Text.Trim();
                            if (Trimmed == "") { continue; }
                            if (Trimmed.StartsWith("//")) { continue; }
                            var m = rFunctionBodyLine.Match(Line.Text);
                            if (!m.Success) { throw new Syntax.InvalidEvaluationException("FunctionBodyLineInvalid", new Syntax.FileTextRange { Text = nm.Text, Range = Line.Range }, Line); }
                            var FunctionName = m.Result("${Name}");
                            var gBody = m.Groups["Body"];

                            if (!FunctionDeclDict.ContainsKey(FunctionName)) { throw new Syntax.InvalidEvaluationException("FunctionNotExist", new Syntax.FileTextRange { Text = nm.Text, Range = Line.Range }, Line); }
                            if (ReachedFunctions.Contains(FunctionName)) { throw new Syntax.InvalidEvaluationException("DuplicateFunction", new Syntax.FileTextRange { Text = nm.Text, Range = Line.Range }, Line); }
                            var fd = FunctionDeclDict[FunctionName];
                            ReachedFunctions.Add(FunctionName);
                            var ep = new ExpressionParser(null, nm.Text);
                            var BodyRange = new Syntax.TextRange { Start = nm.Text.Calc(Line.Range.Start, gBody.Index), End = nm.Text.Calc(Line.Range.Start, gBody.Index + gBody.Length) };
                            var func = ep.ParseFunction(new Yuki.Expression.ExpressionRuntimeProvider<int>(), fd, BodyRange);
                            Functions.Add(FunctionName, func.Definition);
                        }

                        var NoDefinitionList = FunctionDeclDict.Keys.Except(ReachedFunctions).ToList();
                        if (NoDefinitionList.Count > 0)
                        {
                            throw new Syntax.InvalidEvaluationException("DefinitionNotFoundForFunction: {0}".Formats(String.Join(" ", NoDefinitionList.ToArray())), nm.GetFileRange(f), f);
                        }

                        var ModuleDef = new ModuleDef { Name = Name, Description = Description, Functions = Module.Functions.Select(fd => Functions[fd.Name]).ToList() };
                        Modules.Add(ModuleDef);
                    }
                    else
                    {
                        throw new Syntax.InvalidEvaluationException("UnknownFunction", nm.GetFileRange(f), f);
                    }

                    return new Node[] { };
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
