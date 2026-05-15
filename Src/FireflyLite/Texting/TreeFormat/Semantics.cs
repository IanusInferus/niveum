using System;
using System.Collections.Generic;
using System.Diagnostics;
using Firefly.Mapping.MetaSchema;

namespace Firefly.Texting.TreeFormat.Semantics
{
    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class Forest
    {
        public List<Node> Nodes;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    public enum NodeTag
    {
        Empty,
        Leaf,
        Stem
    }
    [TaggedUnion]
    [DebuggerDisplay("{ToString()}")]
    public class Node
    {
        [Tag] public NodeTag _Tag;
        public Unit Empty;
        public string Leaf;
        public Stem Stem;

        public static Node CreateEmpty()
        {
            return new Node { _Tag = NodeTag.Empty, Empty = new Unit() };
        }
        public static Node CreateLeaf(string Value)
        {
            return new Node { _Tag = NodeTag.Leaf, Leaf = Value };
        }
        public static Node CreateStem(Stem Value)
        {
            return new Node { _Tag = NodeTag.Stem, Stem = Value };
        }

        public bool OnEmpty
        {
            get { return _Tag == NodeTag.Empty; }
        }
        public bool OnLeaf
        {
            get { return _Tag == NodeTag.Leaf; }
        }
        public bool OnStem
        {
            get { return _Tag == NodeTag.Stem; }
        }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }

    [Record]
    [DebuggerDisplay("{ToString()}")]
    public class Stem
    {
        public string Name;
        public List<Node> Children;

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
}
