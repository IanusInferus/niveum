@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\Npgsql.dll (
  @echo YUKI\Bin�в�����Npgsql.dll��
  @pause
  @exit
)

@echo ���������룺
@set /p pass=
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"Server=localhost;Port=15432;User ID=postgres;Password=%pass%;" /database:Mail /regenfdbsql:MailData
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"Server=localhost;Port=15432;User ID=postgres;Password=%pass%;" /database:Test /regenfdbsql:TestData

@pause
