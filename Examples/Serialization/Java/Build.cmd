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
set JDK_PATH=
for /D %%a in ("%ProgramFiles(x86)%\Java\jdk-11*", "%ProgramFiles%\Java\jdk-11*") do set JDK_PATH=%%a
echo JDK_PATH=%JDK_PATH%

PATH %JDK_PATH%\bin;%PATH%

if exist bin\src rd /S /Q bin\src
md bin\src
xcopy /E src bin\src\

if exist bin\generated rd /S /Q bin\generated
md bin\generated
xcopy /E generated bin\generated\

if exist bin\classes rd /S /Q bin\classes
md bin\classes
javac -sourcepath bin\generated\ -sourcepath bin\src\ -encoding utf-8 -d bin\classes\ bin\generated\niveum\lang\*.java bin\generated\world\*.java bin\generated\world\binary\*.java bin\src\*.java

pushd bin\classes
jar cvfe ..\DataConv.jar Program *
popd
