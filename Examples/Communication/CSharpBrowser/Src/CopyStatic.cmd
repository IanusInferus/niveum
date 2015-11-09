@if not exist ..\Bin @md ..\Bin
xcopy /E /Y jslib\*.* ..\Bin\
xcopy /S /Y Static\*.* ..\Bin\
