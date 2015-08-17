@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"MailCSharp\Bin\Mail.md" /database:Mail /genm:MailData

@pause
