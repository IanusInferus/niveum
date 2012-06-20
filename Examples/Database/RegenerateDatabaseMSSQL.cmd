@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtype:Schema /connect:"Data Source=.;Integrated Security=True" /database:Mail /regenmssql:Data

@pause
