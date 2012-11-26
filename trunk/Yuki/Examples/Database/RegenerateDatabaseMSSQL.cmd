@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtype:MailSchema /connect:"Data Source=(LocalDB)\v11.0;Integrated Security=True" /database:Mail /regenmssql:MailData
DatabaseRegenerator.exe /loadtype:TestSchema /connect:"Data Source=(LocalDB)\v11.0;Integrated Security=True" /database:Test /regenmssql:TestData

@pause
