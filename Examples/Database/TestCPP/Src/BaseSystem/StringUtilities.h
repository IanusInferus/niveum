#pragma once
#include <string>
#include <memory>
#include <vector>
#include <sstream>
#include <stdexcept>

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

template<typename CharT, typename RangeT>
std::basic_string<CharT> JoinStrings(const RangeT &Value, const std::basic_string<CharT> &Separator)
{
    std::basic_string<CharT> s;
    bool First = true;
    for (std::basic_string<CharT> v : Value)
    {
        if (First)
        {
            s += v;
            First = false;
        }
        else
        {
            s += Separator;
            s += v;
        }
    }
    return std::move(s);
}

template<typename CharT, typename RangeT>
std::basic_string<CharT> JoinStrings(const RangeT &Value, const CharT *Separator)
{
    return JoinStrings(Value, std::basic_string<CharT>(Separator));
}

template<typename CharT, typename RangeT>
std::vector<std::basic_string<CharT>> SplitString(const std::basic_string<CharT> &Input, const RangeT &Separators)
{
    std::vector<std::basic_string<CharT>> l;
    std::basic_string<CharT> s;
    for (CharT c : Input)
    {
        bool Splitted = false;
        for (CharT Separator : Separators)
        {
            if (c == Separator)
            {
                l.push_back(s);
                s.clear();
                Splitted = true;
                break;
            }
        }
        if (Splitted)
        {
            continue;
        }
        s += c;
    }
    l.push_back(s);
    return std::move(l);
}

template<typename CharT>
std::vector<std::basic_string<CharT>> SplitString(const std::basic_string<CharT> &Input, const CharT *Separators)
{
    return SplitString(Input, std::basic_string<CharT>(Separators));
}

template<typename CharT>
bool StartWith(const std::basic_string<CharT> &Input, const std::basic_string<CharT> &Match)
{
    if (Input.length() < Match.length()) { return false; }
    return Input.compare(0, Match.length(), Match) == 0;
}

template<typename CharT>
bool StartWith(const std::basic_string<CharT> &Input, const CharT *Match)
{
    return StartWith(Input, std::basic_string<CharT>(Match));
}

template<typename CharT>
bool EndWith(const std::basic_string<CharT> &Input, const std::basic_string<CharT> &Match)
{
    if (Input.length() < Match.length()) { return false; }
    return Input.compare(Input.length() - Match.length(), Match.length(), Match) == 0;
}

template<typename CharT>
bool EndWith(const std::basic_string<CharT> &Input, const CharT *Match)
{
    return EndWith(Input, std::basic_string<CharT>(Match));
}

bool EqualIgnoreCase(const std::wstring &l, const std::wstring &r);

std::wstring ToLower(const std::wstring &Input);
std::wstring ToUpper(const std::wstring &Input);

// convert string from multibyte to widechar
std::wstring s2w(const std::string &s);

// convert string from widechar to multibyte
std::string w2s(const std::wstring &ws);

// convert string from UTF-8 to widechar
std::wstring u2w(const std::string &us);

// convert string from widechar to UTF-8
std::string w2u(const std::wstring &ws);

// convert string from multibyte to UTF-8
std::string s2u(const std::string &s);

// convert string from UTF-8 to multibyte
std::string u2s(const std::string &us);
