@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\MySql.Data.dll (
  @echo YUKI\Bin中不存在MySql.Data.dll。
  @pause
  @exit
)

@echo 请输入密码：
@set /p pass=
DatabaseRegenerator.exe /loadtype:Schema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Mail /regenmysql:Data

@pause
