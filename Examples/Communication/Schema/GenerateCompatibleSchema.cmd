@SETLOCAL ENABLEDELAYEDEXPANSION

@setlocal
@if "%SUB_NO_PAUSE_SYMBOL%"=="1" (
    @set NO_PAUSE_SYMBOL=1
) else (
    @set SUB_NO_PAUSE_SYMBOL=1
)

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

SchemaManipulator.exe /loadtype:Common /loadtype:Communication /gencomcmpt:%Versions%,Compatibility.tree

@set EXIT_CODE=%ERRORLEVEL%

@if not "%NO_PAUSE_SYMBOL%"=="1" @pause
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
