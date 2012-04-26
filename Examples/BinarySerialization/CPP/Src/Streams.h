//==========================================================================
//
//  File:        Streams.h
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 文件流
//  Version:     2012.04.26.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma once

#include "World.h"
#include "WorldBinary.h"

#include <cstdio>

namespace World
{
    namespace Streams
    {
        using namespace std;

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
                    throw runtime_error("IOException");
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
                    throw runtime_error("IOException");
                }
                return b;
            }
            shared_ptr<vector<uint8_t>> ReadBytes(size_t Size)
            {
                throw runtime_error("NotSupported");
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
                    throw runtime_error("IOException");
                }
            }
            ~WritableStream()
            {
                fclose(f);
            }

            virtual void WriteByte(uint8_t b)
            {
                if (fwrite(&b, 1, 1, f) != 1)
                {
                    throw runtime_error("IOException");
                }
            }
            virtual void WriteBytes(shared_ptr<vector<uint8_t>> l)
            {
                throw runtime_error("NotSupported");
            }
        };
    }
}
