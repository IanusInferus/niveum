@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src\Server\Generated @md CSharp\Src\Server\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Server\Generated\Common.cs,"",true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2cs:CSharp\Src\Server\Generated\Communication.cs,Communication,true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2csb:CSharp\Src\Server\Generated\CommunicationBinary.cs,Communication.Binary,true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2csj:CSharp\Src\Server\Generated\CommunicationJson.cs,Communication.Json
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:CSharp\Src\Server\Configuration.Schema.tree /t2cs:CSharp\Src\Server\Configuration.cs,Server
@if not exist CSharp\Src\Client\Generated @md CSharp\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2cs:CSharp\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2csb:CSharp\Src\Client\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2csj:CSharp\Src\Client\Generated\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src\Server @md CPP\Src\Server
@if not exist CPP\Src\Client @md CPP\Src\Client
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2cpp:CPP\Src\Server\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2cppb:CPP\Src\Server\CommunicationBinary.h,Communication.Binary
copy CPP\Src\Server\Communication.h CPP\Src\Client\ /Y
copy CPP\Src\Server\CommunicationBinary.h CPP\Src\Client\ /Y

:: ActionScript
@if not exist ActionScript\src\communication @md ActionScript\src\communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2as:ActionScript\src\communication,communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2asb:ActionScript\src\communication,communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2asj:ActionScript\src\communication,communication

::Haxe
SchemaManipulator.exe /loadtype:Schema\Common /t2hx:Haxe\src\Common.hx
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /import:Common /t2hx:Haxe\src\Communication.hx
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /import:Common /import:Communication /t2hxj:Haxe\src\CommunicationJson.hx

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
