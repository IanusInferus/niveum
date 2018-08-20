#include "StringUtilities.h"

#include <stdexcept>
#include <limits>
#include <locale>
#include <codecvt>

bool EqualIgnoreCase(const std::wstring& l, const std::wstring& r)
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

std::wstring ToLower(const std::wstring &Input)
{
    auto s = Input;
    for (size_t i = 0; i < s.length(); i += 1)
    {
        s[i] = tolower(s[i]);
    }
    return std::move(s);
}

std::wstring ToUpper(const std::wstring &Input)
{
    auto s = Input;
    for (size_t i = 0; i < s.length(); i += 1)
    {
        s[i] = toupper(s[i]);
    }
    return std::move(s);
}

std::wstring s2w(const std::string& s)
{
    int n = static_cast<int>(std::mbstowcs(nullptr, s.c_str(), std::numeric_limits<std::size_t>::max()));
    if (n == static_cast<std::size_t>(-1)) { throw std::logic_error("InvalidOperationException"); }
    std::wstring ws(n, 0);
    if (n == 0) { return ws; }
    std::mbstowcs(&ws[0], s.c_str(), n);
    return ws;
}

std::string w2s(const std::wstring& ws)
{
    int n = static_cast<int>(std::wcstombs(nullptr, ws.c_str(), std::numeric_limits<std::size_t>::max()));
    if (n == static_cast<std::size_t>(-1)) { throw std::logic_error("InvalidOperationException"); }
    std::string s(n, 0);
    if (n == 0) { return s; }
    std::wcstombs(&s[0], ws.c_str(), n);
    return s;
}

std::wstring u2w(const std::string &us)
{
    std::wstring_convert<std::codecvt_utf8<wchar_t>, wchar_t> conv;
    return conv.from_bytes(us);
}

std::string w2u(const std::wstring &ws)
{
    std::wstring_convert<std::codecvt_utf8<wchar_t>, wchar_t> conv;
    return conv.to_bytes(ws);
}

std::string s2u(const std::string &s)
{
    return w2u(s2w(s));
}

std::string u2s(const std::string &us)
{
    return w2s(u2w(us));
}
