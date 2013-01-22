if [ ! -f ../../Bin/MySql.Data.dll ];
then
  echo MySql.Data.dll does not exist in YUKI/Bin.
  exit
fi

echo Please input password:
read pass
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema "/connect:\"server=localhost;uid=root;pwd=${pass};\"" /database:Mail /regenmysql:MailData
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema "/connect:\"server=localhost;uid=root;pwd=${pass};\"" /database:Test /regenmysql:TestData
