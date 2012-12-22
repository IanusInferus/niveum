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
ulimit -a
查看限制socket连接数的最大支持的文件描述符数量(open files)和最大用户进程数(max user processes)
然后
ulimit -n 65536
ulimit -u 65536
改变成较大的值。
运行./Server。
注意此时若退出root状态ulimit会还原。
若要永久更改配置，需要
在/etc/pam.d/login中添加
session  required       pam_limits.so
然后在/etc/security/limits.conf中添加
*                soft    nofile          65536
*                hard    nofile          65536
*                soft    nproc           65536
*                hard    nproc           65536

这样服务器端程序就成功运行起来了。
客户端程序Src\Client也是通过类似操作即可。

此外，由于Linux下TCP连接关闭后，还会占用端口一段时间，可能导致端口数量不足等问题。
可以通过下面的方法解决。

编辑/etc/sysctl.conf文件，增加三行：
net.ipv4.tcp_syncookies = 1
net.ipv4.tcp_tw_reuse = 1
net.ipv4.tcp_tw_recycle = 1

说明：
net.ipv4.tcp_syncookies = 1 表示开启SYN Cookies。当出现SYN等待队列溢出时，启用cookies来处理，可防范少量SYN攻击，默认为0，表示关闭；
net.ipv4.tcp_tw_reuse = 1 表示开启重用。允许将TIME-WAIT sockets重新用于新的TCP连接，默认为0，表示关闭；
net.ipv4.tcp_tw_recycle = 1 表示开启TCP连接中TIME-WAIT sockets的快速回收，默认为0，表示关闭。
再执行以下命令，让修改结果立即生效：
/sbin/sysctl -p

用以下语句看了一下服务器的TCP状态：
netstat -n | awk '/^tcp/ {++S[$NF]} END {for(a in S) print a, S[a]}'
返回结果如下：
ESTABLISHED 1423
FIN_WAIT1 1
FIN_WAIT2 262
SYN_SENT 1
TIME_WAIT 962
[修改Linux内核参数，减少TCP连接中的TIME-WAIT sockets by 张宴 http://blog.s135.com/post/271/]

还可以用socklist查看当前打开的TCP连接数。
