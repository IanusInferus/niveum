openSUSE下环境配置指南

软件需要

mysql-community-server、mysql-workbench
直接用YaST从光盘安装。

cmake
直接用YaST从光盘安装。

gcc-c++、gcc46-c++、libpth-devel
直接用YaST从光盘安装。

p7zip
直接用YaST从光盘安装。

mono-core、mono-data
直接用YaST从光盘安装。

库需要

boost 1.49.0
https://sourceforge.net/projects/boost/files/boost/1.49.0/boost_1_49_0.7z/download

libmysqlclient-devel
直接用YaST从光盘安装。

libmysqlcppconn-devel
直接用YaST从光盘安装。

库的配置过程如下

1)编译boost
解压缩7-zip压缩文件到Lib/boost目录，可以看到文件Lib/boost/bootstrap.sh。
在Lib/boost目录中执行
find . -type f -name '*.sh' -exec chmod +x {} \;
使得所有的.sh文件可以执行，然后运行
./bootstrap.sh
./bjam --with-system --with-thread --with-date_time --with-regex --with-serialization --toolset=gcc-4.6 stage
即配置好boost库。

2)编译程序

进入Src文件夹，运行
cmake .
生成调试版Makefile
或者运行
cmake -DCMAKE_BUILD_TYPE=Release .
生成发布版Makefile
然后运行
make
等待编译结束。

3)导入数据
若要支持Unicode非基本平面的字符，如“🌸💓”，需要设置MySQL服务器的字符集为utf8mb4，而不能是utf8。
运行
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"server=localhost;uid=root;pwd={password};" /database:Test /regenmysql:TestData
其中{password}是MySQL密码。
如果已经在mysql-workbench中使用密码连接过并保存了密码，可不输入密码，即
mono ../../Bin/DatabaseRegenerator.exe /loadtyperef:CommonSchema /loadtype:TestSchema /connect:"server=localhost;uid=root;" /database:Test /regenmysql:TestData

4)运行程序
执行
./Database "server=localhost;uid=root;database=mail;" /load
和
./Database "server=localhost;uid=root;database=mail;" /perf
