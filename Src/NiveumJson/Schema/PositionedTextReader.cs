//==========================================================================
//
//  File:        PositionedTextReader.cs
//  Location:    Niveum.Json <Visual C#>
//  Description: 带位置的文本读取器
//  Version:     2018.09.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;

namespace Niveum.Json.Syntax
{
    public struct TextPosition
    {
        public readonly int CharIndex;
        public readonly int Row;
        public readonly int Column;
        public TextPosition(int CharIndex, int Row, int Column)
        {
            this.CharIndex = CharIndex;
            this.Row = Row;
            this.Column = Column;
        }
        public override string ToString()
        {
            return $"({Row}, {Column})";
        }
    }

    public struct TextRange
    {
        public readonly TextPosition Start;
        public readonly TextPosition End;
        public TextRange(TextPosition Start, TextPosition End)
        {
            this.Start = Start;
            this.End = End;
        }
        public override string ToString()
        {
            return Start.ToString() + "-" + End.ToString();
        }
    }

    public sealed class PositionedTextReader : IDisposable
    {
        public Optional<String> FilePath { get; private set; }
        public System.IO.TextReader InnerReader { get; private set; }
        public TextPosition CurrentPosition { get; private set; }

        public PositionedTextReader(Optional<String> FilePath, System.IO.TextReader InnerReader)
        {
            this.FilePath = FilePath;
            this.InnerReader = InnerReader;
            this.CurrentPosition = new TextPosition(0, 1, 1);
        }

        public bool EndOfText
        {
            get
            {
                return InnerReader.Peek() == -1;
            }
        }
        public Char Peek()
        {
            var i = InnerReader.Peek();
            if (i == -1) { throw new InvalidOperationException("EndOfText"); }
            var c = (Char)(i);
            return c;
        }
        public Char Read()
        {
            var i = InnerReader.Read();
            if (i == -1) { throw new InvalidOperationException("EndOfText"); }
            var c = (Char)(i);
            if (c == '\n')
            {
                CurrentPosition = new TextPosition(CurrentPosition.CharIndex + 1, CurrentPosition.Row + 1, 1);
            }
            else if (c == '\t')
            {
                CurrentPosition = new TextPosition(CurrentPosition.CharIndex + 1, CurrentPosition.Row, (CurrentPosition.Column + 3) / 4 * 4 + 1);
            }
            else
            {
                CurrentPosition = new TextPosition(CurrentPosition.CharIndex + 1, CurrentPosition.Row, CurrentPosition.Column + 1);
            }
            return c;
        }

        public void Dispose()
        {
            if (InnerReader != null)
            {
                InnerReader.Dispose();
                InnerReader = null;
            }
        }
    }
}