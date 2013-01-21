openSUSE下环境配置指南

首先安装openSUSE 12.1 64 bit系统。
下载地址http://software.opensuse.org/121/en

然后需要一些软件：

1)vsftpd，用于远程管理文件系统
直接用YaST从光盘安装，安装之后在su进入root权限之后用vi编辑/etc/vsftpd.conf，修改write_enable的值为write_enable=YES。
可以通过/etc/init.d/vsftpd start运行服务。

2)mono-core和mono-data，用于运行.Net程序
直接用YaST从光盘安装。

3)postgresql-server，PostgreSQL数据库服务器
直接用YaST从光盘安装。

安装后进入root权限，执行下面的命令启动PostgreSQL
rcpostgresql start
如果网络不可用，它可能卡住不返回，这时需要先将网络配置好
然后保持root状态，用postgres帐号登录进入PostgreSQL服务器管理
su postgres -c psql postgres
然后修改PostgreSQL密码
ALTER USER postgres WITH PASSWORD 'password';
退出
\q
修改/var/lib/pgsql/data/pg_hba.conf，将
# IPv4 local connections:
host    all             all             127.0.0.1/32            ident
# IPv6 local connections:
host    all             all             ::1/128                 ident
修改为
# IPv4 local connections:
host    all             all             127.0.0.1/32            md5
# IPv6 local connections:
host    all             all             ::1/128                 md5
然后重新启动PostgreSQL
rcpostgresql restart

4)pgadmin，PostgreSQL数据库管理工具
直接用YaST从光盘安装。
安装后执行pgadmin3即可运行。

5)导入数据
在Database目录执行，其中密码为刚才设置的密码
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:MailSchema /connect:"Server=localhost;User ID=postgres;Password={Password};" /database:Mail /regenpgsql:MailData

6)运行程序
运行编译好的程序(附带Npgsql中的库文件Npgsql.dll等)
mono DatabasePostgreSQL.exe "Server=localhost;User ID=postgres;Password={Password};Database=mail;" /load

7)mono-core、mono-devel，用于编译.Net程序，需要使用最新3.0.3版。
http://download.mono-project.com/archive/3.0.3/linux/x64/
编译方法，在Src文件夹执行
xbuild

8)monodevelop、monodevelop-debugger-mdb、libgnomeui，用于调试，最好用3.0以上，以免打开最开始出错。
http://download.opensuse.org/repositories/Mono/openSUSE_12.1/
