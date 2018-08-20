#include "DataAccessManager.h"

#include "MySql/DataAccessImplementation.h"

namespace Database
{
    DataAccessManager::DataAccessManager(std::wstring ConnectionString)
        : ConnectionString(ConnectionString)
    {
    }

    std::shared_ptr<Database::IDataAccess> DataAccessManager::Create()
    {
        auto da = std::make_shared<MySql::DataAccessImplementation>(ConnectionString);
        return da;
    }

    std::wstring DataAccessManager::GetProgramName()
    {
        return L"DatabaseMySQL";
    }

    std::wstring DataAccessManager::GetConnectionStringExample()
    {
        return L"server=localhost;uid=root;pwd=password;database=Test;";
    }
}