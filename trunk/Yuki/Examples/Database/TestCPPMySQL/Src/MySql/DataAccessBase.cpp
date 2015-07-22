#include "BaseSystem/Strings.h"
#include "DataAccessBase.h"

#include <exception>
#include <stdexcept>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>
#include <iterator>
#include <mutex>
#include <codecvt>
#include "UtfEncoding.h"
#include <streambuf>

#include <boost/regex.hpp>

#include <cppconn/driver.h>
#include <cppconn/connection.h>
#include <cppconn/statement.h>
#include <cppconn/prepared_statement.h>

namespace Database
{
    namespace _Impl
    {
        using namespace std;
        using namespace sql;

        static std::mutex DriverMutex;

        DataAccessBase::DataAccessBase(wstring ConnectionString)
        {
            boost::wregex rFieldDelimiter(L";");
            boost::wregex rPartDelimiter(L"=");
            boost::wregex rWhitespace(L"\\A\\s+|\\s+\\Z");
            boost::wregex rPort(L"\\d+");

            vector<wstring> Fields;
            boost::regex_split(back_inserter(Fields), std::wstring(ConnectionString), rFieldDelimiter);

            wstring Server;
            wstring Port = L"3306";
            wstring Uid;
            wstring Pwd;
            wstring DbName;

            for (int i = 0; i < static_cast<int>(Fields.size()); i += 1)
            {
                auto f = Fields[i];
                vector<wstring> Parts;
                boost::regex_split(back_inserter(Parts), std::wstring(f), rPartDelimiter);

                if (boost::regex_search(f, rPartDelimiter))
                {
                    if (Parts.size() == 0)
                    {
                        Parts.push_back(L"");
                        Parts.push_back(L"");
                    }
                    else if (Parts.size() == 1)
                    {
                        Parts.push_back(L"");
                    }
                }
                if (Parts.size() != 2) { throw logic_error("ConnectionStringInvalid: " + w2s(f)); }

                auto Name = boost::regex_replace(Parts[0], rWhitespace, L"");
                auto Value = boost::regex_replace(Parts[1], rWhitespace, L"");

                if (EqualIgnoreCase(Name, L"server"))
                {
                    Server = Value;
                }
                else if (EqualIgnoreCase(Name, L"port"))
                {
                    if (!boost::regex_match(Value, rPort)) { throw logic_error("ConnectionStringInvalid: " + w2s(f)); }
                    Port = Value;
                }
                else if (EqualIgnoreCase(Name, L"uid"))
                {
                    Uid = Value;
                }
                else if (EqualIgnoreCase(Name, L"pwd"))
                {
                    Pwd = Value;
                }
                else if (EqualIgnoreCase(Name, L"database"))
                {
                    DbName = Value;
                }
                else
                {
                    throw logic_error("ConnectionStringInvalid: " + w2s(f));
                }
            }

            if (Server == L"")
            {
                throw logic_error("ConnectionStringInvalid: FieldNotFound: server");
            }
            if (Uid == L"")
            {
                throw logic_error("ConnectionStringInvalid: FieldNotFound: uid");
            }
            if (DbName == L"")
            {
                throw logic_error("ConnectionStringInvalid: FieldNotFound: database");
            }

            {
                std::unique_lock<std::mutex> Lock(DriverMutex);
                sql::Driver *driver = get_driver_instance();
                sql::ConnectOptionsMap connectionProperties;
                connectionProperties["hostName"] = w2s(Server);
                connectionProperties["port"] = Parse<int>(Port);
                connectionProperties["userName"] = w2s(Uid);
                if (Pwd != L"")
                {
                    connectionProperties["password"] = w2s(Pwd);
                }
                driver->threadInit();
                con = shared_ptr<Connection>(driver->connect(connectionProperties));
            }
            auto stmt = shared_ptr<Statement>(con->createStatement());
            stmt->execute(ConvertToSQLString(L"USE " + DbName));
            stmt->execute("START TRANSACTION");
        }

        DataAccessBase::~DataAccessBase()
        {
            if (con != nullptr)
            {
                auto stmt = shared_ptr<Statement>(con->createStatement());
                stmt->execute("ROLLBACK");
                {
                    std::unique_lock<std::mutex> Lock(DriverMutex);
                    con->close();
                    con = nullptr;
                    sql::Driver *driver = get_driver_instance();
                    driver->threadEnd();
                }
            }
        }

        void DataAccessBase::Complete()
        {
            auto stmt = shared_ptr<Statement>(con->createStatement());
            stmt->execute("COMMIT");
            {
                std::unique_lock<std::mutex> Lock(DriverMutex);
                con->close();
                con = nullptr;
                sql::Driver *driver = get_driver_instance();
                driver->threadEnd();
            }
        }

