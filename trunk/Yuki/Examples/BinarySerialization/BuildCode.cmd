@PATH ..\..\Bin;%PATH%

:: VB.Net
@if not exist VB\Src @md VB\Src
SchemaManipulator.exe /loadtype:Schema /t2vb:VB\Src\World.vb,World

:: C#
@if not exist CSharp\Src @md CSharp\Src
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\Src\World.cs,World

:: Java
@if not exist Java\src @md Java\src
SchemaManipulator.exe /loadtype:Schema /t2jvb:Java\src\WorldBinary.java,WorldBinary

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\World.h,World
SchemaManipulator.exe /loadtyperef:Schema /t2cppb:CPP\Src\WorldBinary.h,World

:: ActionScript
@if not exist ActionScript\src\worldBinary @md ActionScript\src\worldBinary
SchemaManipulator.exe /loadtype:Schema /t2as:ActionScript\Src\worldBinary,worldBinary

::Haxe
SchemaManipulator.exe /loadtype:Schema /t2hx:Haxe\src\WorldBinary.hx

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Binary Serialization Example","Copyright Public Domain"

@pause
