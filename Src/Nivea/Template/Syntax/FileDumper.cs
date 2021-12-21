//==========================================================================
//
//  File:        FileDumper.cs
//  Location:    Nivea <Visual C#>
//  Description: 文法解析结果导出器
//  Version:     2021.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.TreeText;
using TreeFormat = Firefly.Texting.TreeFormat;
using TFSemantics = Firefly.Texting.TreeFormat.Semantics;
using TFSyntax = Firefly.Texting.TreeFormat.Syntax;

namespace Nivea.Template.Syntax
{
    public class FileDumper
    {
        private TreeSerializer ts = new TreeSerializer();

        public TFSyntax.Forest Dump(FileParserResult Result, String Comment = "")
        {
            var sem = ts.Write(CollectionOperations.CreatePair(Result.File, Result.Positions.ToDictionary(p => p.Key, p => (Object)(p.Value))));
            var syn = TranslateForest(sem.Key, Comment, sem.Value);
            return syn;
        }

        private TFSyntax.Forest TranslateForest(TFSemantics.Forest f, String Comment, Dictionary<Object, Object> Positions)
        {
            var MultiNodesList = new List<TFSyntax.MultiNodes>();
            if (Comment != "")
            {
                var mlc = new TFSyntax.MultiLineComment { SingleLineComment = TreeFormat.Optional<TFSyntax.SingleLineComment>.Empty, Content = new TFSyntax.FreeContent { Text = Comment }, EndDirective = TreeFormat.Optional<TFSyntax.EndDirective>.Empty };
                MultiNodesList.Add(TFSyntax.MultiNodes.CreateNode(TFSyntax.Node.CreateMultiLineComment(mlc)));
            }
            foreach (var n in f.Nodes)
            {
                MultiNodesList.AddRange(TranslateNode(n, Positions));
            }
            return new TFSyntax.Forest { MultiNodesList = MultiNodesList };
        }

        private List<TFSyntax.MultiNodes> TranslateNode(TFSemantics.Node Node, Dictionary<Object, Object> Positions)
        {
            var MultiNodesList = new List<TFSyntax.MultiNodes>();
            if (Positions.ContainsKey(Node))
            {
                var o = Positions[Node];
                if (o is TFSyntax.TextRange)
                {
                    var Range = (TFSyntax.TextRange)(o);
                    var slc = new TFSyntax.SingleLineComment { Content = new TFSyntax.FreeContent { Text = Range.ToString() } };
                    MultiNodesList.Add(TFSyntax.MultiNodes.CreateNode(TFSyntax.Node.CreateSingleLineComment(slc)));
                }
            }
            if (Node.OnEmpty)
            {
                MultiNodesList.Add(TFSyntax.MultiNodes.CreateNode(TFSyntax.Node.CreateSingleLineNodeLine(new TFSyntax.SingleLineNodeLine { SingleLineNode = TFSyntax.SingleLineNode.CreateEmptyNode(new TFSyntax.EmptyNode { }) })));
            }
            else if (Node.OnLeaf)
            {
                MultiNodesList.Add(TFSyntax.MultiNodes.CreateNode(TFSyntax.Node.CreateSingleLineNodeLine(new TFSyntax.SingleLineNodeLine { SingleLineNode = TFSyntax.SingleLineNode.CreateSingleLineLiteral(new TFSyntax.SingleLineLiteral { Text = Node.Leaf }) })));
            }
            else if (Node.OnStem)
            {
                var Children = Node.Stem.Children.SelectMany(c => TranslateNode(c, Positions)).ToList();
                var EndDirective = Children.Count > 0 ? TreeFormat.Optional<TFSyntax.EndDirective>.Empty : new TFSyntax.EndDirective { EndSingleLineComment = TreeFormat.Optional<TFSyntax.SingleLineComment>.Empty };
                MultiNodesList.Add(TFSyntax.MultiNodes.CreateNode(TFSyntax.Node.CreateMultiLineNode(new TFSyntax.MultiLineNode { Head = new TFSyntax.SingleLineLiteral { Text = Node.Stem.Name }, SingleLineComment = TreeFormat.Optional<TFSyntax.SingleLineComment>.Empty, Children = Children, EndDirective = EndDirective })));
            }
            else
            {
                throw new InvalidOperationException();
            }
            return MultiNodesList;
        }
    }
}
