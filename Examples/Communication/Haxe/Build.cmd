haxe build.hxml
xcopy /E /Y jslib\*.* bin\
xcopy /S /Y src\*.html bin\
xcopy /S /Y src\*.json bin\
@pause
