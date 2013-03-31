@PATH ..\..\Bin;%PATH%

:: VB.Net
@if not exist VB\Src @md VB\Src
SchemaManipulator.exe /loadtype:Schema /t2vb:VB\Src\World.vb,World

:: C#
@if not exist CSharp\Src @md CSharp\Src
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\Src\World.cs,World,True
SchemaManipulator.exe /loadtype:Schema /t2csb:CSharp\Src\WorldBinary.cs,World.Binary,True
SchemaManipulator.exe /loadtype:Schema /t2csb:CSharp\Src\WorldBinaryWithoutFirefly.cs,World.BinaryWithoutFirefly,False
SchemaManipulator.exe /loadtype:Schema /t2csj:CSharp\Src\WorldJson.cs,World.Json

:: Java
@if not exist Java\src @md Java\src
SchemaManipulator.exe /loadtype:Schema /t2jvb:Java\src\WorldBinary.java,WorldBinary

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\World.h,World
SchemaManipulator.exe /loadtyperef:Schema /t2cppb:CPP\Src\WorldBinary.h,World

:: ActionScript
@if not exist ActionScript\src\world @md ActionScript\src\world
SchemaManipulator.exe /loadtype:Schema /t2as:ActionScript\Src\world,world
SchemaManipulator.exe /loadtype:Schema /t2asb:ActionScript\Src\world,world
SchemaManipulator.exe /loadtype:Schema /t2asj:ActionScript\Src\world,world

::Haxe
SchemaManipulator.exe /loadtype:Schema /t2hx:Haxe\src\World.hx
SchemaManipulator.exe /loadtype:Schema /import:World /t2hxj:Haxe\src\WorldJson.hx

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Binary Serialization Example","Copyright Public Domain"

@pause
