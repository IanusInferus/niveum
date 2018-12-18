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
SchemaManipulator.exe /loadtype:Schema /t2csv:CSharp\Src\WorldVersions.cs,World,World.World

:: Java
@if not exist Java\src @md Java\src
SchemaManipulator.exe /loadtype:Schema /t2jv:Java\generated,World
SchemaManipulator.exe /loadtype:Schema /t2jvb:Java\generated\WorldBinary.java,WorldBinary

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\World.h,World
SchemaManipulator.exe /loadtype:Schema /t2cppb:CPP\Src\WorldBinary.h,World
SchemaManipulator.exe /loadtype:Schema /t2cppv:CPP\Src\WorldVersions.h,World,World.World

::Haxe
SchemaManipulator.exe /loadtype:Schema /t2hx:Haxe\generated,World
SchemaManipulator.exe /loadtype:Schema /t2hxj:Haxe\generated,World.Json

::Python
SchemaManipulator.exe /loadtype:Schema /t2py:Python\src\World.py
SchemaManipulator.exe /loadtype:Schema /import:World /t2pyb:Python\src\WorldBinary.py

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Binary Serialization Example","Copyright Public Domain"

@pause
