using System;
using Firefly.TextEncoding;

namespace Firefly.Test
{
    public static class BasicTests
    {
        public static void TestUnicode()
        {
            string c = Char32.ToString(0x20C30);
            TestAssert.IsTrue(String16.AscW(c[0]) == 0xD843);
            TestAssert.IsTrue(String16.AscW(c[1]) == 0xDC30);
            int code = Char32.FromString(c);
            TestAssert.IsTrue(String32.AscQ((Char32)c) == 0x20C30);
        }

        public static void TestCommandLine()
        {
            var CmdLine = CommandLine.ParseCmdLine("  test.exe  \"1 ,\" /t123:,123,,\", \",234,  ", true);

            TestAssert.IsTrue(CmdLine.Arguments.Length == 1);
            TestAssert.IsTrue(CmdLine.Arguments[0] == "1 ,");
            TestAssert.IsTrue(CmdLine.Options.Length == 1);
            TestAssert.IsTrue(CmdLine.Options[0].Name == "t123");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments.Length == 6);
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[0] == "");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[1] == "123");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[2] == "");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[3] == ", ");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[4] == "234");
            TestAssert.IsTrue(CmdLine.Options[0].Arguments[5] == "");
        }
    }
}
