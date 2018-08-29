@echo off

set JDK_PATH=
for /D %%a in ("%ProgramFiles(x86)%\Java\jdk*", "%ProgramFiles%\Java\jdk*") do set JDK_PATH=%%a
echo JDK_PATH=%JDK_PATH%

PATH %~dp0\..\..\..\Src\Lib\Firefly;%JDK_PATH%\bin;%PATH%

if exist bin\src rd /S /Q bin\src
md bin\src
xcopy /E src bin\src\

pushd bin\src\
TransEncoding.exe ".*?\.java" UTF-8 /nobom
popd

if exist bin\classes rd /S /Q bin\classes
md bin\classes
javac -sourcepath bin\src\ -encoding utf-8 -d bin\classes\ bin\src\*.java

pushd bin\classes
jar cvfe ..\DataConv.jar Program *
popd

pause
