openSUSE下环境配置指南

首先安装openSUSE 12.1 64 bit系统。
下载地址http://software.opensuse.org/121/en

然后需要一些软件：

1)vsftpd，用于远程管理文件系统
直接用YaST从光盘安装，安装之后在su进入root权限之后用vi编辑/etc/vsftpd.conf，修改write_enable的值为write_enable=YES。
可以通过/etc/init.d/vsftpd start运行服务。

2)cmake，用于生成Makefile
直接用YaST从光盘安装。

3)gcc46-c++，用于编译
默认已安装

4)boost 1.49.0，示例程序需要用到的库
http://sourceforge.net/projects/boost/files/boost/1.49.0/boost_1_49_0.7z/download
然后直接用YaST从光盘安装p7zip，解压缩该7-zip压缩文件到Lib/boost目录，可以看到文件Lib/boost/bootstrap.bat。
在Lib/boost目录中执行
find . -type f -name '*.sh' -exec chmod +x {} \;
使得所有的.sh文件可以执行，然后运行
./bootstrap.sh
./bjam --with-system --with-thread --with-date_time --with-regex --with-serialization --toolset=gcc-4.6 stage
即配置好boost库。

然后编译程序：

进入Src\Server文件夹，运行
cmake .
生成调试版Makefile
或者运行
cmake -DCMAKE_BUILD_TYPE=Release .
生成发布版Makefile
然后运行
make
等待编译结束。

编译完成后，用su提升到root权限，运行
ulimit -n
ulimit -u
查看最大支持的文件描述符数量(socket连接数受此限制)
然后
ulimit -n 65536
ulimit -u 65536
改变成一个较大的值。
运行./Server。
注意此时若退出root状态ulimit会还原。

这样服务器端程序就成功运行起来了。
客户端程序Src\Client也是通过类似操作即可。
