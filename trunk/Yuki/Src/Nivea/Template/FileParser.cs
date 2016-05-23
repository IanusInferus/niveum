//==========================================================================
//
//  File:        FileParser.cs
//  Location:    Nivea <Visual C#>
//  Description: 文件解析器
//  Version:     2016.05.23.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Syntax;

namespace Nivea.Template
{
    public class FileParserResult
    {
        public Semantics.File File;
        public Dictionary<Object, TextRange> Positions;
    }

    public static class FileParser
    {
        public static FileParserResult ParseFile(Text Text)
        {
            var Functions = new HashSet<String>((new List<String>() { "Primitive", "Alias", "Record", "TaggedUnion", "Enum" }).Concat(new List<String>() { "Namespace", "Assembly", "Import", "Template", "Global" }));
            var FreeContentFunctions = new HashSet<String> { "Template", "Global" };

            var ps = new TreeFormatParseSetting()
            {
                IsTableParameterFunction = Name => Functions.Contains(Name),
                IsTableContentFunction = Name => Functions.Contains(Name) && !FreeContentFunctions.Contains(Name)
            };

            var sp = new TreeFormatSyntaxParser(ps, Text);
            var ParserResult = sp.Parse();
            var ts = new TreeSerializer();

            var Sections = new List<Semantics.SectionDef>();
            var Positions = new Dictionary<Object, TextRange>();
            foreach (var TopNode in ParserResult.Value.MultiNodesList)
            {
                if (TopNode.OnFunctionNodes)
                {

                }
                else
                {
                    var pr = new TreeFormatParseResult
                    {
                        Value = new Forest { MultiNodesList = new MultiNodes[] { TopNode } },
                        Text = Text,
                        Positions = ParserResult.Positions,
                        RawFunctionCalls = ParserResult.RawFunctionCalls
                    };
                    var es = new TreeFormatEvaluateSetting { };
                    var e = new TreeFormatEvaluator(es, pr);
                    var er = e.Evaluate();
                    var ReadResult = ts.Read<Semantics.File>(CollectionOperations.CreatePair(er.Value, er.Positions));
                    Sections.AddRange(ReadResult.Key.Sections);
                    foreach (var p in ReadResult.Value)
                    {
                        if (p.Value.Range.OnHasValue)
                        {
                            Positions.Add(p.Key, p.Value.Range.Value);
                        }
                    }
                }
            }

            var File = new Semantics.File { Sections = Sections };
            return new FileParserResult { File = File, Positions = Positions };
        }
    }
}
