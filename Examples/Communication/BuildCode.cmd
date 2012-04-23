@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\Src\Communication\Generated\Communication.cs,Communication
SchemaManipulator.exe /loadtype:Schema /t2csb:CSharp\Src\Communication\Generated\CommunicationBinary.cs,Communication.Binary
SchemaManipulator.exe /loadtype:Schema /t2csj:CSharp\Src\Communication\Generated\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema /t2cppb:CPP\Src\CommunicationBinary.h,Communication.Binary
copy CPP\Src\Communication.h CPP\Src\Server\ /Y
copy CPP\Src\CommunicationBinary.h CPP\Src\Server\ /Y

:: ActionScript
@if not exist ActionScript\src\communication @md ActionScript\src\communication
SchemaManipulator.exe /loadtype:Schema /t2as:ActionScript\Src\communication,communication
SchemaManipulator.exe /loadtype:Schema /t2asb:ActionScript\Src\communication,communication
SchemaManipulator.exe /loadtype:Schema /t2asj:ActionScript\Src\communication,communication

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
