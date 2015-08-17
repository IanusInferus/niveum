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
