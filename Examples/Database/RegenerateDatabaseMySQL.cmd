@PATH ..\..\Bin;%PATH%

@if not exist ..\..\Bin\MySql.Data.dll (
  @echo YUKI\Bin�в�����MySql.Data.dll��
  @pause
  @exit
)

@echo ��Windows��MySQL�������ݱ�ʱĬ��ΪȫСд��������my.ini�е�[mysqld]������lower_case_table_names=2����MySQL Workbench�ķ���������-Options File-Advanced�н�lower_case_table_names��Ϊ2�����������⡣���ú��ڲ�ѯʱ���ǲ������ִ�Сд��ֻ���ڵ��뵼��ʱ��Դ�Сд�����𡣲μ�
@echo http://dev.mysql.com/doc/refman/5.0/en/identifier-case-sensitivity.html

@echo ���������룺
@set /p pass=
DatabaseRegenerator.exe /loadtype:Schema /connect:"server=localhost;uid=root;pwd=%pass%;" /database:Mail /regenmysql:Data

@pause
