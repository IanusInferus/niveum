#pragma once
#include <string>
#include <memory>
#include <sstream>
#include <stdexcept>

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
std::wstring ToHexString(T value)
{
    std::wstringstream s;
    s << std::hex << std::uppercase << value;
    auto tail = std::wstring(s.str());
    if (tail.size() >= sizeof(T) * 2) { return tail; }
    auto head = std::wstring(sizeof(T) * 2 - tail.size(), L'0');
    return head + tail;
}

template<typename T>
T Parse(const std::wstring& str)
{
    if (str.size() > 0)
    {
         wchar_t c = str[0];
         if (!(isdigit(c) || (c == '+') || (c == '-') || (c == '.')))
         {
             throw std::logic_error("InvalidFormat");
         }
    }
    std::wstringstream s;
    s << str;
    T t;
    s >> t;
    if (!s.eof())
    {
        throw std::logic_error("InvalidFormat");
    }
    return t;
}

template<typename T>
T Parse(std::shared_ptr<std::wstring> str)
{
    return Parse<T>(*str);
}

template<typename CharT>
std::basic_string<CharT> ReplaceAllCopy(const std::basic_string<CharT> &Input, const std::basic_string<CharT> &Match, const std::basic_string<CharT> &Replacement)
{
    if (Match.length() == 0)
    {
        throw std::logic_error("InvalidArgument");
    }
    std::basic_string<CharT> Output;
    auto InputSize = Input.size();
    decltype(InputSize) Index = 0;
    while (Index < InputSize)
    {
        if (Input.compare(Index, Match.size(), Match) == 0)
        {
            Output.append(Replacement);
            Index += Match.size();
        }
        else
        {
            Output.push_back(Input[Index]);
            Index += 1;
        }
    }
    return std::move(Output);
}

template<typename CharT>
std::basic_string<CharT> ReplaceAllCopy(const std::basic_string<CharT> &Input, const CharT *Match, const CharT *Replacement)
{
    return ReplaceAllCopy(Input, std::basic_string<CharT>(Match), std::basic_string<CharT>(Replacement));
}

template<typename CharT>
std::basic_string<CharT> ReplaceAllCopy(const CharT *Input, const CharT *Match, const CharT *Replacement)
{
    return ReplaceAllCopy(std::basic_string<CharT>(Input), std::basic_string<CharT>(Match), std::basic_string<CharT>(Replacement));
}

std::wstring s2w(const std::string& s);
std::string w2s(const std::wstring& ws);
std::shared_ptr<std::wstring> s2w(std::shared_ptr<std::string> s);
std::shared_ptr<std::string> w2s(std::shared_ptr<std::wstring> ws);
