PATH ..\..\Bin;%PATH%

:: VB.Net
@if not exist VB @md VB
SchemaManipulator.exe /loadtype:Schema /t2vb:VB\World.vb,World

:: C#
@if not exist CSharp @md CSharp
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\World.cs,World

:: Java
@if not exist Java @md Java
SchemaManipulator.exe /loadtype:Schema /t2jv:Java\World.java,World
SchemaManipulator.exe /loadtyperef:Schema /t2jvb:Java\WorldBinary.java,World

:: C++2011
@if not exist CPP @md CPP
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\World.h,World
SchemaManipulator.exe /loadtyperef:Schema /t2cppb:CPP\WorldBinary.h,World

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Binary Serialization Example","Copyright Public Domain"

@pause
