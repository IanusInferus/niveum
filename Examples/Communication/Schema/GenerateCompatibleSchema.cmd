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

@SETLOCAL ENABLEDELAYEDEXPANSION

@PATH %CD%\..\..\..\Bin\net48;%PATH%

@set SchemaDirs=
@for /D %%a in (Previous\*) do @(
    @set SchemaDirs=!SchemaDirs! %%~nxa
)
@call :qSort !SchemaDirs!
@set SchemaDirs=!return!
@echo Generating compatible schema for 'Communication' at : !SchemaDirs!

@set Versions=
@for %%a in (!SchemaDirs!) do @(
    @if "!Versions!"=="" (
        @set Versions=Previous\%%~nxa,%%~nxa
    ) else (
        @set Versions=!Versions!,Previous\%%~nxa,%%~nxa
    )
)

SchemaManipulator.exe /loadtype:Common /loadtype:Communication /loadtype:CommunicationTestDuplication /gencomcmpt:%Versions%,Compatibility.tree

@set EXIT_CODE=%ERRORLEVEL%
@exit /b %EXIT_CODE%


:qSort
@SETLOCAL
    @set list=%*
    @set size=0
    @set less=
    @set greater=
    @for %%i in (%*) do @set /a size=size+1
    @if %size% LEQ 1 @ENDLOCAL & @set return=%list% & @goto :eof
    @for /f "tokens=2* delims== " %%i in ('set list') do @set p=%%i & @set body=%%j
    @for %%x in (%body%) do @(@if %%x LEQ %p% (@set less=%%x !less!) else (@set greater=%%x !greater!))
    @call :qSort %less%
    @set sorted=%return%
    @call :qSort %greater%
    @set sorted=%sorted% %p% %return%
@ENDLOCAL & @set return=%sorted%
@goto :eof
