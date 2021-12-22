if [ ! -f ../../Bin/net48/MySql.Data.dll ];
then
  echo MySql.Data.dll does not exist in Niveum/Bin.
  exit
fi

echo Please input password:
read pass
mono ../../Bin/net48/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema "/connect:\"server=localhost;uid=root;pwd=${pass};\"" /database:Mail /regenmysql:MailData
mono ../../Bin/net48/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema "/connect:\"server=localhost;uid=root;pwd=${pass};\"" /database:Test /regenmysql:TestData
