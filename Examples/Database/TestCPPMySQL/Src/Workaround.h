#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <iterator>
#include <stdexcept>
#include <codecvt>
#include <utf8.h>

namespace std
{
    template <>
    class wstring_convert<codecvt_utf8<wchar_t, 0x10FFFF, little_endian>, wchar_t>
    {
    public:
        wstring from_bytes(const char *first, const char *last)
        {
            if (sizeof(wchar_t) == 2)
            {
                wstring UTF16String;
                utf8::utf8to16(first, last, back_inserter(UTF16String));

                return UTF16String;
            }
            else if (sizeof(wchar_t) == 4)
            {
                wstring UTF32String;
                utf8::utf8to32(first, last, back_inserter(UTF32String));

                return UTF32String;
            }
            else
            {
                throw logic_error("InvalidOperation");
            }
        }
        string to_bytes(const wstring& str)
        {
            if (sizeof(wchar_t) == 2)
            {
                string UTF8Bytes;
                utf8::utf16to8(str.begin(), str.end(), back_inserter(UTF8Bytes));

                return UTF8Bytes;
            }
            else if (sizeof(wchar_t) == 4)
            {
                string UTF8Bytes;
                utf8::utf32to8(str.begin(), str.end(), back_inserter(UTF8Bytes));

                return UTF8Bytes;
            }
            else
            {
                throw logic_error("InvalidOperation");
            }
        }
    };

    template <>
    class wstring_convert<codecvt_utf16<wchar_t, 0x10FFFF, little_endian>, wchar_t>
    {
    public:
        wstring from_bytes(const char *first, const char *last)
        {
            if (sizeof(wchar_t) == 2)
            {
                vector<uint16_t> UTF16Chars;
                if ((last - first) % 2 != 0)
                {
                    throw logic_error("InvalidUTF16ByteString");
                }
                for (auto p = first; p < last; p += 2)
                {
                    uint8_t lower = *p;
                    uint8_t upper = *(p + 1);
                    UTF16Chars.push_back((static_cast<uint16_t>(upper) << 8 | static_cast<uint16_t>(lower)));
                }

                return wstring(UTF16Chars.begin(), UTF16Chars.end());
            }
            else if (sizeof(wchar_t) == 4)
            {
                vector<uint16_t> UTF16Chars;
                if ((last - first) % 2 != 0)
                {
                    throw logic_error("InvalidUTF16ByteString");
                }
                for (auto p = first; p < last; p += 2)
                {
                    uint8_t lower = *p;
                    uint8_t upper = *(p + 1);
                    UTF16Chars.push_back((static_cast<uint16_t>(upper) << 8 | static_cast<uint16_t>(lower)));
                }

                string UTF8String;
                utf8::utf16to8(UTF16Chars.begin(), UTF16Chars.end(), back_inserter(UTF8String));

                wstring UTF32String;
                utf8::utf8to32(UTF8String.begin(), UTF8String.end(), back_inserter(UTF32String));

                return UTF32String;
            }
            else
            {
                throw logic_error("InvalidOperation");
            }
        }
        string to_bytes(const wstring& str)
        {
            if (sizeof(wchar_t) == 2)
            {
                vector<uint16_t> UTF16String(str.begin(), str.end());

                string UTF16Bytes;
                UTF16Bytes.reserve(UTF16String.size() * 2);
                for (int p = 0; p < (int)(UTF16String.size()); p += 1)
                {
                    uint8_t lower = static_cast<uint16_t>(UTF16String[p] & 0xFF);
                    uint8_t upper = static_cast<uint16_t>((UTF16String[p] >> 8) & 0xFF);
                    UTF16Bytes.push_back(static_cast<char>(lower));
                    UTF16Bytes.push_back(static_cast<char>(upper));
                }

                return UTF16Bytes;
            }
            else if (sizeof(wchar_t) == 4)
            {
                string UTF8String;
                utf8::utf32to8(str.begin(), str.end(), back_inserter(UTF8String));

                vector<uint16_t> UTF16String;
                utf8::utf8to16(UTF8String.begin(), UTF8String.end(), back_inserter(UTF16String));

                string UTF16Bytes;
                UTF16Bytes.reserve(UTF16String.size() * 2);
                for (int p = 0; p < (int)(UTF16String.size()); p += 1)
                {
                    uint8_t lower = static_cast<uint16_t>(UTF16String[p] & 0xFF);
                    uint8_t upper = static_cast<uint16_t>((UTF16String[p] >> 8) & 0xFF);
                    UTF16Bytes.push_back(static_cast<char>(lower));
                    UTF16Bytes.push_back(static_cast<char>(upper));
                }

                return UTF16Bytes;
            }
            else
            {
                throw logic_error("InvalidOperation");
            }
        }
    };
}
