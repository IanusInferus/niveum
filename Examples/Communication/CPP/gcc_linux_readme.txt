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

如果要调试，则还需要
yum --enablerepo=testing-devtools-2-centos-6 install devtoolset-2-binutils devtoolset-2-gdb
yum install /usr/bin/debuginfo-install
当使用gdb调试时如果显示Missing separate debuginfos，则按提示执行debuginfo-install。
如果提示找不到文件，则修改/etc/yum.repos.d/CentOS-Debuginfo.repo中的[debug]下的
enabled=0
为
enabled=1

如果文件不存在，则新建如下文件
# CentOS-Base.repo
#
# The mirror system uses the connecting IP address of the client and the
# update status of each mirror to pick mirrors that are updated to and
# geographically close to the client.  You should use this for CentOS updates
# unless you are manually picking other mirrors.
#

# All debug packages from all the various CentOS-5 releases
# are merged into a single repo, split by BaseArch
#
# Note: packages in the debuginfo repo are currently not signed
#

[debug]
name=CentOS-6 - Debuginfo
baseurl=http://debuginfo.centos.org/6/$basearch/
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-Debug-6
enabled=1

3.编译程序
进入CMakeLists.txt所在文件夹，运行
cmake -DCMAKE_BUILD_TYPE=Debug .
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
