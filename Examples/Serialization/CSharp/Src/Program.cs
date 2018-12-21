//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.Examples <Visual C#>
//  Description: 数据转换工具
//  Version:     2018.12.22.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

extern alias niveum;

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Streaming;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using niveum::Niveum;
using niveum::Niveum.ObjectSchema;

namespace DataConv
{
    public static class Program
    {
        public static int Main()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return MainInner();
            }
            else
            {
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                    return -1;
                }
            }
        }

        public static int MainInner()
        {
            TextEncoding.WritingDefault = TextEncoding.UTF8;

            var CmdLine = CommandLine.GetCmdLine();
            var argv = CmdLine.Arguments;

            if (CmdLine.Arguments.Length != 0)
            {
                DisplayInfo();
                return -1;
            }

            if (CmdLine.Options.Length == 0)
            {
                DisplayInfo();
                return 0;
            }

            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (opt.Name.ToLower() == "t2b")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        TreeToBinary(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "b2t")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        BinaryToTree(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2j")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        TreeToJson(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "j2t")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        JsonToTree(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else
                {
                    throw new ArgumentException(opt.Name);
                }
            }
            return 0;
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"数据转换工具");
            Console.WriteLine(@"DataConv，Public Domain");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"DataConv (/<Command>)*");
            Console.WriteLine(@"将Tree格式数据转化为二进制数据");
            Console.WriteLine(@"/t2b:<TreeFile>,<BinaryFile>");
            Console.WriteLine(@"将二进制数据转化为Tree格式数据");
            Console.WriteLine(@"/b2t:<BinaryFile>,<TreeFile>");
            Console.WriteLine(@"将Tree格式数据转化为JSON数据");
            Console.WriteLine(@"/t2j:<TreeFile>,<JsonFile>");
            Console.WriteLine(@"将JSON数据转化为Tree格式数据");
            Console.WriteLine(@"/j2t:<JsonFile>,<TreeFile>");
            Console.WriteLine(@"TreeFile Tree文件路径。");
            Console.WriteLine(@"BinaryFile 二进制文件路径。");
            Console.WriteLine(@"JsonFile JSON文件路径。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"DataConv /t2b:..\..\Data\WorldData.tree,..\Data\WorldData.bin");
            Console.WriteLine(@"将WorldData.tree转化为WorldData.bin。");
        }

        public static void TreeToBinaryWithFirefly(String TreePath, String BinaryPath)
        {
            var tbc = new TreeBinaryConverter();
            var Data = TreeFile.ReadFile(TreePath);
            var b = tbc.TreeToBinary<World.World>(Data);
            using (var s = Streams.CreateWritable(BinaryPath))
            {
                s.Write(b);
            }
        }

        public static void BinaryToTreeWithFirefly(String BinaryPath, String TreePath)
        {
            var tbc = new TreeBinaryConverter();
            Byte[] Data;
            using (var s = Streams.OpenReadable(BinaryPath))
            {
                Data = s.Read((int)(s.Length));
            }
            var x = tbc.BinaryToTree<World.World>(Data);
            TreeFile.WriteFile(TreePath, x);
        }

        public static void TreeToBinary(String TreePath, String BinaryPath)
        {
            var xs = new XmlSerializer(true);
            var x = TreeFile.ReadFile(TreePath);
            var o = xs.Read<World.World>(x);
            Byte[] b;
            using (var s = new World.BinaryWithoutFirefly.ByteArrayStream())
            {
                World.BinaryWithoutFirefly.BinaryTranslator.WorldToBinary(s, o);
                s.Position = 0;
                b = s.ReadBytes(s.Length);
            }
            using (var s = Streams.CreateWritable(BinaryPath))
            {
                s.Write(b);
            }
        }

        public static void BinaryToTree(String BinaryPath, String TreePath)
        {
            Byte[] b;
            using (var s = Streams.OpenReadable(BinaryPath))
            {
                b = s.Read((int)(s.Length));
            }
            World.World o;
            using (var s = new World.BinaryWithoutFirefly.ByteArrayStream())
            {
                s.WriteBytes(b);
                s.Position = 0;
                o = World.BinaryWithoutFirefly.BinaryTranslator.WorldFromBinary(s);
            }
            var xs = new XmlSerializer(true);
            var x = xs.Write<World.World>(o);
            TreeFile.WriteFile(TreePath, x);
        }

        public static void TreeToJson(String TreePath, String JsonPath)
        {
            var xs = new XmlSerializer(true);
            var x = TreeFile.ReadFile(TreePath);
            var o = xs.Read<World.World>(x);
            var j = World.Json.JsonTranslator.WorldToJson(o);
            var t = j.ToString(Niveum.Json.Formatting.Indented);
            Txt.WriteFile(JsonPath, t);
        }

        public static void JsonToTree(String JsonPath, String TreePath)
        {
            var xs = new XmlSerializer(true);
            var t = Txt.ReadFile(JsonPath);
            var j = Niveum.Json.JToken.Parse(t);
            var o = World.Json.JsonTranslator.WorldFromJson(j);
            var x = xs.Write<World.World>(o);
            TreeFile.WriteFile(TreePath, x);
        }
    }
}
