#pragma once

#include "BaseSystem/StringUtilities.h"
#include "World.h"
#include "WorldBinary.h"

#include <cstdint>
#include <cstddef>
#include <stdexcept>
#include <fstream>

namespace World
{
    namespace Streams
    {
        class ReadableStream : public IReadableStream
        {
        private:
            std::ifstream s;
        public:
            ReadableStream(String Path)
            {
#if defined WIN32 || defined _WIN32
                s.open(Path, std::ifstream::binary);
#else
                s.open(w2s(Path), std::ifstream::binary);
#endif
                if (!s)
                {
                    throw std::runtime_error("IOException");
                }
            }

            uint8_t ReadByte() override
            {
                uint8_t b = 0;
                if (!s.get(reinterpret_cast<char &>(b)))
                {
                    throw std::runtime_error("IOException");
                }
                return b;
            }
            std::vector<std::uint8_t> ReadBytes(std::size_t Size) override
            {
                std::vector<std::uint8_t> l;
                l.resize(Size);
                if (!s.read(reinterpret_cast<char *>(l.data()), Size))
                {
                    throw std::runtime_error("IOException");
                }
                return l;
            }
        };

        class WritableStream : public IWritableStream
        {
        private:
            std::ofstream s;
        public:
            WritableStream(String Path)
            {
#if defined WIN32 || defined _WIN32
                s.open(Path, std::ofstream::binary);
#else
                s.open(w2s(Path), std::ofstream::binary);
#endif
                if (!s)
                {
                    throw std::runtime_error("IOException");
                }
            }

            void WriteByte(std::uint8_t b) override
            {
                if (!s.put(static_cast<char>(b)))
                {
                    throw std::runtime_error("IOException");
                }
            }
            void WriteBytes(const std::vector<std::uint8_t> & l) override
            {
                if (!s.write(reinterpret_cast<const char *>(l.data()), l.size()))
                {
                    throw std::runtime_error("IOException");
                }
            }
        };
    }
}
