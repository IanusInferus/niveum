#pragma once

#include "Database.h"

#include <memory>
#include <string>

namespace Database
{
    class DataAccessManager
    {
    private:
        std::wstring ConnectionString;
    public:
        DataAccessManager(std::wstring ConnectionString);

        std::shared_ptr<Database::IDataAccess> Create();
        static std::wstring GetProgramName();
        static std::wstring GetConnectionStringExample();
    };
}