using System;

namespace Firefly.Test
{
    public static class TestAssert
    {
        public static void IsTrue(bool Condition)
        {
            if (!Condition) throw new AssertionFailedException("Assertion failed.");
        }
        public static void IsTrue(bool Condition, string Message)
        {
            if (!Condition) throw new AssertionFailedException(Message);
        }
    }

    public class AssertionFailedException : Exception
    {
        public AssertionFailedException(string Message) : base(Message) { }
    }
}
