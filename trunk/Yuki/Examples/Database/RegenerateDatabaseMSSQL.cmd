@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtype:Schema /connect:"Data Source=(LocalDB)\v11.0;Integrated Security=True" /database:Mail /regenmssql:Data

@pause
