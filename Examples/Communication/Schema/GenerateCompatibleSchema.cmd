@SETLOCAL ENABLEDELAYEDEXPANSION

@setlocal
@if "%SUB_NO_PAUSE_SYMBOL%"=="1" (
    @set NO_PAUSE_SYMBOL=1
) else (
    @set SUB_NO_PAUSE_SYMBOL=1
)

@PATH %CD%\..\..\..\Bin;%PATH%

@for %%b in ("") do @(
    @set TypeName=%%~b

    @for %%a in (Compatibility!TypeName!\*) do @(
      @set DirName=%%~na
      @if not exist "Previous\!DirName!\Communication!TypeName!" (
        del "%%a"
      )
    )

    @set SchemaDirs=
    @for /D %%a in (Previous\*) do @(
      @if exist "%%a\Communication!TypeName!" (
        @set SchemaDirs=!SchemaDirs! %%~nxa
      )
    )
    @call :qSort !SchemaDirs!
    @set SchemaDirs=!return!
    @echo Generating compatible schema for '!TypeName!' at : !SchemaDirs!

    SchemaManipulator.exe /loadtype:Common /loadtype:Communication!TypeName! /save:Communication!TypeName!.tree

    @for %%a in (!SchemaDirs!) do @(
      @set DirPath=Previous\%%a
      SchemaManipulator.exe /loadtype:"!DirPath!\Common" /loadtype:"!DirPath!\Communication!TypeName!" /save:"!DirPath!\Communication!TypeName!.tree"
    )

    @set OldDir=
    @set NewDir=
    @for %%a in (!SchemaDirs! "") do @(
      @set NewDir=%%~nxa
      @if not "!OldDir!"=="" (
        @set OldPath=Previous\!OldDir!
        @if "!NewDir!"=="" (
          @set NewDir=Head
          @set NewPath=.
        ) else (
          @set NewPath=Previous\!NewDir!
        )
        SchemaManipulator.exe /load:Communication!TypeName!.tree /gencom:"!OldPath!\Communication!TypeName!.tree","!OldDir!","!NewPath!\Communication!TypeName!.tree","!NewDir!","Compatibility!TypeName!\!OldDir!.tree"
      )
      @set OldDir=!NewDir!
    )

    @for %%a in (!SchemaDirs!) do @(
      @set DirPath=Previous\%%a
      del "!DirPath!\Communication!TypeName!.tree"
    )

    del Communication!TypeName!.tree
)

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
