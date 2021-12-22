@PATH ..\..\Bin\net48;%PATH%

@if not exist ..\..\Bin\net48\MySql.Data.dll (
  @echo Niveum\Bin中不存在MySql.Data.dll。
  @pause
  @exit
)

@echo 请输入密码：
@set /p pass=
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Mail /regenmysql:MailData
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Test /regenmysql:TestData

@pause
