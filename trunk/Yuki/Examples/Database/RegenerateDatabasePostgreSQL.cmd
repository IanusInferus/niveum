@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\Npgsql.dll (
  @echo YUKI\Bin中不存在Npgsql.dll。
  @pause
  @exit
)

@echo 请输入密码：
@set /p pass=
DatabaseRegenerator.exe /loadtype:Schema /connect:"Server=localhost;User ID=postgres;Password=%pass%;" /database:Mail /regenpgsql:Data

@pause
