CentOS下环境配置指南

假设是CentOS 6.5 64 bit系统。

1.安装cmake，用于生成Makefile，安装旧版g++，用于编译新版g++
yum install cmake
yum install gcc
yum install gcc-c++

2.安装g++ 5.2
进入一个临时目录，执行
wget ftp://mirrors.kernel.org/gnu/binutils/binutils-2.25.tar.bz2
tar -xvf binutils-2.25.tar.bz2

cd binutils-2.25
./configure --disable-nls

make -j4
make install

回到刚才的临时目录，执行
wget ftp://mirrors.kernel.org/gnu/gcc/gcc-5.2.0/gcc-5.2.0.tar.bz2
tar -xvf gcc-5.2.0.tar.bz2

cd gcc-5.2.0
./contrib/download_prerequisites

./configure --enable-checking=release --enable-languages=c,c++ --disable-multilib

make -j4
make install

ln -sf /usr/local/lib64/libstdc++.so.6 /usr/lib64/libstdc++.so.6


cd /usr/include/c++
ln -sf /usr/local/src/libcxx/include v1

回到刚才的临时目录，执行
wget ftp://ftp.gnu.org/gnu/termcap/termcap-1.3.1.tar.gz
tar -xvf termcap-1.3.1.tar.gz

cd termcap-1.3.1
./configure

make -j4
make install

wget ftp://mirrors.kernel.org/gnu/gdb/gdb-7.9.tar.xz
tar -xvf gdb-7.9.tar.xz

cd gdb-7.9
./configure

make -j4
make install

如果提示找不到makeinfo，可以忽略

在.bashrc中加入
export CC=/usr/local/bin/gcc
export CPP=/usr/local/bin/cpp
export CXX=/usr/local/bin/c++

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

5.异常信息
如果出现异常信息需要知道对应的源代码行号，可以将异常信息存到文件中（如error.log），然后执行
cat error.log | sed -n 's/^.*\[0x\([0-9A-Fa-f]*\)\].*$/\1/p' | xargs addr2line -e ExceptionStackTrace
