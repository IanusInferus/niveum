#include "DataAccessManager.h"

#include "MySql/DataAccessImplementation.h"

namespace Database
{
	DataAccessManager::DataAccessManager(std::wstring ConnectionString)
		: ConnectionString(ConnectionString)
	{
	}

	std::shared_ptr<IDataAccess> DataAccessManager::Create()
	{
		auto da = std::make_shared<DataAccessImplementation>(ConnectionString);
		return da;
	}
}