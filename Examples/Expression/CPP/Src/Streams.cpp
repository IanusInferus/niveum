#if !defined(_CRT_SECURE_NO_WARNINGS) && (defined WIN32 || defined _WIN32)
#   define _CRT_SECURE_NO_WARNINGS
#endif

#include "Streams.h"

#include <stdexcept>
#include <ios>
#include <stdio.h>
#include <string.h>

namespace Niveum
{
namespace ExpressionSchema
{

class ReadableStreamImpl
{
public:
    String Path;
    FILE * f{nullptr};
    std::streampos Position{0};
    std::streampos FileLength{0};
};

ReadableStream::ReadableStream(String Path)
{
    impl_ = std::make_unique<ReadableStreamImpl>();
    impl_->Path = Path;
#if defined WIN32 || defined _WIN32
    auto err = _wfopen_s(&impl_->f, utf16ToWideChar(Path).c_str(), L"rb");
    if (err != 0)
    {
        throw std::runtime_error(strerror(err) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position = static_cast<std::streampos>(_ftelli64(impl_->f));
    if (_fseeki64(impl_->f, 0, SEEK_END) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->FileLength = static_cast<std::streampos>(_ftelli64(impl_->f));
    if (_fseeki64(impl_->f, 0, SEEK_SET) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
#else
    impl_->f = fopen(utf16ToSystem(Path).c_str(), "rb");
    if (impl_->f == nullptr)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position = static_cast<std::streampos>(ftello(impl_->f));
    if (fseeko(impl_->f, 0, SEEK_END) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->FileLength = static_cast<std::streampos>(ftello(impl_->f));
    if (fseeko(impl_->f, 0, SEEK_SET) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
#endif
}

ReadableStream::~ReadableStream()
{
    fclose(impl_->f);
}

void ReadableStream::Close()
{
    if (fclose(impl_->f) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
}

std::uint8_t ReadableStream::ReadByte()
{
    std::uint8_t b = 0;
    if (fread(&b, 1, 1, impl_->f) != 1)
    {
        throw std::runtime_error(strerror(ferror(impl_->f)) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position += 1;
    return b;
}
std::vector<std::uint8_t> ReadableStream::ReadBytes(std::size_t Size)
{
    if (impl_->Position + static_cast<std::streampos>(Size) > impl_->FileLength)
    {
        throw std::runtime_error("EndOfFile" + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    std::vector<std::uint8_t> l;
    l.resize(Size);
    if (fread(l.data(), 1, Size, impl_->f) != Size)
    {
        throw std::runtime_error(strerror(ferror(impl_->f)) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position += Size;
    return l;
}

class WritableStreamImpl
{
public:
    String Path;
    FILE * f{nullptr};
    std::streampos Position{0};
};

WritableStream::WritableStream(String Path)
{
    impl_ = std::make_unique<WritableStreamImpl>();
    impl_->Path = Path;
#if defined WIN32 || defined _WIN32
    auto err = _wfopen_s(&impl_->f, utf16ToWideChar(Path).c_str(), L"wb");
    if (err != 0)
    {
        throw std::runtime_error(strerror(err) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position = static_cast<std::streampos>(_ftelli64(impl_->f));
#else
    impl_->f = fopen(utf16ToSystem(Path).c_str(), "wb");
    if (impl_->f == nullptr)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position = static_cast<std::streampos>(ftello(impl_->f));
#endif
}

WritableStream::~WritableStream()
{
    fclose(impl_->f);
}

void WritableStream::Close()
{
    if (fclose(impl_->f) != 0)
    {
        throw std::runtime_error(strerror(errno) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
}

void WritableStream::WriteByte(std::uint8_t b)
{
    if (fwrite(&b, 1, 1, impl_->f) != 1)
    {
        throw std::runtime_error(strerror(ferror(impl_->f)) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position += 1;
}
void WritableStream::WriteBytes(const std::vector<std::uint8_t> & l)
{
    if (fwrite(l.data(), 1, l.size(), impl_->f) != l.size())
    {
        throw std::runtime_error(strerror(ferror(impl_->f)) + std::string(": ") + utf16ToSystem(impl_->Path));
    }
    impl_->Position += l.size();
}

}
}
