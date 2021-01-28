#include "StringUtilities.h"

#include <cstddef>
#include <climits>
#include <cctype>
#include <stdexcept>
#include <utf8.h>
#if defined(WIN32) || defined(_WIN32)
#   ifndef NOMINMAX
#       define NOMINMAX
#   endif
#   ifndef WIN32_LEAN_AND_MEAN
#       define WIN32_LEAN_AND_MEAN
#   endif
#   include <Windows.h>
#   include <stringapiset.h>
#elif defined(__APPLE__)
#   include <xlocale.h>
#   include <wchar.h>
#else
#   include <cwchar>
#endif

std::u16string utf8ToUtf16(const std::string & u8s)
{
    std::u16string u16s;
    u16s.reserve(u8s.size());
    utf8::utf8to16(u8s.begin(), u8s.end(), std::back_inserter(u16s));
    return u16s;
}

std::string utf16ToUtf8(const std::u16string & u16s)
{
    std::string u8s;
    u8s.reserve(u16s.size() * 2);
    utf8::utf16to8(u16s.begin(), u16s.end(), std::back_inserter(u8s));
    return u8s;
}

std::u32string utf8ToUtf32(const std::string & u8s)
{
    std::u32string u32s;
    u32s.reserve(u8s.size());
    utf8::utf8to32(u8s.begin(), u8s.end(), std::back_inserter(u32s));
    return u32s;
}

std::string utf32ToUtf8(const std::u32string & u32s)
{
    std::string u8s;
    u8s.reserve(u32s.size() * 4);
    utf8::utf32to8(u32s.begin(), u32s.end(), std::back_inserter(u8s));
    return u8s;
}

std::u16string utf32ToUtf16(const std::u32string & u32s)
{
    return utf8ToUtf16(utf32ToUtf8(u32s));
}

std::u32string utf16ToUtf32(const std::u16string & u16s)
{
    return utf8ToUtf32(utf16ToUtf8(u16s));
}

std::string wideCharToUtf8(const std::wstring & ws)
{
    if constexpr (sizeof(wchar_t) == 2)
    {
        return utf16ToUtf8(std::u16string(reinterpret_cast<const char16_t *>(ws.data()), reinterpret_cast<const char16_t *>(ws.data() + ws.size())));
    }
    else if constexpr (sizeof(wchar_t) == 4)
    {
        return utf32ToUtf8(std::u32string(reinterpret_cast<const char32_t *>(ws.data()), reinterpret_cast<const char32_t *>(ws.data() + ws.size())));
    }
    else
    {
        throw std::logic_error("InvalidOperation");
    }
}
std::wstring utf8ToWideChar(const std::string & us)
{
    if constexpr (sizeof(wchar_t) == 2)
    {
        auto u16s = utf8ToUtf16(us);
        return std::wstring(reinterpret_cast<const wchar_t *>(u16s.data()), reinterpret_cast<const wchar_t *>(u16s.data() + u16s.size()));
    }
    else if constexpr (sizeof(wchar_t) == 4)
    {
        auto u32s = utf8ToUtf32(us);
        return std::wstring(reinterpret_cast<const wchar_t *>(u32s.data()), reinterpret_cast<const wchar_t *>(u32s.data() + u32s.size()));
    }
    else
    {
        throw std::logic_error("InvalidOperation");
    }
}

std::u16string wideCharToUtf16(const std::wstring & ws)
{
    if constexpr (sizeof(wchar_t) == 2)
    {
        return std::u16string(reinterpret_cast<const char16_t *>(ws.data()), reinterpret_cast<const char16_t *>(ws.data() + ws.size()));
    }
    else if constexpr (sizeof(wchar_t) == 4)
    {
        return utf32ToUtf16(std::u32string(reinterpret_cast<const char32_t *>(ws.data()), reinterpret_cast<const char32_t *>(ws.data() + ws.size())));
    }
    else
    {
        throw std::logic_error("InvalidOperation");
    }
}

std::wstring utf16ToWideChar(const std::u16string & us)
{
    if constexpr (sizeof(wchar_t) == 2)
    {
        return std::wstring(reinterpret_cast<const wchar_t *>(us.data()), reinterpret_cast<const wchar_t *>(us.data() + us.size()));
    }
    else if constexpr (sizeof(wchar_t) == 4)
    {
        auto u32s = utf16ToUtf32(us);
        return std::wstring(reinterpret_cast<const wchar_t *>(u32s.data()), reinterpret_cast<const wchar_t *>(u32s.data() + u32s.size()));
    }
    else
    {
        throw std::logic_error("InvalidOperation");
    }
}

