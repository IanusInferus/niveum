CentOS下环境配置指南

1.安装Mono
https://www.mono-project.com/download/stable/#download-lin-centos

2.设置TCP
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

3.设置UDP
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
