@PATH ..\..\Bin\net48;%PATH%

:: C#
@if not exist CSharp\Src\Server\Generated @md CSharp\Src\Server\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Server\Generated\Common.cs,"",true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Server\Generated\Communication.cs,Communication,true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Server\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Server\Generated\CommunicationJson.cs,Communication.Json
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csc:CSharp\Src\Server\Generated\CommunicationCompatibility.cs,Communication,Server.Services,ServerImplementation
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:CSharp\Src\Server\Configuration.Schema.tree /t2cs:CSharp\Src\Server\Generated\Configuration.cs,Server
@if not exist CSharp\Src\Client\Generated @md CSharp\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Client\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Client\Generated\CommunicationJson.cs,Communication.Json
@if not exist CSharpBrowser\Src\Browser\Generated @md CSharpBrowser\Src\Browser\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharpBrowser\Src\Browser\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2cs:CSharpBrowser\Src\Browser\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /t2csj:CSharpBrowser\Src\Browser\Generated\CommunicationJson.cs,Communication.Json
@if not exist CSharpBrowserBlazor\Src\Client\Generated @md CSharpBrowserBlazor\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /nullable /t2cs:CSharpBrowserBlazor\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /nullable /t2cs:CSharpBrowserBlazor\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /nullable /t2csj:CSharpBrowserBlazor\Src\Client\Generated\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src\Server\Generated @md CPP\Src\Server\Generated
@if not exist CPP\Src\Client\Generated @md CPP\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /t2cpp:CPP\Src\Server\Generated\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /import:"""Communication.h""" /t2cppb:CPP\Src\Server\Generated\CommunicationBinary.h,Communication.Binary
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /import:"""Communication.h""" /t2cppc:CPP\Src\Server\Generated\CommunicationCompatibility.h,Communication,Server.Services,ServerImplementation
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2cpp:CPP\Src\Client\Generated\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /import:"""Communication.h""" /t2cppb:CPP\Src\Client\Generated\CommunicationBinary.h,Communication.Binary

::Haxe
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2hx:Haxe\generated,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2hxj:Haxe\generated,Communication.Json

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
