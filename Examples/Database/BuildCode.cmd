@PATH ..\..\Bin;%PATH%

:: Mail

:: C# Linq to SQL
@if not exist MailCSharpLinqToSql\Src @md MailCSharpLinqToSql\Src
RelationSchemaManipulator.exe /loadtype:MailSchema /t2csd:MailCSharpLinqToSql\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C# Linq to Entities
@if not exist MailCSharpLinqToEntities\Src @md MailCSharpLinqToEntities\Src
RelationSchemaManipulator.exe /loadtype:MailSchema /t2cse:MailCSharpLinqToEntities\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: Test

:: C#
@if not exist TestCSharp\Src @md TestCSharp\Src
RelationSchemaManipulator.exe /loadtype:TestSchema /t2csdp:TestCSharp\Src\Database.cs,Database

:: C++2011 MySQL
@if not exist TestCPPMySQL\Src @md TestCPPMySQL\Src
RelationSchemaManipulator.exe /loadtype:TestSchema /t2cppdp:TestCPPMySQL\Src\DatabaseEntities.h,Database

@pause
