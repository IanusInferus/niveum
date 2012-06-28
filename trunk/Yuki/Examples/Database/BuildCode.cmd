@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2csdp:CSharp\Src\DatabaseEntities.cs,Database

:: C++2011
@if not exist CPP\Src @md CPP\Src
RelationSchemaManipulator.exe /loadtype:Schema /t2cppdp:CPP\Src\DatabaseEntities.h,Database

@pause
