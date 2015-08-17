@PATH ..\..\Bin;%PATH%

DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"MailCSharp\Bin\Mail.kd" /database:Mail /genms:MailData

@pause
