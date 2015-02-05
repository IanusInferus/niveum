using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Database
{
    class LoadTest
    {
        public static void TestSaveData(int NumUser, int n, TestService s)
        {
            var RetryCount = 0;
            while (RetryCount < 16)
            {
                try
                {
                    s.SaveData(n, n);
                    return;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    if ((ex.Code == "40002") || (ex.Code == "40004"))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
        }

        public static void TestLoadData(int NumUser, int n, TestService s)
        {
            var RetryCount = 0;
            while (RetryCount < 16)
            {
                try
                {
                    var v = s.LoadData(n);
                    Trace.Assert(v == n);
                    return;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    if ((ex.Code == "40002") || (ex.Code == "40004"))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
        }

        public static void TestSaveAndLoadData(int NumUser, int n, TestService s)
        {
            var RetryCount = 0;
            while (RetryCount < 16)
            {
                try
                {
                    s.SaveData(n, n);
                    var v = s.LoadData(n);
                    Trace.Assert(v == n);
                    return;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    if ((ex.Code == "40002") || (ex.Code == "40004"))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
        }

        public static void TestAddLockData(int NumUser, int n, TestService s)
        {
            var RetryCount = 0;
            while (RetryCount < 16)
            {
                try
                {
                    s.AddLockData(1);
                    return;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    if ((ex.Code == "40002") || (ex.Code == "40004"))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
        }

        private static int SumValue;
        private static Object Lockee = new Object();
        public static void TestAddDeleteLockData(int NumUser, int n, TestService s)
        {
            //如果在不使用CascadeLock和TransactionLock的情况下
            //(将Program.cs中的
            //var dam = new DataAccessManager(ConnectionString, cl);
            //替换为
            //var dam = new DataAccessManager(ConnectionString, null);
            //)
            //部分数据库会出现死锁问题

            //测试锁定不存在的行
            //目前测试的数据库
            //SQL Server 2012 通过
            //MySQL 5.5.29 较难出现死锁
            //MySQL 5.6.14 死锁
            //MySQL 5.7.2 死锁
            //PostgreSQL 9.1 异常

            var RetryCount = 0;
            while (RetryCount < 16)
            {
                try
                {
                    if (n % 2 == 0)
                    {
                        s.AddLockData(1);
                    }
                    else
                    {
                        var v = s.DeleteLockData();
                        lock (Lockee)
                        {
                            SumValue += v + 1;
                        }
                    }
                    return;
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    if ((ex.Code == "40002") || (ex.Code == "40004"))
                    {
                        RetryCount += 1;
                        continue;
                    }
                    else
                    {
                        throw new AggregateException(ex);
                    }
                }
            }
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

            SumValue = 0;
            t.SaveLockData(0);
            TestForNumUser(dam, 64, "TestAddDeleteLockData", TestAddDeleteLockData);
            Trace.Assert(SumValue + t.LoadLockData() == 64);

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
            for (int k = 0; k < 7; k += 1)
            {
                var NumUser = 1 << (2 * k);
                t.SaveLockData(0);
                TestForNumUser(dam, NumUser, "TestAddLockData", TestAddLockData);
                Trace.Assert(t.LoadLockData() == NumUser);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 7; k += 1)
            {
                SumValue = 0;
                var NumUser = 1 << (2 * k);
                t.SaveLockData(0);
                TestForNumUser(dam, NumUser, "TestAddDeleteLockData", TestAddDeleteLockData);
                Trace.Assert(SumValue + t.LoadLockData() == NumUser);
            }

            return 0;
        }
    }
}
