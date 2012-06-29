@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2csdp:CSharp\Src\DatabaseEntities.cs,Database

:: C# Linq to SQL
@if not exist CSharpLinqToSql\Src @md CSharpLinqToSql\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2csd:CSharpLinqToSql\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C++2011
@if not exist CPP\Src @md CPP\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2cppdp:CPP\Src\DatabaseEntities.h,Database

@pause
