//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 数据复制工具
//  Version:     2015.07.22.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "BaseSystem/StringUtilities.h"
#include "World.h"
#include "WorldBinary.h"
#include "Streams.h"

#include <exception>
#include <stdexcept>
#include <string>
#include <cwchar>

using namespace World;
using namespace ::World::Streams;

void DisplayInfo()
{
    std::wprintf(L"%ls\n", L"数据复制工具");
    std::wprintf(L"%ls\n", L"DataCopy，Public Domain");
    std::wprintf(L"%ls\n", L"F.R.C.");
    std::wprintf(L"%ls\n", L"");
    std::wprintf(L"%ls\n", L"用法:");
    std::wprintf(L"%ls\n", L"DataCopy <BinaryFile1> <BinaryFile2>");
    std::wprintf(L"%ls\n", L"复制二进制数据");
    std::wprintf(L"%ls\n", L"BinaryFile1 二进制文件路径。");
    std::wprintf(L"%ls\n", L"BinaryFile2 二进制文件路径。");
    std::wprintf(L"%ls\n", L"");
    std::wprintf(L"%ls\n", L"示例:");
    std::wprintf(L"%ls\n", L"DataCopy ..\\..\\SchemaManipulator\\Data\\WorldData.bin ..\\Data\\WorldData.bin");
    std::wprintf(L"%ls\n", L"复制WorldData.bin。");
}

void BinaryToBinary(std::wstring BinaryPath1, std::wstring BinaryPath2)
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

int main(int argc, char **argv)
{
    ModifyStdoutUnicode();

    try
    {
        return MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        std::wprintf(L"Error:\n%ls\n", s2w(ex.what()).c_str());
    }
}

