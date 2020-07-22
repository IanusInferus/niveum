CentOS下环境配置指南

假设是CentOS系统。

1.安装cmake，用于生成Makefile，安装g++
yum install cmake
yum install gcc
yum install gcc-c++

2.编译程序
进入CMakeLists.txt所在文件夹，运行
cmake -DCMAKE_BUILD_TYPE=Debug .
生成调试版Makefile
或者运行
cmake -DCMAKE_BUILD_TYPE=Release .
生成发布版Makefile
然后运行
make
等待编译结束。

3.设置TCP
用su提升到root权限，运行
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

4.设置UDP
Linux上需要设置read buffer size和write buffer size
https://medium.com/@CameronSparr/increase-os-udp-buffers-to-improve-performance-51d167bb1360

显示最大和默认buffer size
sysctl net.core.rmem_max
sysctl net.core.rmem_default
sysctl net.core.wmem_max
sysctl net.core.wmem_default

修改当前设置
sysctl -w net.core.rmem_max=16777216
sysctl -w net.core.rmem_default=16777216
sysctl -w net.core.wmem_max=8388608
sysctl -w net.core.wmem_default=8388608

修改/etc/sysctl.conf或/etc/sysctl.d，重启后生效
net.core.rmem_max=16777216
net.core.rmem_default=16777216
net.core.wmem_max=8388608
net.core.wmem_default=8388608

5.异常信息
如果出现异常信息需要知道对应的源代码行号，可以将异常信息存到文件中（如error.log），然后执行
cat error.log | sed -n 's/^.*\[0x\([0-9A-Fa-f]*\)\].*$/\1/p' | xargs addr2line -e ExceptionStackTrace
