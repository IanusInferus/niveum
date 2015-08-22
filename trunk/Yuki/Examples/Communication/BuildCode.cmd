@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src\Server\Generated @md CSharp\Src\Server\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Server\Generated\Common.cs,"",true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Server\Generated\Communication.cs,Communication,true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Server\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Server\Generated\CommunicationJson.cs,Communication.Json
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /import:Communication /t2csc:CSharp\Src\Server\Generated\CommunicationCompatibility.cs,ServerImplementation,Server.Services
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:CSharp\Src\Server\Configuration.Schema.tree /t2cs:CSharp\Src\Server\Configuration.cs,Server
@if not exist CSharp\Src\Client\Generated @md CSharp\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Client\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Client\Generated\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src\Server @md CPP\Src\Server
@if not exist CPP\Src\Client @md CPP\Src\Client
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CPP\Src\CommunicationAsync.lst /t2cpp:CPP\Src\Server\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility /async:CPP\Src\CommunicationAsync.lst /import:""Communication.h"" /import:""UtfEncoding.h"" /t2cppb:CPP\Src\Server\CommunicationBinary.h,Communication.Binary
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2cpp:CPP\Src\Client\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /import:""Communication.h"" /import:""UtfEncoding.h"" /t2cppb:CPP\Src\Client\CommunicationBinary.h,Communication.Binary

:: ActionScript
@if not exist ActionScript\src\communication @md ActionScript\src\communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2as:ActionScript\src\communication,communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2asb:ActionScript\src\communication,communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2asj:ActionScript\src\communication,communication

::Haxe
SchemaManipulator.exe /loadtype:Schema\Common /t2hx:Haxe\src\Common.hx
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /import:Common /t2hx:Haxe\src\communication\Communication.hx,communication
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /import:Common /import:communication.Communication /t2hxj:Haxe\src\communication\CommunicationJson.hx,communication

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
