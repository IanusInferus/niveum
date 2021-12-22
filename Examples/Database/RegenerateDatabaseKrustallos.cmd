@PATH ..\..\Bin\net48;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"MailCSharp\Bin\Mail.kd" /database:Mail /genkrs:MailData
DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"TestCPP\Bin\Test.kd" /database:Test /genkrs:TestData

@pause
