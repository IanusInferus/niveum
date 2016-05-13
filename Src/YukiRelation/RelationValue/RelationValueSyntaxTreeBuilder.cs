//==========================================================================
//
//  File:        RelationValueSyntaxTreeBuilder.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构数据语法Tree构建器
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;
using Semantics = Firefly.Texting.TreeFormat.Semantics;
using Yuki.RelationSchema;
using TreeFormat = Firefly.Texting.TreeFormat;

namespace Yuki.RelationValue
{
    public static class RelationValueSyntaxTreeBuilder
    {
        public static MultiNodes BuildTable(EntityDef e, List<Semantics.Node> l)
        {
            var Lines = l.Select(n => new TableLine { Nodes = n.Stem.Children.Select(c => TableLineNode.CreateSingleLineLiteral(new SingleLineLiteral { Text = c.Stem.Children.Single().Leaf })).ToArray(), SingleLineComment = TreeFormat.Optional<SingleLineComment>.Empty }).ToList();
            var TableNodes = new TableNodes
            {
                ChildHead = new SingleLineLiteral { Text = e.Name },
                ChildFields = e.Fields.Where(f => f.Attribute.OnColumn).Select(c => new SingleLineLiteral { Text = c.Name }).ToArray(),
                SingleLineComment = TreeFormat.Optional<SingleLineComment>.Empty,
                Children = Lines.ToArray(),
                EndDirective = TreeFormat.Optional<EndDirective>.Empty
            };
            var Table = new MultiLineNode
            {
                Head = new SingleLineLiteral { Text = e.CollectionName },
                SingleLineComment = TreeFormat.Optional<SingleLineComment>.Empty,
                Children = new MultiNodes[] { MultiNodes.CreateTableNodes(TableNodes) },
                EndDirective = TreeFormat.Optional<EndDirective>.Empty
            };
            return MultiNodes.CreateNode(Node.CreateMultiLineNode(Table));
        }
    }
}
