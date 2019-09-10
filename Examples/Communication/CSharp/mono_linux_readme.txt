CentOS下环境配置指南

1)安装Mono
https://www.mono-project.com/download/stable/#download-lin-centos

2)设置TCP，调节服务器最大socket数量
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
