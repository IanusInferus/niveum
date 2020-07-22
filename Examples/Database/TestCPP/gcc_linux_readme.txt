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
