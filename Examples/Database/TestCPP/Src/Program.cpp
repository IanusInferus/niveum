//==========================================================================
//
//  File:        Program.cpp
//  Location:    Niveum.Examples <C++ 2011>
//  Description: 数据库示例程序
//  Version:     2021.02.13.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "BaseSystem/StringUtilities.h"
#include "DataAccessManager.h"
#include "LoadTest.h"
#include "PerformanceTest.h"

#include <exception>
#include <stdexcept>
#include <string>
#include <cwchar>

namespace Database
{
    class Program
    {
    public:
        static int MainInner(int argc, char **argv)
        {
            DisplayTitle();

            if (argc == 3)
            {
                auto ConnectionString = systemToWideChar(argv[1]);
                auto Option = systemToWideChar(argv[2]);

                auto dam = std::make_shared<DataAccessManager>(ConnectionString);
                if (EqualIgnoreCase(Option, L"/simple"))
                {
                    auto da = dam->Create();
                    auto records = da->FromTestDuplicatedKeyNameRecordSelectManyByA(u"Test1");
                    for (auto r : *records)
                    {
                        std::wprintf(L"%d %ls %d\n", r->Id, utf16ToWideChar(r->A).c_str(), static_cast<int>(r->B));
                    }
                }
                else if (EqualIgnoreCase(Option, L"/load"))
                {
                    LoadTest::DoTest(dam);
                }
                else if (EqualIgnoreCase(Option, L"/perf"))
                {
                    PerformanceTest::DoTest(dam);
                }
                else
                {
                    DisplayInfo(DataAccessManager::GetProgramName(), DataAccessManager::GetConnectionStringExample());
                    return -1;
                }
            }
            else
            {
                DisplayInfo(DataAccessManager::GetProgramName(), DataAccessManager::GetConnectionStringExample());
                return -1;
            }
            return 0;
        }

        static void DisplayTitle()
        {
            std::wprintf(L"%ls\n", L"数据库示例程序");
            std::wprintf(L"%ls\n", L"Author:      F.R.C.");
            std::wprintf(L"%ls\n", L"Copyright(C) Public Domain");
        }

        static void DisplayInfo(std::wstring ProgramName, std::wstring ConnectionString)
        {
            std::wprintf(L"%ls\n", L"用法:");
            std::wprintf(L"%ls\n", (ProgramName + L" <ConnectionString> /simple|/load|/perf").c_str());
            std::wprintf(L"%ls\n", L"ConnectionString 数据库连接字符串");
            std::wprintf(L"%ls\n", L"/simple 简单测试");
            std::wprintf(L"%ls\n", L"/load 自动化负载测试，当前不支持");
            std::wprintf(L"%ls\n", L"/perf 自动化性能测试，当前不支持");
            std::wprintf(L"%ls\n", L"示例:");
            if (ConnectionString != L"")
            {
                std::wprintf(L"%ls\n", (ProgramName + L" \"" + ConnectionString + L"\" /simple").c_str());
            }
        }
    };
}

#ifdef _MSC_VER

#include <io.h>
#include <fcntl.h>

void ModifyStdoutUnicode()
{
    _setmode(_fileno(stdout), _O_U16TEXT);
}

#else

void ModifyStdoutUnicode()
{
}

#endif

#include <locale.h>

void SetLocale()
{
    setlocale(LC_ALL, "");
}

int main(int argc, char **argv)
{
    ModifyStdoutUnicode();
    SetLocale();

    try
    {
        return Database::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        std::wprintf(L"Error:\n%ls\n", systemToWideChar(ex.what()).c_str());
        return -1;
    }
}
