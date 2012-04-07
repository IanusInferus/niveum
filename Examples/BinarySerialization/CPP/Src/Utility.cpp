//==========================================================================
//
//  File:        Utility.cpp
//  Location:    Yuki.Examples <Visual C++ 2010>
//  Description: ¹¤¾ßº¯Êý
//  Version:     2012.04.07.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include <windows.h>
#include <regex>
#include <functional>

#include "Utility.h"

using namespace std;

PRIVATE shared_ptr<wstring> DescapeQuota(shared_ptr<wstring> s)
{
    if (*s == L"") { return make_shared<wstring>(L""); }
    if ((s->length() >= 1) && ((*s)[0] == '\"') && ((*s)[s->length() - 1] == '\"'))
    {
        return make_shared<wstring>(&((*s)[1]), &((*s)[s->length() - 1]));
    }
    else
    {
        return s;
    }
}

PRIVATE shared_ptr<vector<shared_ptr<wstring>>> SplitCmdLineWithChar(shared_ptr<wstring> CmdLine, shared_ptr<wstring> Pattern, wchar_t c, bool SuppressFirst)
{
    auto argv = make_shared<vector<shared_ptr<wstring>>>();
    bool SuppressedFirst = !SuppressFirst;
    size_t NextStart = 0;
    wregex r(*Pattern);
    wcmatch arg;
    while ((NextStart < CmdLine->length()) && (regex_search(&(*CmdLine)[NextStart], arg, r)))
    {
        if (arg.position() != 0)
        {
            size_t Length = arg.position();
            for (size_t i = 0; i < Length; i += 1)
            {
                if ((*CmdLine)[NextStart + i]  != c)
                {
                    throw logic_error("InvalidOperation");
                }
            }
        }
        size_t Start = NextStart + arg.position();
        NextStart = Start + arg.str().length();
        if (!SuppressedFirst)
        {
            SuppressedFirst = true;
            continue;
        }
        auto m = make_shared<wstring>(*CmdLine, Start, arg.str().length());
        argv->push_back(DescapeQuota(m));
    }
    if (NextStart != CmdLine->length())
    {
        size_t Length = CmdLine->length() - NextStart;
        for (size_t i = 0; i < Length; i += 1)
        {
            if ((*CmdLine)[NextStart + i] != c)
            {
                throw logic_error("InvalidOperation");
            }
        }
    }
    return argv;
}

shared_ptr<CommandLineArguments> GetCmdLine()
{
    LPWSTR CmdLine = GetCommandLineW();
    auto argv = SplitCmdLineWithChar(make_shared<wstring>(CmdLine), make_shared<wstring>(L"(\"[^\"]*\"|([^\" ])+)+"), L' ', true);

    auto Arguments = make_shared<vector<shared_ptr<wstring>>>();
    auto Options = make_shared<vector<shared_ptr<CommandLineOption>>>();

    for_each(argv->begin(), argv->end(), [&](shared_ptr<wstring> v)
    {
        if ((v->length() > 0) && ((*v)[0] == L'/'))
        {
            auto OptionLine = make_shared<wstring>(*v, 1, v->length() - 1);
            shared_ptr<wstring> Name;
            shared_ptr<wstring> ParameterLine;
            bool Found = false;
            size_t Index = 0;
            for (size_t i = 0; i < OptionLine->length(); i += 1)
            {
                if ((*OptionLine)[i] == L':') {
                    Found = true;
                    Index = i;
                    break;
                }
            }
            if (Found)
            {
                Name = DescapeQuota(make_shared<wstring>(*OptionLine, 0, Index));
                ParameterLine = make_shared<wstring>(*OptionLine, Index + 1);
            }
            else
            {
                Name = DescapeQuota(OptionLine);
                ParameterLine = make_shared<wstring>(L"");
            }

            auto Arguments = SplitCmdLineWithChar(ParameterLine, make_shared<wstring>(L"(\"[^\"]*\"|([^\",])+)+"), L',', false);

            auto clo = make_shared<CommandLineOption>();
            clo->Name = Name;
            clo->Arguments = Arguments;

            Options->push_back(clo);
        }
        else
        {
            Arguments->push_back(v);
        }
    });

    auto ret = make_shared<CommandLineArguments>();
    ret->Arguments = Arguments;
    ret->Options = Options;

    return ret;
}

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
    int n = MultiByteToWideChar(CP_ACP, 0, s->c_str(), s->length(), 0, 0);
    if(n <= 0) return nullptr;
    auto ws = make_shared<wstring>(n, 0);
    MultiByteToWideChar(CP_ACP, 0, s->c_str(), s->length(), &(*ws)[0], n);
    return ws;
}

shared_ptr<string> w2s(shared_ptr<wstring> ws)
{
    int n = WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, ws->c_str(), ws->length(), 0, 0, "?", NULL);
    if(n <= 0) return nullptr;
    auto s = make_shared<string>(n, 0);
    WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, ws->c_str(), ws->length(), &(*s)[0], n, "?", NULL);
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
