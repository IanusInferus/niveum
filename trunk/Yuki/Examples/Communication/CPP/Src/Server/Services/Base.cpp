#include "Services/ServerImplementation.h"

#include <memory>
#include <string>
#include <boost/date_time/posix_time/posix_time.hpp>

using namespace std;
using namespace boost::posix_time;
using namespace Communication;
using namespace Server;

/// <summary>服务器时间</summary>
shared_ptr<ServerTimeReply> ServerImplementation::ServerTime(shared_ptr<ServerTimeRequest> r)
{
    ptime now = second_clock::universal_time();
    auto s = to_iso_extended_wstring(now) + L"Z"; //yyyy-MM-ddTHH:mm:ssZ
    return ServerTimeReply::CreateSuccess(s);
}

/// <summary>退出</summary>
shared_ptr<QuitReply> ServerImplementation::Quit(shared_ptr<QuitRequest> r)
{
    c->RaiseQuit();
    return QuitReply::CreateSuccess();
}
