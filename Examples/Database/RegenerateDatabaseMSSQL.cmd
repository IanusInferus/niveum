@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"Data Source=.;Integrated Security=True" /database:Mail /regenmssql:MailData
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"Data Source=.;Integrated Security=True" /database:Test /regenmssql:TestData

@pause
