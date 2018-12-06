Haxe示例配置指南

Windows平台下需要软件
.Net Framework 4.0
Haxe 3.4.2
nginx，可选，部署环境
FlashDevelop，可选，开发环境
Chrome，可选，调试环境，支持Source map功能，可以直接调试Haxe代码

1)配置Haxe
从下面地址下载安装包安装，然后
https://github.com/HaxeFoundation/haxe/releases/download/3.4.2/haxe-3.4.2-win.exe

2)生成代码并编译CSharp服务器
运行外层目录BuildCode.cmd。
运行外层目录CSharp\Src\Build.cmd。
运行CSharp\Bin\Server.exe，若没有权限则按提示增加权限。
CSharp服务器默认在80端口监听HTTP请求。

3)配置nginx，可选
从http://nginx.org/下载nginx。
安装nginx，修改配置文件conf/nginx.conf，删去原来的location /，增加如下配置。
        location / {
            root   D:/Projects/Niveum/Examples/Communication/Haxe/bin;
            index  index.xhtml index.html index.htm;
        }

        location = /api/q {
            rewrite      ^.*$  /api/q  break;
            proxy_pass   http://127.0.0.1:8003;
            proxy_set_header    X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header    X-Forwarded-Port $remote_port;
        }
其中路径要做相应更改。
运行nginx。
注意nginx不能Ctrl+C直接关闭，需要执行命令nginx -s stop才能关闭。

4)编译运行例子
运行Build.cmd编译，运行Run.cmd从Chrome打开网页。
或用任意浏览器进入
http://localhost:8003/haxe/
或
http://localhost/haxe/

5)编辑
打开Client.hxproj，可以在FlashDevelop中编辑脚本。
