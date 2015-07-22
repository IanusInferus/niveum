#include "Times.h"

#include "Strings.h"

#include <sstream>
#include <iomanip>
#include <tuple>
#include <regex>

// http://howardhinnant.github.io/date_algorithms.html

// Returns number of days since civil 1970-01-01.  Negative values indicate
//    days prior to 1970-01-01.
// Preconditions:  y-m-d represents a date in the civil (Gregorian) calendar
//                 m is in [1, 12]
//                 d is in [1, last_day_of_month(y, m)]
//                 y is "approximately" in
//                   [numeric_limits<Int>::min()/366, numeric_limits<Int>::max()/366]
//                 Exact range of validity is:
//                 [civil_from_days(numeric_limits<Int>::min()),
//                  civil_from_days(numeric_limits<Int>::max()-719468)]
template <class Int>
Int
days_from_civil(Int y, unsigned m, unsigned d)
{
    static_assert(std::numeric_limits<unsigned>::digits >= 18,
    "This algorithm has not been ported to a 16 bit unsigned integer");
    static_assert(std::numeric_limits<Int>::digits >= 20,
        "This algorithm has not been ported to a 16 bit signed integer");
    y -= m <= 2;
    const Int era = (y >= 0 ? y : y - 399) / 400;
    const unsigned yoe = static_cast<unsigned>(y - era * 400);      // [0, 399]
    const unsigned doy = (153 * (m + (m > 2 ? -3 : 9)) + 2) / 5 + d - 1;  // [0, 365]
    const unsigned doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;         // [0, 146096]
    return era * 146097 + static_cast<Int>(doe)-719468;
}

// Returns year/month/day triple in civil calendar
// Preconditions:  z is number of days since 1970-01-01 and is in the range:
//                   [numeric_limits<Int>::min(), numeric_limits<Int>::max()-719468].
template <class Int>
std::tuple<Int, unsigned, unsigned>
civil_from_days(Int z)
{
    static_assert(std::numeric_limits<unsigned>::digits >= 18,
    "This algorithm has not been ported to a 16 bit unsigned integer");
    static_assert(std::numeric_limits<Int>::digits >= 20,
        "This algorithm has not been ported to a 16 bit signed integer");
    z += 719468;
    const Int era = (z >= 0 ? z : z - 146096) / 146097;
    const unsigned doe = static_cast<unsigned>(z - era * 146097);          // [0, 146096]
    const unsigned yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;  // [0, 399]
    const Int y = static_cast<Int>(yoe)+era * 400;
    const unsigned doy = doe - (365 * yoe + yoe / 4 - yoe / 100);                // [0, 365]
    const unsigned mp = (5 * doy + 2) / 153;                                   // [0, 11]
    const unsigned d = doy - (153 * mp + 2) / 5 + 1;                             // [1, 31]
    const unsigned m = mp + (mp < 10 ? 3 : -9);                            // [1, 12]
    return std::tuple<Int, unsigned, unsigned>(y + (m <= 2), m, d);
}

typedef std::chrono::duration<int, std::ratio<86400>> days;

// time to ISO 8601 string 'yyyy-MM-ddTHH:mm:ssZ'
std::wstring DateTimeUtcToString(std::chrono::system_clock::time_point Time)
{
    auto t = Time.time_since_epoch();
    auto days_from_epoch = std::chrono::duration_cast<days>(t);
    auto tu = civil_from_days<int>(days_from_epoch.count());
    auto year = std::get<0>(tu);
    auto month = std::get<1>(tu);
    auto day = std::get<2>(tu);
    t -= days_from_epoch;
    auto hour = std::chrono::duration_cast<std::chrono::hours>(t);
    t -= hour;
    auto minute = std::chrono::duration_cast<std::chrono::minutes>(t);
    t -= minute;
    auto second = std::chrono::duration_cast<std::chrono::seconds>(t);
    t -= second;

    std::wstringstream s;
    s.fill('0');
    s << std::setw(4) << year << L'-' << std::setw(2) << month << L'-' << std::setw(2) << day;
    s << L'T';
    s << std::setw(2) << hour.count() << L':' << std::setw(2) << minute.count() << L':' << std::setw(2) << second.count();
    s << L'Z';

    return std::wstring(s.str());
}

// parse ISO 8601 string 'yyyy-MM-ddTHH:mm:ssZ'
std::chrono::system_clock::time_point StringToDateTimeUtc(std::wstring s)
{
    static std::wregex rIso8601(L"(-?[0-9]+)-([0-9]+)-([0-9]+)T([0-9]+):([0-9]+):([0-9]+)Z");

    std::wsmatch m;

    if (!std::regex_match(s, m, rIso8601))
    {
        throw std::logic_error("InvalidFormat");
    }

    auto year = Parse<int>(m[1].str());
    auto month = Parse<unsigned>(m[2].str());
    auto day = Parse<unsigned>(m[3].str());
    auto hour = Parse<unsigned>(m[4].str());
    auto minute = Parse<unsigned>(m[5].str());
    auto second = Parse<unsigned>(m[6].str());

    auto days_from_epoch = (days)(days_from_civil(year, month, day));
    auto t = (std::chrono::system_clock::time_point)(days_from_epoch);
    t += (std::chrono::hours)(hour);
    t += (std::chrono::minutes)(minute);
    t += (std::chrono::seconds)(second);

    return t;
}

std::chrono::system_clock::time_point UtcNow()
{
    auto now = std::chrono::system_clock::now();
    return now;
}

