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
for %%f in ("%ProgramFiles%") do (
  for %%v in (2022) do (
    for %%p in (Enterprise Professional Community BuildTools) do (
      for %%b in (Current) do (
        if exist "%%~f\Microsoft Visual Studio\%%v\%%p\MSBuild\%%b\Bin\MSBuild.exe" (
          set MSBuild="%%~f\Microsoft Visual Studio\%%v\%%p\MSBuild\%%b\Bin\MSBuild.exe"
          goto MSBuild_Found
        )
      )
    )
  )
)
:MSBuild_Found
echo MSBuild=%MSBuild%

%MSBuild% /t:Rebuild /p:Configuration=Release || exit /b 1
