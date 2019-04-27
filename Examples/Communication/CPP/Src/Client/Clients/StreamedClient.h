#pragma once

#include <cstdint>
#include <vector>
#include <string>
#include <memory>
#include <functional>

#ifndef _UNIT_TYPE_
typedef struct {} Unit;
#define _UNIT_TYPE_
#endif
typedef bool Boolean;

namespace Client
{
    class StreamedVirtualTransportClientHandleResultCommand
    {
    public:
        std::u16string CommandName;
        std::function<void()> HandleResult;
    };

    enum StreamedVirtualTransportClientHandleResultTag
    {
        StreamedVirtualTransportClientHandleResultTag_Continue = 0,
        StreamedVirtualTransportClientHandleResultTag_Command = 1
    };
    /* TaggedUnion */
    class StreamedVirtualTransportClientHandleResult /* final */
    {
    public:
        /* Tag */ StreamedVirtualTransportClientHandleResultTag _Tag;

        Unit Continue;
        std::shared_ptr<StreamedVirtualTransportClientHandleResultCommand> Command;

        static std::shared_ptr<class StreamedVirtualTransportClientHandleResult> CreateContinue()
        {
            auto r = std::make_shared<StreamedVirtualTransportClientHandleResult>();
            r->_Tag = StreamedVirtualTransportClientHandleResultTag_Continue;
            r->Continue = Unit();
            return r;
        }
        static std::shared_ptr<class StreamedVirtualTransportClientHandleResult> CreateCommand(std::shared_ptr<StreamedVirtualTransportClientHandleResultCommand> Value)
        {
            auto r = std::make_shared<StreamedVirtualTransportClientHandleResult>();
            r->_Tag = StreamedVirtualTransportClientHandleResultTag_Command;
            r->Command = Value;
            return r;
        }

        Boolean OnContinue() const
        {
            return _Tag == StreamedVirtualTransportClientHandleResultTag_Continue;
        }
        Boolean OnCommand() const
        {
            return _Tag == StreamedVirtualTransportClientHandleResultTag_Command;
        }
    };

    class IStreamedVirtualTransportClient
    {
    public:
        virtual ~IStreamedVirtualTransportClient() {}

        virtual std::shared_ptr<std::vector<std::uint8_t>> GetReadBuffer() = 0;
        virtual int GetReadBufferOffset() = 0;
        virtual int GetReadBufferLength() = 0;
        virtual std::vector<std::shared_ptr<std::vector<std::uint8_t>>> TakeWriteBuffer() = 0;
        virtual std::shared_ptr<StreamedVirtualTransportClientHandleResult> Handle(int Count) = 0;
        virtual std::uint64_t Hash() = 0;
        std::function<void()> ClientMethod;
    };
}
