@echo off

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

pause
