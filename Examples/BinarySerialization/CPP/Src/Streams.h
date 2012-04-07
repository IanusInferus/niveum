//==========================================================================
//
//  File:        Streams.h
//  Location:    Yuki.Examples <Visual C++ 2010>
//  Description: ÎÄ¼þÁ÷
//  Version:     2012.04.07.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma once

#include "World.h"
#include "WorldBinary.h"

#include <cstdio>

#pragma warning (disable : 4996)

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
                f = _wfopen(Path.c_str(), L"rb");
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
            vector<uint8_t> ReadBytes(size_t Size)
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
                f = _wfopen(Path.c_str(), L"wb");
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
            virtual void WriteBytes(vector<uint8_t> l)
            {
                throw runtime_error("NotSupported");
            }
        };
    }
}
