CentOS下环境配置指南

1)安装Mono
（以下的二进制安装因为Mono官方一直没有发布新的Linux二进制版本，无法使用。）
如果需要从二进制安装，从
http://download.mono-project.com/archive/3.0.4/linux/x64/
下载mono-core、mono-data、mono-devel、mono-extras。
wget http://download.mono-project.com/archive/3.0.4/linux/x64/mono-core-3.0.4-0.x86_64.rpm
wget http://download.mono-project.com/archive/3.0.4/linux/x64/mono-data-3.0.4-0.x86_64.rpm
wget http://download.mono-project.com/archive/3.0.4/linux/x64/mono-devel-3.0.4-0.x86_64.rpm
wget http://download.mono-project.com/archive/3.0.4/linux/x64/mono-extras-3.0.4-0.x86_64.rpm

安装
rpm -ivh --nodeps mono-core-3.0.4-0.x86_64.rpm
rpm -ivh --nodeps mono-data-3.0.4-0.x86_64.rpm
rpm -ivh --nodeps mono-devel-3.0.4-0.x86_64.rpm
rpm -ivh --nodeps mono-extras-3.0.4-0.x86_64.rpm

如果需要卸载，执行
rpm -e --nodeps mono-core
rpm -e --nodeps mono-data
rpm -e --nodeps mono-devel
rpm -e --nodeps mono-extras

如果需要从源代码安装，执行
yum install bison gettext glib2 freetype fontconfig libpng libpng-devel libX11 libX11-devel glib2-devel libgdi* libexif glibc-devel urw-fonts java unzip gcc gcc-c++ automake autoconf libtool make bzip2 wget
cd /usr/local/src
wget http://download.mono-project.com/sources/mono/mono-4.0.2.5.tar.bz2
tar jxf mono-4.0.2.5.tar.bz2
cd mono-4.0.2
./configure --prefix=/usr/local
make
make install

如果安装后用mono --version得到的版本不对，需要
增加初始化文件
vi /etc/profile.d/usr_local_bin.sh

填入内容
pathmunge () {
    case ":${PATH}:" in
        *:"$1":*)
            ;;
        *)
            if [ "$2" = "after" ] ; then
                PATH=$PATH:$1
            else
                PATH=$1:$PATH
            fi
    esac
}

pathmunge /usr/local/bin

如果需要支持访问https的网页，需要导入CA证书
cert-sync /etc/ssl/ca-bundle.pem

2)设置TCP，调节服务器最大socket数量和socket回收速度
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
