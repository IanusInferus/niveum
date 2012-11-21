@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2csdp:CSharp\Src\DatabaseEntities.cs,Database

:: C# Linq to SQL
@if not exist CSharpLinqToSql\Src @md CSharpLinqToSql\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2csd:CSharpLinqToSql\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C# Linq to Entities
@if not exist CSharpLinqToEntities\Src @md CSharpLinqToEntities\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2cse:CSharpLinqToEntities\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C++2011 MySQL
@if not exist CPPMySQL\Src @md CPPMySQL\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2cppdp:CPPMySQL\Src\DatabaseEntities.h,Database

@pause
