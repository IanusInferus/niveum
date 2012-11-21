@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\MySql.Data.dll (
  @echo YUKI\Bin中不存在MySql.Data.dll。
  @pause
  @exit
)

@echo 在Windows上MySQL导入数据表时默认为全小写，可以在my.ini中的[mysqld]中增加lower_case_table_names=2或在MySQL Workbench的服务器管理-Options File-Advanced中将lower_case_table_names设为2来解决这个问题。设置后在查询时还是不会区分大小写，只是在导入导出时会对大小写有区别。参见
@echo http://dev.mysql.com/doc/refman/5.0/en/identifier-case-sensitivity.html

@echo 请输入密码：
@set /p pass=
DatabaseRegenerator.exe /loadtype:Schema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Mail /regenmysql:Data

@pause
