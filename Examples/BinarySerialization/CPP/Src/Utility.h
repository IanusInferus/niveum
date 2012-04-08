//==========================================================================
//
//  File:        Utility.h
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 工具函数
//  Version:     2012.04.08.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma once
#include <string>
#include <memory>
#include <vector>
#include <sstream>
#include <exception>
#include <stdexcept>
#include <locale>

#define PRIVATE static

bool EqualIgnoreCase(const std::wstring& l, const std::wstring& r);
bool EqualIgnoreCase(std::shared_ptr<std::wstring> l, const std::wstring& r);
bool EqualIgnoreCase(std::shared_ptr<std::wstring> l, std::shared_ptr<std::wstring> r);

template<typename T>
std::wstring ToString(T value)
{
    std::wstringstream s;
    s << value;
    return std::wstring(s.str());
}

template<typename T>
T Parse(const std::wstring& str)
{
    if (str.size() > 0)
    {
         wchar_t c = str[0];
         if (!(isdigit(c) || (c == '+') || (c == '-') || (c == '.')))
         {
             throw logic_error("InvalidFormat");
         }
    }
    std::wstringstream s;
    s << str;
    T t;
    s >> t;
    if (!s.eof())
    {
        throw logic_error("InvalidFormat");
    }
    return t;
}

template<typename T>
T Parse(std::shared_ptr<std::wstring> str)
{
    return Parse<T>(*str);
}

std::shared_ptr<std::wstring> s2w(std::shared_ptr<std::string> s);
std::shared_ptr<std::string> w2s(std::shared_ptr<std::wstring> ws);
std::wstring s2w(const std::string& s);
std::string w2s(const std::wstring& ws);
