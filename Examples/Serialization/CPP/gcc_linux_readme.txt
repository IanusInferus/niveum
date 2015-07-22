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
进入CMakeLists.txt所在文件夹，运行
cmake .
生成调试版Makefile
或者运行
cmake -DCMAKE_BUILD_TYPE=Release .
生成发布版Makefile
然后运行
make
等待编译结束。
