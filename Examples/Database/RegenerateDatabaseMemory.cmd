@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"MailCSharp\Bin\Data.md" /database:Mail /regenm:MailData

@pause
