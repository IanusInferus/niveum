@PATH ..\..\Bin;%PATH%

:: Mail

:: C# Linq to Entities
@if not exist MailCSharpLinqToEntities\Src @md MailCSharpLinqToEntities\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2cse:MailCSharpLinqToEntities\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C#
@if not exist MailCSharp\Src @md MailCSharp\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csdp:MailCSharp\Src\Database.cs,Database.Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csm:MailCSharp\Src\Memory\MemoryDataAccess.cs,Database.Database,Database.Memory
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csmssql:MailCSharp\Src\SqlServer\SqlServerDataAccess.cs,Database.Database,Database.SqlServer
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csmysql:MailCSharp\Src\MySql\MySqlDataAccess.cs,Database.Database,Database.MySql
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2cskrs:MailCSharp\Src\Krustallos\KrustallosDataAccess.cs,Database.Database,Database.Krustallos

:: Test

:: C#
@if not exist TestCSharp\Src @md TestCSharp\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csdp:TestCSharp\Src\Database.cs,Database.Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csmssql:TestCSharp\Src\SqlServer\SqlServerDataAccess.cs,Database.Database,Database.SqlServer
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csmysql:TestCSharp\Src\MySql\MySqlDataAccess.cs,Database.Database,Database.MySql
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2cskrs:TestCSharp\Src\Krustallos\KrustallosDataAccess.cs,Database.Database,Database.Krustallos
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2cscw:TestCSharp\Src\CountedDataAccessWrapper.cs,Database.Database,Database.Database

:: C++2011 MySQL
@if not exist TestCPPMySQL\Src @md TestCPPMySQL\Src
RelationSchemaManipulator.exe /loadtype:CommonSchema /loadtype:TestSchema /import:""Workaround.h"" /t2cppdp:TestCPPMySQL\Src\Database.h,Database

@pause
