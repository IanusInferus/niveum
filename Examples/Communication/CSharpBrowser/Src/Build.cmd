PATH %ProgramFiles(x86)%\MSBuild\14.0\Bin;%PATH%

MSBuild /t:Rebuild /p:Configuration=Release
pause
