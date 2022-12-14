@echo off

setlocal
if "%SUB_NO_PAUSE_SYMBOL%"=="1" set NO_PAUSE_SYMBOL=1
if /I "%COMSPEC%" == %CMDCMDLINE% set NO_PAUSE_SYMBOL=1
set SUB_NO_PAUSE_SYMBOL=1
call :main %*
set EXIT_CODE=%ERRORLEVEL%
if not "%NO_PAUSE_SYMBOL%"=="1" pause
exit /b %EXIT_CODE%

:main
dotnet build --configuration Release || exit /b 1

copy Doc\Readme.*.txt ..\Bin\ || exit /b 1
copy Doc\UpdateLog.*.txt ..\Bin\ || exit /b 1
copy Doc\License.*.txt ..\Bin\ || exit /b 1
