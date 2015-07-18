CentOS下环境配置指南

假设是CentOS 6.5 64 bit系统。

1.安装cmake，用于生成Makefile
yum install cmake

2.安装g++ 4.8.2
cd /etc/yum.repos.d
wget http://people.centos.org/tru/devtools-2/devtools-2.repo 
yum --enablerepo=testing-devtools-2-centos-6 install devtoolset-2-binutils devtoolset-2-gcc devtoolset-2-gcc-c++

export CC=/opt/rh/devtoolset-2/root/usr/bin/gcc  
export CPP=/opt/rh/devtoolset-2/root/usr/bin/cpp
export CXX=/opt/rh/devtoolset-2/root/usr/bin/c++

3.编译程序
进入Src\Server文件夹，运行
cmake .
生成调试版Makefile
或者运行
cmake -DCMAKE_BUILD_TYPE=Release .
生成发布版Makefile
然后运行
make
等待编译结束。

4.设置TCP
运行
ulimit -a
查看限制socket连接数的最大支持的文件描述符数量(open files)和最大用户进程数(max user processes)
然后
ulimit -n 65536
ulimit -u 65536
改变成较大的值。
若要永久更改配置，需要
在/etc/pam.d/login中添加
session    required     pam_limits.so
然后在/etc/security/limits.conf中添加
*                soft    nofile          65536
*                hard    nofile          65536
*                soft    nproc           65536
*                hard    nproc           65536
如果有/etc/security/limits.d/90-nproc.conf，也需要修改其中的
*          soft    nproc     1024
为
*          soft    nproc     65536

此外，由于Linux下TCP连接关闭后，还会占用端口一段时间，可能导致端口数量不足等问题。
可以通过下面的方法解决。

编辑/etc/sysctl.conf文件，在
net.ipv4.tcp_syncookies = 1
下增加两行：
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
