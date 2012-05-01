#pragma once

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <boost/asio.hpp>
#include <boost/thread.hpp>
#include <boost/format.hpp>

namespace Server
{
    class SessionContext : public std::enable_shared_from_this<SessionContext>
    {
    public:
        //���̹߳���ֻ������

        std::function<void()> Quit; //���߳��¼�(��������Ҫ��֤�̰߳�ȫ)
        void RaiseQuit()
        {
            if (Quit != nullptr) { Quit(); }
        }

        boost::asio::ip::tcp::endpoint RemoteEndPoint;

        std::shared_ptr<std::vector<std::uint8_t>> SessionToken;
        std::wstring GetSessionTokenString() const
        {
            std::wstring s;
            for (int k = 0; k < (int)(SessionToken->size()); k += 1)
            {
                auto b = (*SessionToken)[k];
                s += (boost::wformat(L"%2X") % b).str();
            }
            return s;
        }

        //���̹߳����д���ʱ�����
        //#include <boost/thread/locks.hpp>
        //дʱ�ȶ���boost::unique_lock<boost::shared_mutex> WriterLock(SessionLock);
        //��ʱ�ȶ���boost::shared_lock<boost::shared_mutex> ReaderLock(SessionLock);
        boost::shared_mutex SessionLock;


        //���̹߳����д���ʣ���д����ͨ��SessionLock

        int ReceivedMessageCount; //���̱߳���


        //���̷߳���

        int SendMessageCount;


        SessionContext()
            : SessionToken(std::make_shared<std::vector<std::uint8_t>>()),
              ReceivedMessageCount(0),
              SendMessageCount(0)
        {
        }
    };
}
