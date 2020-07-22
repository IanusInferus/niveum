#pragma once

#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/ThreadLocalVariable.h"
#include "TestService.h"

#include <cmath>
#include <memory>
#include <string>
#include <functional>
#include <exception>
#include <stdexcept>
#include <cstdio>
#include <thread>
#include <mutex>

namespace Database
{
    class PerformanceTest
    {
    public:
        static void Assert(bool expr)
        {
            if (!expr) { throw std::logic_error("Assertion failed."); }
        }

        static void Sleep(int millisec)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(millisec));
        }

        static void TestSaveData(int NumUser, int n, std::shared_ptr<TestService> s)
        {
            s->SaveData(n, n);
        }

        static void TestLoadData(int NumUser, int n, std::shared_ptr<TestService> s)
        {
            auto v = s->LoadData(n);
            Assert(v == n);
        }

        static void TestSaveAndLoadData(int NumUser, int n, std::shared_ptr<TestService> s)
        {
            s->SaveData(n, n);
            auto v = s->LoadData(n);
            Assert(v == n);
        }

        static void TestAddLockData(int NumUser, int n, std::shared_ptr<TestService> s)
        {
            s->AddLockData(1);
        }

        static void TestForNumUser(std::shared_ptr<DataAccessManager> dam, int NumRequestPerUser, int NumUser, std::wstring Title, std::function<void(int, int, std::shared_ptr<TestService>)> Test)
        {
            auto Factory = [=]() -> std::shared_ptr<TestService> { return std::make_shared<TestService>(*dam); };
            auto tf = BaseSystem::ThreadLocalVariable<std::shared_ptr<TestService>>(Factory);

            int ProcessorCount = (int)(std::thread::hardware_concurrency());
            auto ThreadCount = ProcessorCount * 2 + 1;

            std::mutex Lockee;
            auto TaskQueue = std::make_shared<std::queue<std::function<void()>>>();

            std::vector<std::shared_ptr<std::thread>> Threads;
            for (int i = 0; i < ProcessorCount; i += 1)
            {
                auto t = std::make_shared<std::thread>([&]()
                {
                    while (true)
                    {
                        std::function<void()> a = nullptr;
                        {
                            std::unique_lock<std::mutex> Lock(Lockee);
                            if (TaskQueue == nullptr) { return; }
                            if (TaskQueue->size() > 0)
                            {
                                a = TaskQueue->front();
                                TaskQueue->pop();
                            }
                        }
                        if (a != nullptr)
                        {
                            try
                            {
                                a();
                            }
                            catch (std::exception &ex)
                            {
                                std::wprintf(L"Error:\n%ls\n", systemToWideChar(ex.what()).c_str());
                            }
                        }
                    }
                });
                Threads.push_back(t);
            }

            BaseSystem::AutoResetEvent eNum;
            BaseSystem::LockedVariable<int> vNum(NumUser);

            auto Time = std::chrono::steady_clock::time_point::clock::now();

            for (int i = 0; i < NumUser; i += 1)
            {
                auto a = [=, &tf, &eNum, &vNum]()
                {
                    for (int k = 0; k < NumRequestPerUser; k += 1)
                    {
                        Test(NumUser, i, tf.Value());
                    }
                    vNum.Update([](const int &n) { return n - 1; });
                    eNum.Set();
                };
                {
                    std::unique_lock<std::mutex> Lock(Lockee);
                    TaskQueue->push(a);
                }
            }

            while(vNum.Check<bool>([](const int &n) { return n > 0; }))
            {
                eNum.WaitOne();
            }

            {
                std::unique_lock<std::mutex> Lock(Lockee);
                TaskQueue = nullptr;
            }

            for (int i = 0; i < (int)(Threads.size()); i += 1)
            {
                auto t = Threads[i];
                t->join();
            }

            auto TimeSpan = std::chrono::duration<double, std::chrono::milliseconds::period>(std::chrono::steady_clock::time_point::clock::now() - Time);
            auto TimeDiff = static_cast<int>(std::round(TimeSpan.count()));

            if (Title == L"") { return; }
            std::wprintf(L"%ls: %d Users, %d Request/User, %d ms\n", Title.c_str(), NumUser, NumRequestPerUser, TimeDiff);
        }

        static int DoTest(std::shared_ptr<DataAccessManager> dam)
        {
            auto t = std::make_shared<TestService>(*dam);

            TestForNumUser(dam, 8, 1, L"TestSaveData", TestSaveData);
            TestForNumUser(dam, 8, 1, L"TestLoadData", TestLoadData);
            TestForNumUser(dam, 8, 1, L"TestSaveAndLoadData", TestSaveAndLoadData);

            t->SaveLockData(0);
            TestForNumUser(dam, 8, 1, L"TestAddLockData", TestAddLockData);
            Assert(t->LoadLockData() == 8);

            Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(dam, 1 << k, 4096, L"TestSaveData", TestSaveData);
            }

            Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(dam, 1 << k, 4096, L"TestLoadData", TestLoadData);
            }

            Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(dam, 1 << k, 4096, L"TestSaveAndLoadData", TestSaveAndLoadData);
            }

            Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                auto NumUser = 1 << k;
                t->SaveLockData(0);
                TestForNumUser(dam, NumUser, 4096, L"TestAddLockData", TestAddLockData);
                Assert(t->LoadLockData() == NumUser * 4096);
            }

            return 0;
        }
    };
}