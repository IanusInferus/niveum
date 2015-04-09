#pragma once

#include "Clients/ISerializationClient.h"

#include "Communication.h"
#include "CommunicationBinary.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
#include <unordered_map>
#include <string>
#include <functional>
#include <boost/asio.hpp>
#include <boost/date_time.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    class BinarySerializationClientAdapter : public IBinarySerializationClientAdapter
    {
    private:
        int NumTimeoutMilliseconds;
        int RequestCount;
        static int MaxRequestCount() { return 16; }

        class BinarySender : public Communication::Binary::IBinarySender
        {
        private:
            BinarySerializationClientAdapter &a;
        public:
            BinarySender(BinarySerializationClientAdapter &a)
                : a(a)
            {
            }

            void Send(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
            {
                if (a.ClientCommandSent != nullptr)
                {
                    a.ClientCommandSent(CommandName);
                }
                CommandRequest cq = {};
                cq.Name = CommandName;
                auto Time = boost::posix_time::microsec_clock::universal_time();
                cq.Time = Time;
                auto Finished = std::make_shared<bool>(false);
                auto Timer = std::make_shared<boost::asio::deadline_timer>(a.io_service);
                Timer->expires_from_now(boost::posix_time::milliseconds(a.NumTimeoutMilliseconds));
                Timer->async_wait([=](const boost::system::error_code& error)
                {
                    if (error == boost::system::errc::success)
                    {
                        if (!*Finished)
                        {
                            throw boost::system::system_error(boost::system::errc::timed_out, boost::system::generic_category());
                        }
                    }
                });
                cq.Timer = Timer;
                cq.Finished = Finished;
                if (a.CommandRequests->count(CommandName) > 0)
                {
                    auto q = (*a.CommandRequests)[CommandName];
                    q->push(cq);
                }
                else
                {
                    auto q = std::make_shared<std::queue<CommandRequest>>();
                    q->push(cq);
                    (*a.CommandRequests)[CommandName] = q;
                }
                if (a.RequestCount < MaxRequestCount())
                {
                    if (a.CommandQueue->size() > 0)
                    {
                        auto c = std::make_shared<CommandContent>();
                        c->CommandName = CommandName;
                        c->CommandHash = CommandHash;
                        c->Parameters = Parameters;
                        a.CommandQueue->push(c);
                        a.SendNotFlushedRequest();
                    }
                    else
                    {
                        a.RequestCount += 1;
                        if (a.ClientEvent != nullptr)
                        {
                            a.ClientEvent(CommandName, CommandHash, Parameters);
                        }
                    }
                }
                else
                {
                    auto c = std::make_shared<CommandContent>();
                    c->CommandName = CommandName;
                    c->CommandHash = CommandHash;
                    c->Parameters = Parameters;
                    a.CommandQueue->push(c);
                }
            }
        };

        std::shared_ptr<Communication::Binary::BinarySerializationClient> bc;
        boost::asio::io_service &io_service;

        struct CommandRequest
        {
            std::wstring Name;
            boost::posix_time::ptime Time;
            std::shared_ptr<boost::asio::deadline_timer> Timer;
            std::shared_ptr<bool> Finished;
        };
        struct CommandContent
        {
            std::wstring CommandName;
            std::uint32_t CommandHash;
            std::shared_ptr<std::vector<std::uint8_t>> Parameters;
        };

        std::shared_ptr<std::unordered_map<std::wstring, std::shared_ptr<std::queue<CommandRequest>>>> CommandRequests;
        std::shared_ptr<std::queue<std::shared_ptr<CommandContent>>> CommandQueue;

        void SendNotFlushedRequest()
        {
            while ((CommandQueue->size() > 0) && (RequestCount < MaxRequestCount()))
            {
                RequestCount += 1;
                auto c = CommandQueue->front();
                CommandQueue->pop();
                if (ClientEvent != nullptr)
                {
                    ClientEvent(c->CommandName, c->CommandHash, c->Parameters);
                }
            }
        }

    public:
        std::function<void(std::wstring)> ClientCommandSent;
        std::function<void(std::wstring, int)> ClientCommandReceived;
        std::function<void(std::wstring, int)> ClientCommandFailed;
        std::function<void(std::wstring)> ServerCommandReceived;

        BinarySerializationClientAdapter(boost::asio::io_service &io_service, int NumTimeoutMilliseconds)
            : io_service(io_service), NumTimeoutMilliseconds(NumTimeoutMilliseconds), RequestCount(0)
        {
            CommandRequests = std::make_shared<std::unordered_map<std::wstring, std::shared_ptr<std::queue<CommandRequest>>>>();
            CommandQueue = std::make_shared<std::queue<std::shared_ptr<CommandContent>>>();
            this->bc = std::make_shared<Communication::Binary::BinarySerializationClient>(std::make_shared<BinarySender>(*this));
            auto ac = this->bc->GetApplicationClient();
            ac->ErrorCommand = [=](std::shared_ptr<Communication::ErrorCommandEvent> e)
            {
                auto CommandName = e->CommandName;
                if (CommandRequests->count(CommandName) > 0)
                {
                    auto q = (*CommandRequests)[CommandName];
                    auto &cq = q->front();

                    *cq.Finished = true;
                    auto TimeSpan = boost::posix_time::microsec_clock::universal_time() - cq.Time;
                    auto Milliseconds = (int)(TimeSpan.total_milliseconds());
                    if (ClientCommandFailed != nullptr)
                    {
                        ClientCommandFailed(cq.Name, Milliseconds);
                    }
                    q->pop();
                    if (q->size() == 0)
                    {
                        CommandRequests->erase(CommandName);
                    }
                    RequestCount -= 1;
                    SendNotFlushedRequest();
                }
                this->bc->GetApplicationClient()->DequeueCallback(e->CommandName);
            };
        }

        std::shared_ptr<Communication::IApplicationClient> GetApplicationClient()
        {
            return bc->GetApplicationClient();
        }

        virtual std::uint64_t Hash()
        {
            return bc->GetApplicationClient()->Hash();
        }
        virtual void HandleResult(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
        {
            if (CommandRequests->count(CommandName) > 0)
            {
                auto q = (*CommandRequests)[CommandName];
                auto &cq = q->front();

                *cq.Finished = true;
                auto TimeSpan = boost::posix_time::microsec_clock::universal_time() - cq.Time;
                auto Milliseconds = (int)(TimeSpan.total_milliseconds());
                if (ClientCommandReceived != nullptr)
                {
                    ClientCommandReceived(cq.Name, Milliseconds);
                }
                q->pop();
                if (q->size() == 0)
                {
                    CommandRequests->erase(CommandName);
                }
                RequestCount -= 1;
                SendNotFlushedRequest();
            }
            else
            {
                if (ServerCommandReceived != nullptr)
                {
                    ServerCommandReceived(CommandName);
                }
            }
            bc->HandleResult(CommandName, CommandHash, Parameters);
        }

        void ClearRequests()
        {
            auto ac = bc->GetApplicationClient();
            for (auto p : *CommandRequests)
            {
                auto q = std::get<1>(p);
                while (!q->empty())
                {
                    auto &cq = q->front();
                    *cq.Finished = true;
                    ac->DequeueCallback(cq.Name);
                    q->pop();
                }
            }
            CommandRequests->clear();
        }
    };
}
