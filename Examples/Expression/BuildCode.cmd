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
@if not exist CSharp\Src @md CSharp\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,CSharp\Bin\net48\Assembly.bin /t2csbl:CSharp\Src\Calculation.cs,ExprCalc

:: C++2011
@if not exist CPP\Src @md CPP\Src
SchemaManipulator.exe /loadtype:..\..\Src\NiveumObject\Common /loadtype:..\..\Src\NiveumExpression\ExpressionSchema /t2cpp:CPP\Src\ExpressionSchema.h,Niveum.ExpressionSchema
SchemaManipulator.exe /loadtyperef:..\..\Src\NiveumObject\Common /loadtype:..\..\Src\NiveumExpression\ExpressionSchema /t2cppb:CPP\Src\ExpressionSchemaBinary.h,Niveum.ExpressionSchema
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,Cpp\Bin\Assembly.bin /import:""ExpressionCalculator.h"" /t2cppbl:Cpp\Src\Calculation.h,ExprTest

:: C
@if not exist C\Src @md C\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,C\Src\Calculation.bin /t2c:C\Src\Calculation.bin,C\Src\Calculation.h,C\Src\Calculation.c,ExprTest

:: RV64
@if not exist RV64\Src @md RV64\Src
ExpressionManipulator.exe /loadtype:Schema /t2b:Assembly,RV64\Src\Calculation.bin /t2rv64:RV64\Src\Calculation.bin,RV64\Src\Calculation.h,RV64\Src\Calculation.s,ExprTest
