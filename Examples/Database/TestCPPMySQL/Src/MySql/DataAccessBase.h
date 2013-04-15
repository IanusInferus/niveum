#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

#include <cppconn/connection.h>
#include <cppconn/prepared_statement.h>

#include "DatabaseEntities.h"

namespace Database
{
    namespace _Impl
    {
        using namespace std;
        using namespace sql;

        class DataAccessBase
        {
        private:
            shared_ptr<Connection> con;

        public:
            DataAccessBase(wstring ConnectionString);

            virtual ~DataAccessBase();

            void Complete();

            wstring ConvertToWString(SQLString s);

            SQLString ConvertToSQLString(wstring s);

            shared_ptr<PreparedStatement> CreateTextCommand(wstring CommandText);

            void Add(wstring ParameterName, bool Value);

            void Add(wstring ParameterName, wstring Value);

            void Add(wstring ParameterName, int Value);

            void Add(wstring ParameterName, double Value);

            void Add(wstring ParameterName, shared_ptr<vector<uint8_t>> Value);

            void AddNull(wstring ParameterName);

            bool GetBoolean(shared_ptr<ResultSet> rs, wstring FieldName);

            wstring GetString(shared_ptr<ResultSet> rs, wstring FieldName);

            int GetInt(shared_ptr<ResultSet> rs, wstring FieldName);

            double GetReal(shared_ptr<ResultSet> rs, wstring FieldName);

            shared_ptr<vector<uint8_t>> GetBinary(shared_ptr<ResultSet> rs, wstring FieldName);

            Optional<bool> GetOptionalOfBoolean(shared_ptr<ResultSet> rs, wstring FieldName);

            Optional<wstring> GetOptionalOfString(shared_ptr<ResultSet> rs, wstring FieldName);

            Optional<int> GetOptionalOfInt(shared_ptr<ResultSet> rs, wstring FieldName);

            Optional<double> GetOptionalOfReal(shared_ptr<ResultSet> rs, wstring FieldName);

            Optional<shared_ptr<vector<uint8_t>>> GetOptionalOfBinary(shared_ptr<ResultSet> rs, wstring FieldName);
        };
    }
}

namespace Database
{
    typedef _Impl::DataAccessBase DataAccessBase;
}
