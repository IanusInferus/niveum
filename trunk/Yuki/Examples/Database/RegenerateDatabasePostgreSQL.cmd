@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\Npgsql.dll (
  @echo YUKI\Bin�в�����Npgsql.dll��
  @pause
  @exit
)

@echo ���������룺
@set /p pass=
DatabaseRegenerator.exe /loadtype:MailSchema /connect:"Server=localhost;User ID=postgres;Password=%pass%;" /database:Mail /regenpgsql:MailData
DatabaseRegenerator.exe /loadtype:TestSchema /connect:"Server=localhost;User ID=postgres;Password=%pass%;" /database:Test /regenpgsql:TestData

@pause
