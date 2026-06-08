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
setlocal enabledelayedexpansion

if not exist "..\Libs\temp" md "..\Libs\temp"
pushd "..\Libs\temp"

set "PACKAGES=Microsoft.Data.Sqlite:net48#10.0.8,net8.0#10.0.8 System.Data.SqlClient:net48#4.9.1,net8.0#4.9.1 MySql.Data:net48#6.10.9,net8.0#9.7.0"
set "PACKAGES=%PACKAGES:,=@%"

for %%I in (%PACKAGES%) do (
    for /f "tokens=1 delims=:" %%A in ("%%I") do set "PKG_NAME=%%A"
    for /f "tokens=2* delims=:" %%B in ("%%I") do set "TARGET_PAIRS=%%B"
    set "TARGET_PAIRS=!TARGET_PAIRS:@=,!"
    
    for %%P in (!TARGET_PAIRS!) do (
        for /f "tokens=1,2 delims=#" %%F in ("%%P") do (
            set "TFM=%%F"
            set "PKG_VER=%%G"
            
            set "DEST_DIR=..\!PKG_NAME!\!TFM!"

            echo ===================================================================
            echo Package: !PKG_NAME! ^| Target: !TFM! ^(!PKG_VER!^)
            echo ===================================================================

            if exist ..\temp del /S /Q ..\temp\*

            if "!TFM!"=="net48" (
                dotnet new classlib -f netstandard2.0
            ) else (
                dotnet new classlib -f !TFM!
            )
            dotnet add package !PKG_NAME! --version !PKG_VER!
            dotnet publish -c Release -o "!DEST_DIR!"
            echo !PKG_VER! > "!DEST_DIR!\version.txt"

            if exist "!DEST_DIR!\temp.*" del "!DEST_DIR!\temp.*"
        )
    )
)

popd

copy /Y ..\Libs\Microsoft.Data.Sqlite\net8.0\runtimes\win-x64\native\e_sqlite3.dll ..\Libs\Microsoft.Data.Sqlite\net48\e_sqlite3.dll

rd /S /Q "..\Libs\temp"

echo ===================================================================
echo Matrix processing sequence complete.
echo ===================================================================
