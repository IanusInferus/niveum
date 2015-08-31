#include "SerializationServerAdapter.h"

#include "BaseSystem/ThreadLocalVariable.h"
#include "BaseSystem/ExceptionStackTrace.h"

#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <memory>
#include <exception>
#include <typeinfo>

namespace Server
{
    static std::shared_ptr<BaseSystem::ThreadLocalVariable<Communication::Binary::BinarySerializationServer>> sss = std::make_shared<BaseSystem::ThreadLocalVariable<Communication::Binary::BinarySerializationServer>>([]() { return std::make_shared<Communication::Binary::BinarySerializationServer>(); });

    BinarySerializationServerAdapter::BinarySerializationServerAdapter(std::shared_ptr<Communication::IApplicationServer> ApplicationServer)
    {
        s = ApplicationServer;
        ss = sss->Value();
        ssed = std::make_shared<Communication::Binary::BinarySerializationServerEventDispatcher>(ApplicationServer);
        ssed->ServerEvent = [=](std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
        {
            if (ServerEvent != nullptr)
            {
                ServerEvent(CommandName, CommandHash, Parameters);
            }
        };
    }

    std::uint64_t BinarySerializationServerAdapter::Hash()
    {
        return ss->Hash();
    }
    bool BinarySerializationServerAdapter::HasCommand(std::wstring CommandName, std::uint32_t CommandHash)
    {
        return ss->HasCommand(CommandName, CommandHash) || ss->HasCommandAsync(CommandName, CommandHash);
    }
    void BinarySerializationServerAdapter::ExecuteCommand(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters, std::function<void(std::shared_ptr<std::vector<std::uint8_t>>)> OnSuccess, std::function<void(const std::exception &)> OnFailure)
    {
        std::function<void()> a;
        if (ss->HasCommand(CommandName, CommandHash))
        {
            a = [=]
            {
                auto OutParameters = ss->ExecuteCommand(s, CommandName, CommandHash, Parameters);
                OnSuccess(OutParameters);
            };
        }
        else if (ss->HasCommandAsync(CommandName, CommandHash))
        {
            a = [=]
            {
                ss->ExecuteCommandAsync(s, CommandName, CommandHash, Parameters, [=](std::shared_ptr<std::vector<std::uint8_t>> OutParameters)
                {
                    OnSuccess(OutParameters);
                }, OnFailure);
            };
        }
        if (a == nullptr) { return; }
        if (ExceptionStackTrace::IsDebuggerAttached())
        {
            a();
        }
        else
        {
            try
            {
                ExceptionStackTrace::Execute(a);
            }
            catch (const std::exception &ex)
            {
                auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace();
                OnFailure(std::runtime_error(Message));
            }
        }
    }
}
