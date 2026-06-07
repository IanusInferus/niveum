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
SchemaManipulator.exe /loadtype:Schema /t2jvb:Java\generated,World.Binary

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:Schema /t2cpp:CPP\Src\World.h,World
SchemaManipulator.exe /loadtype:Schema /t2cppb:CPP\Src\WorldBinary.h,World
SchemaManipulator.exe /loadtype:Schema /t2cppv:CPP\Src\WorldVersions.h,World,World.World

:: Haxe
SchemaManipulator.exe /loadtype:Schema /t2hx:Haxe\generated,World
SchemaManipulator.exe /loadtype:Schema /t2hxj:Haxe\generated,World.Json

:: Python
SchemaManipulator.exe /loadtype:Schema /t2py:Python\src\World.py
SchemaManipulator.exe /loadtype:Schema /import:World /t2pyb:Python\src\WorldBinary.py,World
SchemaManipulator.exe /loadtype:Schema /import:World /t2pyj:Python\src\WorldJson.py,World

:: Xhtml
@if not exist XHTML @md XHTML
SchemaManipulator.exe /loadtype:Schema /t2xhtml:XHTML,"Binary Serialization Example","Copyright Public Domain"
