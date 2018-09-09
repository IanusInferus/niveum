@if not exist ..\Bin @md ..\Bin
xcopy /E /Y jslib\*.js ..\Bin\
xcopy /S /Y Static\*.* ..\Bin\
