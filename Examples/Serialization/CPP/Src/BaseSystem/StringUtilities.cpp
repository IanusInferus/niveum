#include "StringUtilities.h"

#include <limits>

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

bool EqualIgnoreCase(std::shared_ptr<std::wstring> l, const std::wstring& r)
{
    return EqualIgnoreCase(*l, r);
}

bool EqualIgnoreCase(std::shared_ptr<std::wstring> l, std::shared_ptr<std::wstring> r)
{
    return EqualIgnoreCase(*l, *r);
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

std::shared_ptr<std::wstring> s2w(std::shared_ptr<std::string> s)
{
    return std::make_shared<std::wstring>(s2w(*s));
}

std::shared_ptr<std::string> w2s(std::shared_ptr<std::wstring> ws)
{
    return std::make_shared<std::string>(w2s(*ws));
}
