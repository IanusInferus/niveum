//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <Visual C++ 2010>
//  Description: 数据复制工具
//  Version:     2012.04.07.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma warning (disable : 4345)

#include "Utility.h"
#include "World.h"
#include "WorldBinary.h"
#include "Streams.h"

#include <exception>
#include <stdexcept>
#include <string>

using namespace std;
using namespace World;
using namespace ::World::Streams;

void DisplayInfo()
{
    wprintf(L"%ls\n", L"数据复制工具");
    wprintf(L"%ls\n", L"DataCopy，Public Domain");
    wprintf(L"%ls\n", L"F.R.C.");
    wprintf(L"%ls\n", L"");
    wprintf(L"%ls\n", L"用法:");
    wprintf(L"%ls\n", L"DataCopy (/<Command>)*");
    wprintf(L"%ls\n", L"复制二进制数据");
    wprintf(L"%ls\n", L"/b2b:<BinaryFile1>,<BinaryFile2>");
    wprintf(L"%ls\n", L"BinaryFile1 二进制文件路径。");
    wprintf(L"%ls\n", L"BinaryFile2 二进制文件路径。");
    wprintf(L"%ls\n", L"");
    wprintf(L"%ls\n", L"示例:");
    wprintf(L"%ls\n", L"DataCopy /b2b:..\\..\\Data\\WorldData.tree,..\\Data\\WorldData.bin");
    wprintf(L"%ls\n", L"将WorldData.tree转化为WorldData.bin。");
}

void BinaryToBinary(wstring BinaryPath1, wstring BinaryPath2)
{
    ReadableStream rs(BinaryPath1);

    auto Data = BinaryTranslator::WorldFromBinary(rs);

    WritableStream ws(BinaryPath2);

    BinaryTranslator::WorldToBinary(ws, Data);
}

int MainInner()
{
    auto CmdLine = GetCmdLine();

    if (CmdLine->Arguments->size() != 0)
    {
        DisplayInfo();
        return -1;
    }

    if (CmdLine->Options->size() == 0)
    {
        DisplayInfo();
        return 0;
    }

    for (auto iopt = CmdLine->Options->begin(); iopt != CmdLine->Options->end(); iopt.operator++())
    {
        auto opt = *iopt;
        if (EqualIgnoreCase(opt->Name, L"?") || EqualIgnoreCase(opt->Name, L"help"))
        {
            DisplayInfo();
            return 0;
        }
        else if (EqualIgnoreCase(opt->Name, L"b2b"))
        {
            auto args = opt->Arguments;
            if (args->size() == 2)
            {
                BinaryToBinary(*(*args)[0], *(*args)[1]);
            }
            else
            {
                DisplayInfo();
                return -1;
            }
        }
        else
        {
            throw logic_error(w2s(L"ArgumentException: " + *opt->Name));
        }
    }

    return 0;
}

int main(int argc, char **argv)
{
    setlocale(LC_ALL, "");

    try
    {
        return MainInner();
    }
    catch (std::exception &ex)
    {
        printf("Error:\n%s\n", ex.what());
    }
}