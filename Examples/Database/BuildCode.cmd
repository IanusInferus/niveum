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

:: Mail

:: C# Linq to Entities
@if not exist MailCSharpLinqToEntities\Src @md MailCSharpLinqToEntities\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2cse:MailCSharpLinqToEntities\Src\DatabaseEntities.cs,Database,Database.Linq,Database.Linq,DbRoot

:: C#
@if not exist MailCSharp\Src @md MailCSharp\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csdp:MailCSharp\Src\Database.cs,Database.Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csm:MailCSharp\Src\Memory\MemoryDataAccess.cs,Database.Database,Database.Memory
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csmssql:MailCSharp\Src\SqlServer\SqlServerDataAccess.cs,Database.Database,Database.SqlServer
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2csmysql:MailCSharp\Src\MySql\MySqlDataAccess.cs,Database.Database,Database.MySql
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2cskrs:MailCSharp\Src\Krustallos\KrustallosDataAccess.cs,Database.Database,Database.Krustallos

:: SQL
@if not exist SQL @md SQL
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2sqlite:SQL\Mail.sqlite.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2tsql:SQL\Mail.tsql.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2pgsql:SQL\Mail.pgsql.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /t2mysql:SQL\Mail.mysql.sql,Database

:: Test

:: C#
@if not exist TestCSharp\Src @md TestCSharp\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csdp:TestCSharp\Src\Database.cs,Database.Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csmssql:TestCSharp\Src\SqlServer\SqlServerDataAccess.cs,Database.Database,Database.SqlServer
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2csmysql:TestCSharp\Src\MySql\MySqlDataAccess.cs,Database.Database,Database.MySql
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2cskrs:TestCSharp\Src\Krustallos\KrustallosDataAccess.cs,Database.Database,Database.Krustallos
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2cscw:TestCSharp\Src\CountedDataAccessWrapper.cs,Database.Database,Database.Database

:: C++17
@if not exist TestCPP\Src @md TestCPP\Src
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2cppdp:TestCPP\Src\Database.h,Database.Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /import:"""Database.h""" /t2cppm:TestCPP\Src\Memory\MemoryDataAccess.h,Database.Database,Database.Memory

:: SQL
@if not exist SQL @md SQL
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2sqlite:SQL\Test.sqlite.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2tsql:SQL\Test.tsql.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2pgsql:SQL\Test.pgsql.sql,Database
RelationSchemaManipulator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /t2mysql:SQL\Test.mysql.sql,Database

:: Xhtml
@if not exist XHTML @md XHTML
RelationSchemaManipulator.exe /loadtype:CommonSchema /loadtype:TestSchema /t2xhtml:XHTML,"Database Example","Copyright Public Domain"
