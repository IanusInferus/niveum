﻿//==========================================================================
//
//  File:        Utility.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 工具函数
//  Version:     2012.04.08.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma warning (disable : 4345)
#pragma warning (disable : 4996)

#include "Utility.h"

#include <climits>

using namespace std;

bool EqualIgnoreCase(const wstring& l, const wstring& r)
{
    if (l.length() != r.length()) { return false; }
    for (size_t i = 0; i < l.length(); i += 1)
    {
        if (l[i] != r[i])
        {
            auto cl = tolower(l[i]);
            auto cr = tolower(r[i]);
            if (cl != cr)
            {
                return false;
            }
        }
    }
    return true;
}

bool EqualIgnoreCase(shared_ptr<wstring> l, const wstring& r)
{
    return EqualIgnoreCase(*l, r);
}

bool EqualIgnoreCase(shared_ptr<wstring> l, shared_ptr<wstring> r)
{
    return EqualIgnoreCase(*l, *r);
}

shared_ptr<wstring> s2w(shared_ptr<string> s)
{
    int n = mbstowcs(NULL, s->c_str(), INT_MAX);
    if (n <= 0) return nullptr;
    auto ws = make_shared<wstring>(n, 0);
    mbstowcs(&(*ws)[0], s->c_str(), n);
    return ws;
}

shared_ptr<string> w2s(shared_ptr<wstring> ws)
{
    int n = wcstombs(NULL, ws->c_str(), INT_MAX);
    if (n <= 0) return nullptr;
    auto s = make_shared<string>(n, 0);
    wcstombs(&(*s)[0], ws->c_str(), n);
    return s;
}

wstring s2w(const string& s)
{
    return *s2w(make_shared<string>(s));
}

string w2s(const wstring& ws)
{
    return *w2s(make_shared<wstring>(ws));
}
