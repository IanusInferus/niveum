@SETLOCAL ENABLEDELAYEDEXPANSION

@setlocal
@if "%SUB_NO_PAUSE_SYMBOL%"=="1" (
    @set NO_PAUSE_SYMBOL=1
) else (
    @set SUB_NO_PAUSE_SYMBOL=1
)

@PATH %CD%\..\..\..\Bin;%PATH%

@set SchemaDirs=
@for /D %%a in (..\PreviousSchemas\*) do @set SchemaDirs=!SchemaDirs! %%~nxa
@call :qSort %SchemaDirs%
@set SchemaDirs=%return%

SchemaManipulator.exe /loadtype:Common /loadtype:Communication /save:Communication.tree

@for /D %%a in (..\PreviousSchemas\*) do (
  SchemaManipulator.exe /loadtype:"%%a\Common" /loadtype:"%%a\Communication" /save:"%%a\Communication.tree"
)

@set OldDir=
@set NewDir=
@for %%a in (%SchemaDirs% "") do (
  @set NewDir=%%~nxa
  @if not "!OldDir!"=="" (
    @set OldPath=..\PreviousSchemas\!OldDir!
    @if "!NewDir!"=="" (
      @set NewPath=.
    ) else (
      @set NewPath=..\PreviousSchemas\!NewDir!
    )
    SchemaManipulator.exe /load:"!NewPath!\Communication.tree" /gencom:"!OldPath!\Communication.tree","Compatibility\!OldDir!.tree","!OldDir!"
  )
  @set OldDir=!NewDir!
)

@for /D %%a in (..\PreviousSchemas\*) do (
  del "%%a\Communication.tree"
)

del Communication.tree

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
