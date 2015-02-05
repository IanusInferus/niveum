if [ ! -f ../../Bin/Npgsql.dll ];
then
  echo Npgsql.dll does not exist in YUKI/Bin.
  exit
fi

echo Please input password:
read pass
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema "/connect:\"Server=localhost;Port=15432;User ID=postgres;Password=${pass};\"" /database:Mail /regenfdbsql:MailData
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema "/connect:\"Server=localhost;Port=15432;User ID=postgres;Password=${pass};\"" /database:Test /regenfdbsql:TestData
