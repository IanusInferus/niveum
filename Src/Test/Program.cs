using System;
using System.Collections.Generic;

namespace Firefly.Test
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var Tests = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("TestUnicode", BasicTests.TestUnicode),
                new KeyValuePair<string, Action>("TestCommandLine", BasicTests.TestCommandLine),
                new KeyValuePair<string, Action>("TestMetaProgramming", MappingTests.TestMetaProgramming),
                new KeyValuePair<string, Action>("TestObjectTreeMapper", MappingTests.TestObjectTreeMapper),
                new KeyValuePair<string, Action>("TestBinarySerializer", MappingTests.TestBinarySerializer),
                new KeyValuePair<string, Action>("TestXmlSerializer", MappingTests.TestXmlSerializer),
                new KeyValuePair<string, Action>("TestXmlSerializerForDict", MappingTests.TestXmlSerializerForDict),
                new KeyValuePair<string, Action>("TestAlias", MappingTests.TestAlias),
                new KeyValuePair<string, Action>("TestTaggedUnion", MappingTests.TestTaggedUnion),
                new KeyValuePair<string, Action>("TestTuple", MappingTests.TestTuple),
                new KeyValuePair<string, Action>("TestMixed", MappingTests.TestMixed),
                new KeyValuePair<string, Action>("TestDebuggerDisplayer", MappingTests.TestDebuggerDisplayer),
                new KeyValuePair<string, Action>("TestRecursive", MappingTests.TestRecursive)
            };

            int Passed = 0;
            int Failed = 0;
            foreach (var t in Tests)
            {
                try
                {
                    t.Value();
                    Console.WriteLine("[PASS] " + t.Key);
                    Passed += 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[FAIL] " + t.Key + ": " + ex.GetType().Name + ": " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    Failed += 1;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Passed: " + Passed + ", Failed: " + Failed + ", Total: " + Tests.Count);
            return Failed == 0 ? 0 : 1;
        }
    }
}
