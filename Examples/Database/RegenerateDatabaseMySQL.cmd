@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\MySql.Data.dll (
  @echo YUKI\Bin�в�����MySql.Data.dll��
  @pause
  @exit
)

@echo ���������룺
@set /p pass=
DatabaseRegenerator.exe /loadtype:Schema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Mail /regenmysql:Data

@pause
