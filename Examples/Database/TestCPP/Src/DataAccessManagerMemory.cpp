#include "DataAccessManager.h"
#include "Memory/MemoryDataAccess.h"
#include "BaseSystem/StringUtilities.h"

#include <fstream>

namespace Database
{
    DataAccessManager::DataAccessManager(std::wstring ConnectionString)
        : ConnectionString(ConnectionString)
    {
    }

    std::shared_ptr<Database::IDataAccess> DataAccessManager::Create()
    {
        std::ifstream s;
#if defined WIN32 || defined _WIN32
        s.open(ConnectionString, std::ifstream::binary);
#else
        s.open(wideCharToSystem(ConnectionString), std::ifstream::binary);
#endif
        s.seekg(0, s.end);
        auto size = static_cast<std::size_t>(s.tellg());
        std::vector<std::uint8_t> buffer;
        buffer.resize(size);
        s.seekg(0, s.beg);
        s.read(reinterpret_cast<char *>(buffer.data()), size);
        auto da = std::make_shared<Memory::MemoryDataAccess>(buffer);
        return da;
    }

    std::wstring DataAccessManager::GetProgramName()
    {
        return L"DatabaseMemory";
    }

    std::wstring DataAccessManager::GetConnectionStringExample()
    {
        return L"Test.kd";
    }
}