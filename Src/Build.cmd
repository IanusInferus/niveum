PATH %ProgramFiles(x86)%\MSBuild\14.0\Bin;%SystemRoot%\Microsoft.NET\Framework\v4.0.30319;%PATH%

MSBuild /t:Rebuild /p:Configuration=Release

copy Doc\Readme.*.txt ..\Bin\
copy Doc\UpdateLog.*.txt ..\Bin\
copy Doc\License.*.txt ..\Bin\

@pause
