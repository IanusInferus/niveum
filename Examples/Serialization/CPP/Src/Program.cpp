//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 数据复制工具
//  Version:     2015.07.18.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

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
    wprintf(L"%ls\n", L"DataCopy <BinaryFile1> <BinaryFile2>");
    wprintf(L"%ls\n", L"复制二进制数据");
    wprintf(L"%ls\n", L"BinaryFile1 二进制文件路径。");
    wprintf(L"%ls\n", L"BinaryFile2 二进制文件路径。");
    wprintf(L"%ls\n", L"");
    wprintf(L"%ls\n", L"示例:");
    wprintf(L"%ls\n", L"DataCopy ..\\..\\SchemaManipulator\\Data\\WorldData.bin ..\\Data\\WorldData.bin");
    wprintf(L"%ls\n", L"复制WorldData.bin。");
}

void BinaryToBinary(wstring BinaryPath1, wstring BinaryPath2)
{
    ReadableStream rs(BinaryPath1);

    auto Data = BinaryTranslator::WorldFromBinary(rs);

    WritableStream ws(BinaryPath2);

    BinaryTranslator::WorldToBinary(ws, Data);
}

int MainInner(int argc, char **argv)
{
    if (argc != 3)
    {
        DisplayInfo();
        return -1;
    }
    BinaryToBinary(s2w(argv[1]), s2w(argv[2]));
    return 0;
}

int main(int argc, char **argv)
{
    setlocale(LC_ALL, "");

    try
    {
        return MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        printf("Error:\n%s\n", ex.what());
    }
}