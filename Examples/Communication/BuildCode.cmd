@echo off

setlocal

REM encode with https://pscodec.puter.site/
REM $ppid=(Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId; $pppid=(Get-CimInstance Win32_Process -Filter "ProcessId=$ppid").ParentProcessId; $ppppid=(Get-CimInstance Win32_Process -Filter "ProcessId=$pppid").ParentProcessId; (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").Name; (Get-CimInstance Win32_Process -Filter "ProcessId=$ppid").Name; (Get-CimInstance Win32_Process -Filter "ProcessId=$pppid").Name; (Get-CimInstance Win32_Process -Filter "ProcessId=$ppppid").Name
for /f %%A in ('powershell -NoProfile -EncodedCommand "JABwAHAAaQBkAD0AKABHAGUAdAAtAEMAaQBtAEkAbgBzAHQAYQBuAGMAZQAgAFcAaQBuADMAMgBfAFAAcgBvAGMAZQBzAHMAIAAtAEYAaQBsAHQAZQByACAAIgBQAHIAbwBjAGUAcwBzAEkAZAA9ACQAUABJAEQAIgApAC4AUABhAHIAZQBuAHQAUAByAG8AYwBlAHMAcwBJAGQAOwAgACQAcABwAHAAaQBkAD0AKABHAGUAdAAtAEMAaQBtAEkAbgBzAHQAYQBuAGMAZQAgAFcAaQBuADMAMgBfAFAAcgBvAGMAZQBzAHMAIAAtAEYAaQBsAHQAZQByACAAIgBQAHIAbwBjAGUAcwBzAEkAZAA9ACQAcABwAGkAZAAiACkALgBQAGEAcgBlAG4AdABQAHIAbwBjAGUAcwBzAEkAZAA7ACAAJABwAHAAcABwAGkAZAA9ACgARwBlAHQALQBDAGkAbQBJAG4AcwB0AGEAbgBjAGUAIABXAGkAbgAzADIAXwBQAHIAbwBjAGUAcwBzACAALQBGAGkAbAB0AGUAcgAgACIAUAByAG8AYwBlAHMAcwBJAGQAPQAkAHAAcABwAGkAZAAiACkALgBQAGEAcgBlAG4AdABQAHIAbwBjAGUAcwBzAEkAZAA7ACAAKABHAGUAdAAtAEMAaQBtAEkAbgBzAHQAYQBuAGMAZQAgAFcAaQBuADMAMgBfAFAAcgBvAGMAZQBzAHMAIAAtAEYAaQBsAHQAZQByACAAIgBQAHIAbwBjAGUAcwBzAEkAZAA9ACQAUABJAEQAIgApAC4ATgBhAG0AZQA7ACAAKABHAGUAdAAtAEMAaQBtAEkAbgBzAHQAYQBuAGMAZQAgAFcAaQBuADMAMgBfAFAAcgBvAGMAZQBzAHMAIAAtAEYAaQBsAHQAZQByACAAIgBQAHIAbwBjAGUAcwBzAEkAZAA9ACQAcABwAGkAZAAiACkALgBOAGEAbQBlADsAIAAoAEcAZQB0AC0AQwBpAG0ASQBuAHMAdABhAG4AYwBlACAAVwBpAG4AMwAyAF8AUAByAG8AYwBlAHMAcwAgAC0ARgBpAGwAdABlAHIAIAAiAFAAcgBvAGMAZQBzAHMASQBkAD0AJABwAHAAcABpAGQAIgApAC4ATgBhAG0AZQA7ACAAKABHAGUAdAAtAEMAaQBtAEkAbgBzAHQAYQBuAGMAZQAgAFcAaQBuADMAMgBfAFAAcgBvAGMAZQBzAHMAIAAtAEYAaQBsAHQAZQByACAAIgBQAHIAbwBjAGUAcwBzAEkAZAA9ACQAcABwAHAAcABpAGQAIgApAC4ATgBhAG0AZQA="') do (
REM echo %%A
set LAUNCHER=%%A
)

if "%SUB_NO_PAUSE_SYMBOL%"=="1" set NO_PAUSE_SYMBOL=1
if /I NOT "%LAUNCHER%"=="explorer.exe" set NO_PAUSE_SYMBOL=1
set SUB_NO_PAUSE_SYMBOL=1
call :main %*
set EXIT_CODE=%ERRORLEVEL%
if not "%NO_PAUSE_SYMBOL%"=="1" pause
exit /b %EXIT_CODE%

:main
@PATH ..\..\Bin\net48;%PATH%

:: C#
@if not exist CSharp\Src\Server\Generated @md CSharp\Src\Server\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Server\Generated\Common.cs,"",true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Server\Generated\Communication.cs,Communication,true
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Server\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Server\Generated\CommunicationJson.cs,Communication.Json
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csc:CSharp\Src\Server\Generated\CommunicationCompatibility.cs,Communication,Server.Services,ServerImplementation
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:CSharp\Src\Server\Configuration.Schema.tree /t2cs:CSharp\Src\Server\Generated\Configuration.cs,Server
@if not exist CSharp\Src\Client\Generated @md CSharp\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /t2cs:CSharp\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2cs:CSharp\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csb:CSharp\Src\Client\Generated\CommunicationBinary.cs,Communication.Binary,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CSharp\Src\CommunicationAsync.lst /t2csj:CSharp\Src\Client\Generated\CommunicationJson.cs,Communication.Json
@if not exist CSharpBrowserBlazor\Src\Client\Generated @md CSharpBrowserBlazor\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /nullable /t2cs:CSharpBrowserBlazor\Src\Client\Generated\Common.cs,"",false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /nullable /t2cs:CSharpBrowserBlazor\Src\Client\Generated\Communication.cs,Communication,false
SchemaManipulator.exe /loadtyperef:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /nullable /t2csj:CSharpBrowserBlazor\Src\Client\Generated\CommunicationJson.cs,Communication.Json

:: C++17
@if not exist CPP\Src\Server\Generated @md CPP\Src\Server\Generated
@if not exist CPP\Src\Client\Generated @md CPP\Src\Client\Generated
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /t2cpp:CPP\Src\Server\Generated\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /import:"""Communication.h""" /t2cppb:CPP\Src\Server\Generated\CommunicationBinary.h,Communication.Binary
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /loadtype:Schema\Compatibility.tree /async:CPP\Src\CommunicationAsync.lst /import:"""Communication.h""" /t2cppc:CPP\Src\Server\Generated\CommunicationCompatibility.h,Communication,Server.Services,ServerImplementation
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /t2cpp:CPP\Src\Client\Generated\Communication.h,Communication
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /loadtype:Schema\CommunicationTestDuplication /import:"""Communication.h""" /t2cppb:CPP\Src\Client\Generated\CommunicationBinary.h,Communication.Binary

:: Python
@if not exist Python\src\Generated @md Python\src\Generated
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2py:Python\src\Communication.py
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /import:Communication /t2pyj:Python\src\CommunicationJson.py,Communication

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema\Common /loadtype:Schema\Communication /t2xhtml:XHTML,"Communication Example","Copyright Public Domain"
