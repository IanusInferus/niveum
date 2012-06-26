@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src\Communication\Generated @md CSharp\Src\Communication\Generated
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\Src\Communication\Generated\Communication.cs,Communication
SchemaManipulator.exe /loadtype:Schema /t2csb:CSharp\Src\Communication\Generated\CommunicationBinary.cs,Communication.Binary
SchemaManipulator.exe /loadtype:Schema /t2csj:CSharp\Src\Communication\Generated\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src\Server @md CPP\Src\Server
@if not exist CPP\Src\Client @md CPP\Src\Client
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\Server\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema /t2cppb:CPP\Src\Server\CommunicationBinary.h,Communication.Binary
copy CPP\Src\Server\Communication.h CPP\Src\Client\ /Y
copy CPP\Src\Server\CommunicationBinary.h CPP\Src\Client\ /Y

:: ActionScript
@if not exist ActionScript\src\communication @md ActionScript\src\communication
SchemaManipulator.exe /loadtype:Schema /t2as:ActionScript\Src\communication,communication
SchemaManipulator.exe /loadtype:Schema /t2asb:ActionScript\Src\communication,communication
SchemaManipulator.exe /loadtype:Schema /t2asj:ActionScript\Src\communication,communication

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
