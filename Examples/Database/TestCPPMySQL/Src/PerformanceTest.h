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

#include <boost/thread.hpp>
#include <boost/date_time/posix_time/posix_time.hpp>
#include <boost/asio.hpp>

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
			boost::this_thread::sleep(boost::posix_time::milliseconds(millisec));
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
			auto tf = std::make_shared<BaseSystem::ThreadLocalVariable<TestService>>(Factory);

			int ProcessorCount = (int)(boost::thread::hardware_concurrency());
			auto IoService = std::make_shared<boost::asio::io_service>(ProcessorCount * 2 + 1);
			boost::asio::io_service::work Work(*IoService);

			std::vector<std::shared_ptr<boost::thread>> Threads;
			for (int i = 0; i < ProcessorCount; i += 1)
			{
				auto t = std::make_shared<boost::thread>([&]()
				{
					IoService->run();
				});
				Threads.push_back(t);
			}

			BaseSystem::AutoResetEvent eNum;
			BaseSystem::LockedVariable<int> vNum(NumUser);

			auto Time = boost::posix_time::microsec_clock::universal_time();

			for (int i = 0; i < NumUser; i += 1)
			{
				IoService->post([=, &eNum, &vNum]()
				{
					for (int k = 0; k < NumRequestPerUser; k += 1)
					{
						Test(NumUser, i, tf->Value());
					}
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

			auto TimeDiff = static_cast<int>((boost::posix_time::microsec_clock::universal_time() - Time).total_milliseconds());

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