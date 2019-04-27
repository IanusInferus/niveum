#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

#include <cppconn/connection.h>
#include <cppconn/prepared_statement.h>

#include "Database.h"

namespace Database
{
    namespace MySql
    {
        class DataAccessBase
        {
        private:
            std::shared_ptr<sql::Connection> con;

        public:
            DataAccessBase(std::wstring ConnectionString);

            virtual ~DataAccessBase();

            void Complete();

            std::wstring ConvertToWString(sql::SQLString s);

            sql::SQLString ConvertToSQLString(std::wstring s);

            std::shared_ptr<sql::PreparedStatement> CreateTextCommand(std::wstring CommandText);

            void Add(std::wstring ParameterName, bool Value);

            void Add(std::wstring ParameterName, std::wstring Value);

            void Add(std::wstring ParameterName, int Value);

            void Add(std::wstring ParameterName, double Value);

            void Add(std::wstring ParameterName, std::shared_ptr<std::vector<std::uint8_t>> Value);

            void AddNull(std::wstring ParameterName);

            bool GetBoolean(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::wstring GetString(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            int GetInt(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            double GetReal(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::shared_ptr<std::vector<std::uint8_t>> GetBinary(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::optional<bool> GetOptionalOfBoolean(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::optional<std::wstring> GetOptionalOfString(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::optional<int> GetOptionalOfInt(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::optional<double> GetOptionalOfReal(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);

            std::optional<std::shared_ptr<std::vector<std::uint8_t>>> GetOptionalOfBinary(std::shared_ptr<sql::ResultSet> rs, std::wstring FieldName);
        };
    }
}
