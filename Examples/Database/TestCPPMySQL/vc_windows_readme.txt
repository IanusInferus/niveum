Windows下环境配置指南

软件需要

Visual Studio 2015
MySQL 5.6.25

cmake 3.3.1
http://www.cmake.org/files/v3.3/cmake-3.3.1-win32-x86.exe

库需要

boost 1.57.0
https://sourceforge.net/projects/boost/files/boost/1.57.0/boost_1_57_0.7z/download

MySQL Connector C 6.1.6
https://dev.mysql.com/get/Downloads/Connector-C/mysql-connector-c-6.1.6-src.zip
https://dev.mysql.com/get/Downloads/Connector-C/mysql-connector-c-6.1.6-win32.zip

MySQL Connector C++ 1.1.6
https://dev.mysql.com/get/Downloads/Connector-C++/mysql-connector-c++-1.1.6.zip

库的配置过程如下

1)编译boost
解压到boost根目录中，如C:\boost，可看到C:\boost\bootstrap.bat。
在命令行boost根目录中分别运行
bootstrap.bat
bjam --with-system --with-thread --with-date_time --with-regex --with-serialization stage
在环境变量中添加BOOST_ROOT，指向boost的根目录。
即配置好boost库。

2)编译MySQL Connector C
解压mysql-connector-c-6.1.6-src.zip到MySQL Connector C根目录TestCPPMySQL\Lib\mysql-connector-c中，可看到TestCPPMySQL\Lib\mysql-connector-c\README。
将缺少的两个文件从mysql-connector-c-6.1.6-win32.zip中找到解压过去
include\mysqld_ername.h
include\mysqld_error.h

将
include\my_global.h
include\thr_cond.h
include\mysql\psi\mysql_thread.h
三个文件中的timespec全字匹配替换为mytimespec

将
mysys\lf_hash.c
中的
lfind全字匹配替换为mylfind
lsearch全字匹配替换为mylsearch

将
sql-common\my_time.c
中的
inline void set_zero_time : 67
改为
void set_zero_time

执行
cmake . -G "Visual Studio 14 2015"

将
include\my_config.h : 244
的
#define DEFAULT_TMPDIR P_tmpdir
改为
#define DEFAULT_TMPDIR "/tmp"

PATH %SystemRoot%\Microsoft.NET\Framework\v4.0.30319;%PATH%
MSBuild libmysql\libmysql.vcxproj /t:Rebuild /p:Configuration=Debug /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V140
MSBuild libmysql\libmysql.vcxproj /t:Rebuild /p:Configuration=Release /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V140

3)编译MySQL Connector C++
解压mysql-connector-c++-1.1.6.zip到MySQL Connector C++根目录TestCPPMySQL\Lib\mysql-connector-c++中，可看到TestCPPMySQL\Lib\mysql-connector-c++\README。

将
driver\nativeapi\mysql_private_iface.h : 48
中的
#define snprintf _snprintf
改为
//#define snprintf _snprintf

for /D %%a in (../mysql-connector-c) do set MYSQL_CONNECTOR_C_ROOT=%%~dpxna
cmake . -DMYSQL_INCLUDE_DIR="%MYSQL_CONNECTOR_C_ROOT%/include" -DMYSQL_LIB_DIR:STRING="%MYSQL_CONNECTOR_C_ROOT%/libmysql" -DMYSQL_LIB:STRING="libmysql.lib" -DBOOST_ROOT:STRING="%BOOST_ROOT%" -G "Visual Studio 14 2015"

PATH %SystemRoot%\Microsoft.NET\Framework\v4.0.30319;%PATH%
MSBuild driver\mysqlcppconn.vcxproj /t:Rebuild /p:Configuration=Debug /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V140
MSBuild driver\mysqlcppconn.vcxproj /t:Rebuild /p:Configuration=Release /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V140

4)编译例子
运行Src\Build.cmd或者打开Database.sln进行编译。

5)导入数据
若要支持Unicode非基本平面的字符，如“🌸💓”，需要设置MySQL服务器的字符集为utf8mb4，而不能是utf8。
运行RegenerateDatabaseMySQL.cmd导入数据。

6)运行程序
执行
Database "server=localhost;uid=root;pwd={password};database=mail;" /load
和
Database "server=localhost;uid=root;pwd={password};database=mail;" /perf
其中{password}是MySQL密码。
