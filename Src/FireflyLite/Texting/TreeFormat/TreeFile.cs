using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Mapping.MetaSchema;

namespace Firefly.Texting.TreeFormat
{
    public sealed class TreeFile
    {
        public static TreeFormatParseResult ReadRaw(string Path, TreeFormatParseSetting ParseSetting)
        {
            return ReadRaw(Path, TextEncoding.TextEncoding.Default, ParseSetting);
        }
        public static TreeFormatParseResult ReadRaw(string Path, Encoding Encoding, TreeFormatParseSetting ParseSetting)
        {
            using (var sr = Txt.CreateTextReader(Path, Encoding))
            {
                var t = Txt.ReadFile(sr);
                var Text = TreeFormatTokenParser.BuildText(t, Path);
                var tfp = new TreeFormatSyntaxParser(ParseSetting, Text);
                if (Debugger.IsAttached)
                {
                    return tfp.Parse();
                }
                else
                {
                    try
                    {
                        return tfp.Parse();
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new Syntax.InvalidSyntaxException("", new Syntax.FileTextRange { Text = tfp.Text, Range = Optional<Syntax.TextRange>.Empty }, ex);
                    }
                }
            }
        }
        public static TreeFormatParseResult ReadRaw(StreamReader Reader, TreeFormatParseSetting ParseSetting)
        {
            var t = Txt.ReadFile(Reader);
            var Text = TreeFormatTokenParser.BuildText(t, "");
            var tfp = new TreeFormatSyntaxParser(ParseSetting, Text);
            return tfp.Parse();
        }
        public static TreeFormatParseResult ReadRaw(StreamReader Reader, string Path, TreeFormatParseSetting ParseSetting)
        {
            var t = Txt.ReadFile(Reader);
            var Text = TreeFormatTokenParser.BuildText(t, Path);
            var tfp = new TreeFormatSyntaxParser(ParseSetting, Text);
            return tfp.Parse();
        }

        public static TreeFormatResult ReadDirect(string Path, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            return ReadDirect(Path, TextEncoding.TextEncoding.Default, ParseSetting, EvaluateSetting);
        }
        public static TreeFormatResult ReadDirect(string Path, Encoding Encoding, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var pr = ReadRaw(Path, Encoding, ParseSetting);
            var tfe = new TreeFormatEvaluator(EvaluateSetting, pr);
            return tfe.Evaluate();
        }
        public static TreeFormatResult ReadDirect(StreamReader Reader, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var pr = ReadRaw(Reader, ParseSetting);
            var tfe = new TreeFormatEvaluator(EvaluateSetting, pr);
            return tfe.Evaluate();
        }
        public static TreeFormatResult ReadDirect(StreamReader Reader, string Path, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var pr = ReadRaw(Reader, Path, ParseSetting);
            var tfe = new TreeFormatEvaluator(EvaluateSetting, pr);
            return tfe.Evaluate();
        }

        public static XElement ReadFile(string Path, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            return ReadFile(Path, TextEncoding.TextEncoding.Default, ParseSetting, EvaluateSetting);
        }
        public static XElement ReadFile(string Path, Encoding Encoding, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var er = ReadDirect(Path, Encoding, ParseSetting, EvaluateSetting);
            return XmlInterop.TreeToXml(er);
        }
        public static XElement ReadFile(StreamReader Reader, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var er = ReadDirect(Reader, ParseSetting, EvaluateSetting);
            return XmlInterop.TreeToXml(er);
        }
        public static XElement ReadFile(StreamReader Reader, string Path, TreeFormatParseSetting ParseSetting, TreeFormatEvaluateSetting EvaluateSetting)
        {
            var er = ReadDirect(Reader, Path, ParseSetting, EvaluateSetting);
            return XmlInterop.TreeToXml(er);
        }

        public static XElement ReadFile(string Path)
        {
            return ReadFile(Path, TextEncoding.TextEncoding.Default);
        }
        public static XElement ReadFile(string Path, Encoding Encoding)
        {
            var er = ReadDirect(Path, Encoding, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            return XmlInterop.TreeToXml(er);
        }
        public static XElement ReadFile(StreamReader Reader)
        {
            var er = ReadDirect(Reader, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            return XmlInterop.TreeToXml(er);
        }
        public static XElement ReadFile(StreamReader Reader, string Path)
        {
            var er = ReadDirect(Reader, Path, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
            return XmlInterop.TreeToXml(er);
        }

        public static void WriteRaw(string Path, Syntax.Forest Value)
        {
            WriteRaw(Path, TextEncoding.TextEncoding.WritingDefault, Value);
        }
        public static void WriteRaw(string Path, Encoding Encoding, Syntax.Forest Value)
        {
            using (var sw = Txt.CreateTextWriter(Path, Encoding))
            {
                WriteRaw(sw, Value);
            }
        }
        public static void WriteRaw(StreamWriter Writer, Syntax.Forest Value)
        {
            var w = new TreeFormatSyntaxWriter(Writer);
            w.Write(Value);
        }

        public static void WriteDirect(string Path, Semantics.Forest Value)
        {
            WriteDirect(Path, TextEncoding.TextEncoding.WritingDefault, Value);
        }
        public static void WriteDirect(string Path, Encoding Encoding, Semantics.Forest Value)
        {
            using (var sw = Txt.CreateTextWriter(Path, Encoding))
            {
                WriteDirect(sw, Value);
            }
        }
        public static void WriteDirect(StreamWriter Writer, Semantics.Forest Value)
        {
            var w = new TreeFormatWriter(Writer);
            w.Write(Value);
        }

        public static void WriteFile(string Path, XElement Value)
        {
            WriteFile(Path, TextEncoding.TextEncoding.WritingDefault, Value);
        }
        public static void WriteFile(string Path, Encoding Encoding, XElement Value)
        {
            using (var sw = Txt.CreateTextWriter(Path, Encoding))
            {
                WriteFile(sw, Value);
            }
        }
        public static void WriteFile(StreamWriter Writer, XElement Value)
        {
            var v = XmlInterop.XmlToTreeRaw(Value);
            WriteRaw(Writer, v.Value);
        }
    }
}
