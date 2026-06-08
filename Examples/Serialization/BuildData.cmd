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

:: SchemaManipulator
@if not exist SchemaManipulator\Data @md SchemaManipulator\Data
SchemaManipulator.exe /loadtype:Schema /t2b:Data\WorldData.tree,SchemaManipulator\Data\WorldData.bin,World.World /b2t:SchemaManipulator\Data\WorldData.bin,SchemaManipulator\Data\WorldData.Binary.tree,World.World /t2b:SchemaManipulator\Data\WorldData.Binary.tree,SchemaManipulator\Data\WorldData.Binary.bin,World.World

:: VB.Net
@if not exist VB\Data @md VB\Data
if exist VB\Bin\DataConv.exe (
  VB\Bin\DataConv.exe /t2b:Data\WorldData.tree,VB\Data\WorldData.bin /b2t:VB\Data\WorldData.bin,VB\Data\WorldData.Binary.tree /t2b:VB\Data\WorldData.Binary.tree,VB\Data\WorldData.Binary.bin
)

:: C#
@if not exist CSharp\Data @md CSharp\Data
if exist CSharp\Bin\DataConv.exe (
  CSharp\Bin\DataConv.exe /t2b:Data\WorldData.tree,CSharp\Data\WorldData.bin /b2t:CSharp\Data\WorldData.bin,CSharp\Data\WorldData.Binary.tree /t2b:CSharp\Data\WorldData.Binary.tree,CSharp\Data\WorldData.Binary.bin
  CSharp\Bin\DataConv.exe /t2j:Data\WorldData.tree,CSharp\Data\WorldData.json /j2t:CSharp\Data\WorldData.json,CSharp\Data\WorldData.Json.tree /t2j:CSharp\Data\WorldData.Json.tree,CSharp\Data\WorldData.Json.json
)

:: Java
@if not exist Java\Data @md Java\Data
if exist Java\bin\Program.class (
  @pushd Java\bin\
  java Program ..\..\SchemaManipulator\Data\WorldData.bin ..\Data\WorldData.bin
  java Program ..\Data\WorldData.bin ..\Data\WorldData2.bin
  @popd
)

:: C++17
@if not exist CPP\Data @md CPP\Data
if exist CPP\Bin\DataCopy.exe (
  CPP\Bin\DataCopy.exe SchemaManipulator\Data\WorldData.bin CPP\Data\WorldData.bin
  CPP\Bin\DataCopy.exe CPP\Data\WorldData.bin CPP\Data\WorldData2.bin
)

:: Rust
@if not exist Rust\Data @md Rust\Data
if exist Rust\target\release\data-copy.exe (
  set RUST_BACKTRACE=1
  Rust\target\release\data-copy.exe SchemaManipulator\Data\WorldData.bin Rust\Data\WorldData.bin
  Rust\target\release\data-copy.exe Rust\Data\WorldData.bin Rust\Data\WorldData2.bin
)

:: Haxe
@if not exist Haxe\Data @md Haxe\Data
if exist Haxe\bin\DataCopy.n (
  @pushd Haxe
  call Run.cmd
  @popd
)

:: Python
@if not exist Python\Data @md Python\Data
if exist Python\src\Run.cmd (
  @pushd Python
  call Run.cmd
  @popd
)
