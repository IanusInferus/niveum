Windows下环境配置指南

1)设置UDP最大端口数
显示最大端口数
netsh int ipv4 show dynamicport tcp
netsh int ipv4 show dynamicport udp
netsh int ipv6 show dynamicport tcp
netsh int ipv6 show dynamicport udp

设置端口范围
netsh int ipv4 set dynamic udp start=16384 num=49152
netsh int ipv6 set dynamic udp start=16384 num=49152
