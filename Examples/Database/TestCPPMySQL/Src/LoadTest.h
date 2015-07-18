#pragma once

#include "AutoResetEvent.h"
#include "LockedVariable.h"
#include "ThreadLocalVariable.h"
#include "TestService.h"

#include <memory>
#include <string>
#include <functional>
#include <exception>
#include <stdexcept>
#include <cstdio>
#include <chrono>
#include <thread>

#include <boost/asio.hpp>

namespace Database
{
    class LoadTest
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

        static void TestForNumUser(std::shared_ptr<DataAccessManager> dam, int NumUser, std::wstring Title, std::function<void(int, int, std::shared_ptr<TestService>)> Test)
        {
            auto Factory = [=]() -> std::shared_ptr<TestService> { return std::make_shared<TestService>(*dam); };
            auto tf = std::make_shared<BaseSystem::ThreadLocalVariable<TestService>>(Factory);

            int ProcessorCount = (int)(std::thread::hardware_concurrency());
            auto IoService = std::make_shared<boost::asio::io_service>(ProcessorCount * 2 + 1);
            boost::asio::io_service::work Work(*IoService);

            std::vector<std::shared_ptr<std::thread>> Threads;
            for (int i = 0; i < ProcessorCount; i += 1)
            {
                auto t = std::make_shared<std::thread>([&]()
                {
                    IoService->run();
                });
                Threads.push_back(t);
            }

            BaseSystem::AutoResetEvent eNum;
            BaseSystem::LockedVariable<int> vNum(NumUser);

            auto Time = std::chrono::steady_clock::time_point::clock::now();

            for (int i = 0; i < NumUser; i += 1)
            {
                IoService->post([=, &eNum, &vNum]()
                {
                    Test(NumUser, i, tf->Value());
                    vNum.Update([](const int &n) { return n - 1; });
                    eNum.Set();
                });
            }

            while(vNum.Check<bool>([](const int &n) { return n > 0; }))
            {
                eNum.WaitOne();
            }

            IoService->stop();

            for (int i = 0; i < (int)(Threads.size()); i += 1)
            {
                auto t = Threads[i];
                t->join();
            }

            auto TimeSpan = std::chrono::duration<double, std::chrono::milliseconds::period>(std::chrono::steady_clock::time_point::clock::now() - Time);
            auto TimeDiff = static_cast<int>(std::round(TimeSpan.count()));

            if (Title == L"") { return; }
            std::wprintf(L"%ls: %d Users, %d ms\n", Title.c_str(), NumUser, TimeDiff);
        }

        static int DoTest(std::shared_ptr<DataAccessManager> dam)
        {
            auto t = std::make_shared<TestService>(*dam);

            TestForNumUser(dam, 64, L"TestSaveData", TestSaveData);
            TestForNumUser(dam, 64, L"TestLoadData", TestLoadData);
            TestForNumUser(dam, 64, L"TestSaveAndLoadData", TestSaveAndLoadData);

            t->SaveLockData(0);
            TestForNumUser(dam, 64, L"TestAddLockData", TestAddLockData);
            Assert(t->LoadLockData() == 64);

            Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), L"TestSaveData", TestSaveData);
            }

            Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), L"TestLoadData", TestLoadData);
            }

            Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(dam, 1 << (2 * k), L"TestSaveAndLoadData", TestSaveAndLoadData);
            }

            Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                auto NumUser = 1 << (2 * k);
                t->SaveLockData(0);
                TestForNumUser(dam, NumUser, L"TestAddLockData", TestAddLockData);
                Assert(t->LoadLockData() == NumUser);
            }

            return 0;
        }
    };
}