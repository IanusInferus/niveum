@echo off

set JRE_PATH=
for /D %%a in ("%ProgramFiles(x86)%\Java\jre*", "%ProgramFiles%\Java\jre*", "%ProgramFiles(x86)%\Java\jdk*", "%ProgramFiles%\Java\jdk*") do set JRE_PATH=%%a
echo JRE_PATH=%JRE_PATH%

PATH %JRE_PATH%\bin;%PATH%

pushd bin
java -jar DataConv.jar ..\..\SchemaManipulator\Data\WorldData.bin ..\Data\WorldData.bin
popd

pause
