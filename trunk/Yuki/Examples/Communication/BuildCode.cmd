@PATH ..\..\Bin;%PATH%

:: C#
@if not exist CSharp\Src @md CSharp\Src
SchemaManipulator.exe /loadtype:Schema /t2cs:CSharp\Src\Communication.cs,Communication
SchemaManipulator.exe /loadtype:Schema /t2csb:CSharp\Src\CommunicationBinary.cs,Communication.Binary
SchemaManipulator.exe /loadtype:Schema /t2csj:CSharp\Src\CommunicationJson.cs,Communication.Json

:: C++2011
@if not exist CPP\Src @md CPP\Src
::SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\Communication.h,Communication

:: ActionScript
@if not exist ActionScript\src\communication @md ActionScript\src\communication
SchemaManipulator.exe /loadtype:Schema /t2asc:ActionScript\Src\communication,communication

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"

@pause
