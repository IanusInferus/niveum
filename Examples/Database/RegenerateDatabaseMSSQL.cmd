@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"Data Source=(LocalDB)\v11.0;Integrated Security=True" /database:Mail /regenmssql:MailData
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"Data Source=(LocalDB)\v11.0;Integrated Security=True" /database:Test /regenmssql:TestData

@pause