std::wstring systemToWideChar(const std::string & s)
{
#if defined(WIN32) || defined(_WIN32)
    if (s.size() == 0) { return L""; }
    int n = MultiByteToWideChar(CP_THREAD_ACP, 0, s.data(), static_cast<int>(s.size()), nullptr, 0);
    if (n == 0)
    {
        throw std::logic_error("InvalidChar");
    }
    std::wstring ws(n, 0);
    MultiByteToWideChar(CP_THREAD_ACP, 0, s.data(), static_cast<int>(s.size()), const_cast<wchar_t*>(ws.data()), n);
    return ws;
#else
    std::wstring ws;
    ws.reserve(s.size());
    std::mbstate_t State{};
    wchar_t wc{};
    const char* Ptr = s.data();
    const char* End = s.data() + s.size();
    while (Ptr < End)
    {
#if defined(__APPLE__)
        std::size_t InCharCount = mbrtowc_l(&wc, Ptr, End - Ptr, &State, LC_GLOBAL_LOCALE);
#else
        std::size_t InCharCount = std::mbrtowc(&wc, Ptr, End - Ptr, &State);
#endif
        if (InCharCount == 0)
        {
            //null character
            Ptr += 1;
        }
        else if (InCharCount == static_cast<std::size_t>(-3))
        {
        }
        else if ((InCharCount == static_cast<std::size_t>(-2)) || (InCharCount == static_cast<std::size_t>(-1)))
        {
            throw std::logic_error("InvalidChar");
        }
        else
        {
            Ptr += InCharCount;
        }
        ws.push_back(wc);
    }
    return ws;
#endif
}

std::string wideCharToSystem(const std::wstring & ws)
{
#if defined(WIN32) || defined(_WIN32)
    if (ws.size() == 0) { return ""; }
    int n = WideCharToMultiByte(CP_THREAD_ACP, 0, ws.data(), static_cast<int>(ws.size()), nullptr, 0, nullptr, nullptr);
    std::string s(n, 0);
    if (n == 0)
    {
        throw std::logic_error("InvalidChar");
    }
    WideCharToMultiByte(CP_THREAD_ACP, 0, ws.data(), static_cast<int>(ws.size()), const_cast<char *>(s.data()), n, nullptr, nullptr);
    return s;
#else
    std::string s;
    s.reserve(ws.size() * sizeof(wchar_t));
    std::mbstate_t State{};
    char cOut[MB_LEN_MAX]{};
    for (wchar_t wc : ws)
    {
#if defined(__APPLE__)
        std::size_t OutCharCount = wcrtomb_l(cOut, wc, &State, LC_GLOBAL_LOCALE);
#else
        std::size_t OutCharCount = std::wcrtomb(cOut, wc, &State);
#endif
        if (OutCharCount == static_cast<std::size_t>(-1))
        {
            throw std::logic_error("InvalidChar");
        }
        s.append(cOut, OutCharCount);
    }
    return s;
#endif
}


std::u16string systemToUtf16(const std::string & s)
{
    return wideCharToUtf16(systemToWideChar(s));
}

std::string utf16ToSystem(const std::u16string & us)
{
    return wideCharToSystem(utf16ToWideChar(us));
}

std::string systemToUtf8(const std::string & s)
{
    return wideCharToUtf8(systemToWideChar(s));
}

std::string utf8ToSystem(const std::string & us)
{
    return wideCharToSystem(utf8ToWideChar(us));
}

bool EqualIgnoreCase(const std::wstring& l, const std::wstring& r)
{
    if (l.length() != r.length()) { return false; }
    for (std::size_t i = 0; i < l.length(); i += 1)
    {
        if (l[i] != r[i])
        {
            auto cl = std::tolower(l[i]);
            auto cr = std::tolower(r[i]);
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
        s[i] = std::tolower(s[i]);
    }
    return std::move(s);
}

std::wstring ToUpper(const std::wstring &Input)
{
    auto s = Input;
    for (std::size_t i = 0; i < s.length(); i += 1)
    {
        s[i] = std::toupper(s[i]);
    }
    return std::move(s);
}
