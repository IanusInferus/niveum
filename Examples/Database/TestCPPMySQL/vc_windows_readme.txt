Windows下环境配置指南

软件需要

Visual Studio 2013

MySQL 5.5.24.1
http://cdn.mysql.com/Downloads/MySQLInstaller/mysql-installer-5.5.24.1.msi

cmake 2.8.8
http://www.cmake.org/files/v2.8/cmake-2.8.8-win32-x86.exe

库需要

boost 1.49.0
https://sourceforge.net/projects/boost/files/boost/1.49.0/boost_1_49_0.7z/download

MySQL Connector C 6.0.2
http://cdn.mysql.com/Downloads/Connector-C/mysql-connector-c-6.0.2.tar.gz

MySQL Connector C++ 1.1.0
http://cdn.mysql.com/Downloads/Connector-C++/mysql-connector-c++-1.1.0.tar.gz

库的配置过程如下

1)编译boost
解压到boost根目录中，如C:\boost，可看到C:\boost\bootstrap.bat。
在命令行boost根目录中分别运行
bootstrap.bat
bjam --with-system --with-thread --with-date_time --with-regex --with-serialization stage
在环境变量中添加BOOST_ROOT，指向boost的根目录。
即配置好boost库。

2)编译MySQL Connector C
解压到MySQL Connector C根目录中，如C:\mysql-connector-c，可看到C:\mysql-connector-c\README。
在cmake中将source路径和build路径均选为C:/mysql-connector-c，然后点Configure。

如果出现
"README" could not be found.
"LICENSE.mysql" could not be found.
等错误，可以尝试将MySQL Connector C根目录中的CMakeLists.txt中353行处开始的

IF(EXISTS "COPYING")
  SET(CPACK_RESOURCE_FILE_LICENSE     "COPYING")
ELSE(EXISTS "COPYING")
  SET(CPACK_RESOURCE_FILE_LICENSE     "LICENSE.mysql")
ENDIF(EXISTS "COPYING")
SET(CPACK_PACKAGE_DESCRIPTION_FILE    "README")

替换为

IF(EXISTS "${CMAKE_SOURCE_DIR}/COPYING")
  SET(CPACK_RESOURCE_FILE_LICENSE     "${CMAKE_SOURCE_DIR}/COPYING")
ELSE(EXISTS "${CMAKE_SOURCE_DIR}/COPYING")
  SET(CPACK_RESOURCE_FILE_LICENSE     "${CMAKE_SOURCE_DIR}/LICENSE.mysql")
ENDIF(EXISTS "${CMAKE_SOURCE_DIR}/COPYING")
SET(CPACK_PACKAGE_DESCRIPTION_FILE    "${CMAKE_SOURCE_DIR}/README")

再进行Configure。
成功后Generate。

之后在MySQL Connector C根目录中新建一个Build.cmd批处理文件，填入下述内容并运行进行编译。

PATH %SystemRoot%\Microsoft.NET\Framework\v4.0.30319;%PATH%

MSBuild libmysql.sln /t:Rebuild /p:Configuration=Debug /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V120
MSBuild libmysql.sln /t:Rebuild /p:Configuration=Release /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V120
pause

在环境变量中添加MYSQL_CONNECTOR_C_ROOT，指向MySQL Connector C的根目录。

3)编译MySQL Connector C++

解压到MySQL Connector C++根目录中，如C:\mysql-connector-c++，可看到C:\mysql-connector-c++\README。
在cmake中将source路径和build路径均选为C:/mysql-connector-c++，然后点Configure。

之后将MYSQL_INCLUDE_DIR设为MySQL Connector C根目录/include，如C:/mysql-connector-c/include。
将MYSQL_LIB设为MySQL Connector C根目录/libmysql/$(ConfigurationName)/libmysql.lib，如C:/mysql-connector-c/libmysql/$(ConfigurationName)/libmysql.lib。

如果在
test/CJUnitTestsPort/CMakeLists.txt:28
test/unit/CMakeLists.txt:28
出现错误，可以尝试将这两个文件中的

LINK_DIRECTORIES(../framework/$(ConfigurationName))

替换为

LINK_DIRECTORIES(${CMAKE_SOURCE_DIR}/test/framework/$(ConfigurationName))

再进行Configure。
成功后Generate。

之后在MySQL Connector C++根目录中新建一个Build.cmd批处理文件，填入下述内容并运行进行编译。

PATH %SystemRoot%\Microsoft.NET\Framework\v4.0.30319;%PATH%

MSBuild MYSQLCPPCONN.sln /t:Rebuild /p:Configuration=Debug /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V120
MSBuild MYSQLCPPCONN.sln /t:Rebuild /p:Configuration=Release /p:VCTargetsPath="%ProgramFiles(x86)%\MSBuild\Microsoft.Cpp\v4.0\V120
pause

在环境变量中添加MYSQL_CONNECTOR_CXX_ROOT，指向MySQL Connector C++的根目录。

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
