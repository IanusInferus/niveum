using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;

namespace Database
{
    class LoadTest
    {
        public static void TestSaveData(int NumUser, int n, TestService s)
        {
            s.SaveData(n, n);
        }

        public static void TestLoadData(int NumUser, int n, TestService s)
        {
            var v = s.LoadData(n);
            Trace.Assert(v == n);
        }

        public static void TestSaveAndLoadData(int NumUser, int n, TestService s)
        {
            s.SaveData(n, n);
            var v = s.LoadData(n);
            Trace.Assert(v == n);
        }

        public static void TestAddLockData(int NumUser, int n, TestService s)
        {
            s.AddLockData(1);
        }

        public static void TestForNumUser(DataAccessManager dam, int NumUser, String Title, Action<int, int, TestService> Test)
        {
            ThreadLocal<TestService> t = new ThreadLocal<TestService>(() => new TestService(dam));

            var Time = Environment.TickCount;
            Parallel.For(0, NumUser, i => Test(NumUser, i, t.Value));
            var TimeDiff = Environment.TickCount - Time;

            if (Title == "") { return; }
            Console.WriteLine("{0}: {1} Users, {2} ms", Title, NumUser, TimeDiff);
        }

        public static int DoTest(DataAccessManager dam)
        {
            var t = new TestService(dam);

            TestForNumUser(dam, 64, "TestSaveData", TestSaveData);
            TestForNumUser(dam, 64, "TestLoadData", TestLoadData);
            TestForNumUser(dam, 64, "TestSaveAndLoadData", TestSaveAndLoadData);

            t.SaveLockData(0);
            TestForNumUser(dam, 64, "TestAddLockData", TestAddLockData);
            Trace.Assert(t.LoadLockData() == 64);

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), "TestSaveData", TestSaveData);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), "TestLoadData", TestLoadData);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), "TestSaveAndLoadData", TestSaveAndLoadData);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                var NumUser = 1 << (2 * k);
                t.SaveLockData(0);
                TestForNumUser(dam, NumUser, "TestAddLockData", TestAddLockData);
                Trace.Assert(t.LoadLockData() == NumUser);
            }

            return 0;
        }
    }
}
