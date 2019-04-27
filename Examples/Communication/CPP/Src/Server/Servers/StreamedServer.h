#pragma once

#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <memory>
#include <exception>
#include <stdexcept>

#ifndef _UNIT_TYPE_
typedef struct {} Unit;
#define _UNIT_TYPE_
#endif
typedef bool Boolean;

namespace Server
{
    class StreamedVirtualTransportServerHandleResultCommand
    {
    public:
        std::u16string CommandName;
        std::function<void(std::function<void()>, std::function<void(const std::exception &)>)> ExecuteCommand;
    };

    class StreamedVirtualTransportServerHandleResultBadCommand
    {
    public:
        std::u16string CommandName;
    };

    class StreamedVirtualTransportServerHandleResultBadCommandLine
    {
    public:
        std::u16string CommandLine;
    };

    enum StreamedVirtualTransportServerHandleResultTag
    {
        StreamedVirtualTransportServerHandleResultTag_Continue = 0,
        StreamedVirtualTransportServerHandleResultTag_Command = 1,
        StreamedVirtualTransportServerHandleResultTag_BadCommand = 2,
        StreamedVirtualTransportServerHandleResultTag_BadCommandLine = 3
    };
    /* TaggedUnion */
    class StreamedVirtualTransportServerHandleResult /* final */
    {
    public:
        /* Tag */ StreamedVirtualTransportServerHandleResultTag _Tag;
        Unit Continue;
        std::shared_ptr<StreamedVirtualTransportServerHandleResultCommand> Command;
        std::shared_ptr<StreamedVirtualTransportServerHandleResultBadCommand> BadCommand;
        std::shared_ptr<StreamedVirtualTransportServerHandleResultBadCommandLine> BadCommandLine;

        static std::shared_ptr<class StreamedVirtualTransportServerHandleResult> CreateContinue()
        {
            auto r = std::make_shared<StreamedVirtualTransportServerHandleResult>();
            r->_Tag = StreamedVirtualTransportServerHandleResultTag_Continue;
            r->Continue = Unit();
            return r;
        }
        static std::shared_ptr<class StreamedVirtualTransportServerHandleResult> CreateCommand(std::shared_ptr<StreamedVirtualTransportServerHandleResultCommand> Value)
        {
            auto r = std::make_shared<StreamedVirtualTransportServerHandleResult>();
            r->_Tag = StreamedVirtualTransportServerHandleResultTag_Command;
            r->Command = Value;
            return r;
        }
        static std::shared_ptr<class StreamedVirtualTransportServerHandleResult> CreateBadCommand(std::shared_ptr<StreamedVirtualTransportServerHandleResultBadCommand> Value)
        {
            auto r = std::make_shared<StreamedVirtualTransportServerHandleResult>();
            r->_Tag = StreamedVirtualTransportServerHandleResultTag_BadCommand;
            r->BadCommand = Value;
            return r;
        }
        static std::shared_ptr<class StreamedVirtualTransportServerHandleResult> CreateBadCommandLine(std::shared_ptr<StreamedVirtualTransportServerHandleResultBadCommandLine> Value)
        {
            auto r = std::make_shared<StreamedVirtualTransportServerHandleResult>();
            r->_Tag = StreamedVirtualTransportServerHandleResultTag_BadCommandLine;
            r->BadCommandLine = Value;
            return r;
        }

        Boolean OnContinue() const
        {
            return _Tag == StreamedVirtualTransportServerHandleResultTag_Continue;
        }
        Boolean OnCommand() const
        {
            return _Tag == StreamedVirtualTransportServerHandleResultTag_Command;
        }
        Boolean OnBadCommand() const
        {
            return _Tag == StreamedVirtualTransportServerHandleResultTag_BadCommand;
        }
        Boolean OnBadCommandLine() const
        {
            return _Tag == StreamedVirtualTransportServerHandleResultTag_BadCommandLine;
        }
    };

    class IStreamedVirtualTransportServer
    {
    public:
        virtual ~IStreamedVirtualTransportServer() {}

        virtual std::shared_ptr<std::vector<std::uint8_t>> GetReadBuffer() = 0;
        virtual int GetReadBufferOffset() = 0;
        virtual int GetReadBufferLength() = 0;
        virtual std::vector<std::shared_ptr<std::vector<std::uint8_t>>> TakeWriteBuffer() = 0;
        virtual std::shared_ptr<StreamedVirtualTransportServerHandleResult> Handle(int Count) = 0;
        virtual std::uint64_t Hash() = 0;
        std::function<void()> ServerEvent;
        std::function<void(std::u16string, std::size_t)> InputByteLengthReport;
        std::function<void(std::u16string, std::size_t)> OutputByteLengthReport;
    };
}
