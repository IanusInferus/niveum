using System;
using System.Collections.Generic;

namespace Firefly.Texting.TreeFormat
{
    public class TreeFormatResult
    {
        public Semantics.Forest Value;
        public Dictionary<object, Syntax.FileTextRange> Positions;
    }
}
