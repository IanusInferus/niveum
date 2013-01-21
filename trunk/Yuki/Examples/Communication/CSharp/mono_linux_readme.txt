openSUSE下环境配置指南

首先安装openSUSE 12.1 64 bit系统。
下载地址http://software.opensuse.org/121/en

然后需要一些软件：

1)vsftpd，用于远程管理文件系统
直接用YaST从光盘安装，安装之后在su进入root权限之后用vi编辑/etc/vsftpd.conf，修改write_enable的值为write_enable=YES。
可以通过/etc/init.d/vsftpd start运行服务。

2)mono-core，用于运行.Net程序
直接用YaST从光盘安装。

在Windows上编译完成后，用su提升到root权限，运行
ulimit -a
查看限制socket连接数的最大支持的文件描述符数量(open files)和最大用户进程数(max user processes)
然后
ulimit -n 65536
ulimit -u 65536
改变成较大的值。
运行mono Server.exe
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
客户端程序Client.exe也是通过类似操作即可。

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

3)mono-core、mono-devel，用于编译.Net程序，需要使用最新3.0.3版。
http://download.mono-project.com/archive/3.0.3/linux/x64/
编译方法，在Src文件夹执行
xbuild

4)monodevelop、monodevelop-debugger-mdb、libgnomeui，用于调试，最好用3.0以上，以免打开最开始出错。
http://download.opensuse.org/repositories/Mono/openSUSE_12.1/
