#pragma once

#include "World.h"
#include "WorldBinary.h"

#include <cstdint>
#include <cstdio>
#include <stdexcept>

namespace World
{
    namespace Streams
    {
        class ReadableStream : public IReadableStream
        {
        private:
            FILE *f;
        public:
            ReadableStream(String Path)
            {
                f = fopen(w2s(Path).c_str(), "rb");
                if (f == NULL)
                {
                    throw std::runtime_error("IOException");
                }
            }
            ~ReadableStream()
            {
                fclose(f);
            }

            uint8_t ReadByte()
            {
                uint8_t b = 0;
                if (fread(&b, 1, 1, f) != 1)
                {
                    throw std::runtime_error("IOException");
                }
                return b;
            }
            std::shared_ptr<std::vector<std::uint8_t>> ReadBytes(size_t Size)
            {
                throw std::runtime_error("NotSupported");
            }
        };

        class WritableStream : public IWritableStream
        {
        private:
            FILE *f;
        public:
            WritableStream(String Path)
            {
                f = fopen(w2s(Path).c_str(), "wb");
                if (f == NULL)
                {
                    throw std::runtime_error("IOException");
                }
            }
            ~WritableStream()
            {
                fclose(f);
            }

            virtual void WriteByte(std::uint8_t b)
            {
                if (fwrite(&b, 1, 1, f) != 1)
                {
                    throw std::runtime_error("IOException");
                }
            }
            virtual void WriteBytes(std::shared_ptr<std::vector<std::uint8_t>> l)
            {
                throw std::runtime_error("NotSupported");
            }
        };
    }
}
