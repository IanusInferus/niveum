#pragma once

#include "BaseSystem/StringUtilities.h"
#include "ExpressionSchema.h"
#include "ExpressionSchemaBinary.h"

#include <cstdint>
#include <cstddef>

namespace Niveum
{
    namespace ExpressionSchema
    {
        class ReadableStreamImpl;
        class ReadableStream : public IReadableStream
        {
        private:
            std::unique_ptr<ReadableStreamImpl> impl_;
        public:
            ReadableStream(String Path);
            virtual ~ReadableStream();
            void Close();

            std::uint8_t ReadByte() override;
            std::vector<std::uint8_t> ReadBytes(std::size_t Size) override;
        };

        class WritableStreamImpl;
        class WritableStream : public IWritableStream
        {
        private:
            std::unique_ptr<WritableStreamImpl> impl_;
        public:
            WritableStream(String Path);
            virtual ~WritableStream();
            void Close();

            void WriteByte(std::uint8_t b) override;
            void WriteBytes(const std::vector<std::uint8_t> & l) override;
        };
    }
}