        wstring DataAccessBase::ConvertToWString(SQLString s)
        {
            wstring_convert<codecvt_utf8<wchar_t, 0x10FFFF, little_endian>, wchar_t> conv;
            return conv.from_bytes(reinterpret_cast<const char *>(s->data()), reinterpret_cast<const char *>(s->data() + s->size()));
        }

        SQLString DataAccessBase::ConvertToSQLString(wstring s)
        {
            wstring_convert<codecvt_utf8<wchar_t, 0x10FFFF, little_endian>, wchar_t> conv;
            auto Bytes = conv.to_bytes(s);
            return Bytes;
        }

        shared_ptr<PreparedStatement> DataAccessBase::CreateTextCommand(wstring CommandText)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(CommandText)));
            return stmt;
        }

        void DataAccessBase::Add(wstring ParameterName, bool Value)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            stmt->setBoolean(1, Value);
            stmt->execute();
        }

        void DataAccessBase::Add(wstring ParameterName, wstring Value)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            stmt->setString(1, ConvertToSQLString(Value));
            stmt->execute();
        }

        void DataAccessBase::Add(wstring ParameterName, int Value)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            stmt->setInt(1, Value);
            stmt->execute();
        }

        void DataAccessBase::Add(wstring ParameterName, double Value)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            stmt->setDouble(1, Value);
            stmt->execute();
        }

        class ByteBuffer : public streambuf
        {
        public:
            ByteBuffer(shared_ptr<vector<uint8_t>> Value)
            {
                auto d = reinterpret_cast<char *>(Value->data());
                auto s = Value->size();
                setg(d, d, d + s);
            }
        };

        void DataAccessBase::Add(wstring ParameterName, shared_ptr<vector<uint8_t>> Value)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            ByteBuffer buffer(Value);
            istream s(&buffer);
            stmt->setBlob(1, &s);
            stmt->execute();
        }

        void DataAccessBase::AddNull(wstring ParameterName)
        {
            auto stmt = shared_ptr<PreparedStatement>(con->prepareStatement(ConvertToSQLString(L"SET @" + ParameterName + L"=?;")));
            stmt->setNull(1, 0);
            stmt->execute();
        }

        bool DataAccessBase::GetBoolean(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { throw logic_error("InvalidOperationException"); }
            return rs->getBoolean(FieldNameS);
        }

        wstring DataAccessBase::GetString(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { throw logic_error("InvalidOperationException"); }
            return ConvertToWString(rs->getString(FieldNameS));
        }

        int DataAccessBase::GetInt(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { throw logic_error("InvalidOperationException"); }
            return rs->getInt(FieldNameS);
        }

        double DataAccessBase::GetReal(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { throw logic_error("InvalidOperationException"); }
            return rs->getDouble(FieldNameS);
        }

        shared_ptr<vector<uint8_t>> DataAccessBase::GetBinary(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { throw logic_error("InvalidOperationException"); }
            auto is = shared_ptr<istream>(rs->getBlob(FieldNameS));
            auto s = make_shared<vector<uint8_t>>();
            char b = 0;
            while (is->get(b))
            {
                s->push_back(b);
            }
            if (!is->eof()) { throw logic_error("InvalidOperationException"); }
            return s;
        }

        Optional<bool> DataAccessBase::GetOptionalOfBoolean(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { return nullptr; }
            return rs->getBoolean(FieldNameS);
        }

        Optional<wstring> DataAccessBase::GetOptionalOfString(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { return nullptr; }
            return ConvertToWString(rs->getString(FieldNameS));
        }

        Optional<int> DataAccessBase::GetOptionalOfInt(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { return nullptr; }
            return rs->getInt(FieldNameS);
        }

        Optional<double> DataAccessBase::GetOptionalOfReal(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { return nullptr; }
            return rs->getDouble(FieldNameS);
        }

        Optional<shared_ptr<vector<uint8_t>>> DataAccessBase::GetOptionalOfBinary(shared_ptr<ResultSet> rs, wstring FieldName)
        {
            auto FieldNameS = ConvertToSQLString(FieldName);
            if (rs->isNull(FieldNameS)) { return nullptr; }
            auto is = shared_ptr<istream>(rs->getBlob(FieldNameS));
            auto s = make_shared<vector<uint8_t>>();
            char b = 0;
            while (is->get(b))
            {
                s->push_back(b);
            }
            if (!is->eof()) { throw logic_error("InvalidOperationException"); }
            return s;
        }
    }
}
