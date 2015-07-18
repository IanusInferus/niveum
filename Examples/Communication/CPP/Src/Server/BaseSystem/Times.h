#pragma once

#include <string>
#include <chrono>

// time to ISO 8601 string 'yyyy-MM-ddTHH:mm:ssZ'
std::wstring DateTimeUtcToString(std::chrono::system_clock::time_point Time);

// parse ISO 8601 string 'yyyy-MM-ddTHH:mm:ssZ'
std::chrono::system_clock::time_point StringToDateTimeUtc(std::wstring s);

std::chrono::system_clock::time_point UtcNow();
