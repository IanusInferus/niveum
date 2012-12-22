Haxe示例配置指南

Windows平台下需要软件
.Net Framework 4.5
Haxe r5693以上
nginx
FlashDevelop，可选，编辑环境
Chrome，可选，调试环境，支持Source map功能，可以直接调试Haxe代码

1)配置Haxe
从下面地址下载安装包安装，然后
http://haxe.org/file/Haxe-2.10-Windows.exe

从下面地址下载最新版，覆盖安装目录
http://94.142.242.48/builds/windows/?C=M;O=D

Haxe的JSON库haxe.Json中存在一个bug，读取UTF-8文本时，如果含有中文会出错。
补丁如下(适用于svn5693)：
std\haxe\Json.hx : 337

			#if (neko || php || cpp)
			// ensure utf8 chars are not cut
			else if( c >= 0x80 ) {
				pos++;
-				if( c >= 0xE0 ) pos += 1 + (c & 32);
+				if( c >= 0xFC ) pos += 4;
+				else if( c >= 0xF8 ) pos += 3;
+				else if( c >= 0xF0 ) pos += 2;
+				else if( c >= 0xE0 ) pos++;
			}
			#end

到目前(svn5712)，补丁尚没有进入SVN。

2)生成代码并编译CSharp服务器
运行外层目录BuildCode.cmd。
运行外层目录CSharp\Src\Build.cmd。
运行CSharp\Bin\Server.exe，若没有权限则按提示增加权限。
CSharp服务器默认在8003端口监听HTTP请求。

3)配置nginx
从http://nginx.org/下载nginx。
安装nginx，修改配置文件conf/nginx.conf，删去原来的location /，增加如下配置。
        location / {
            root   D:/Projects/YUKI/Examples/Communication/Haxe/bin;
            index  index.xhtml index.html index.htm;
        }

        location = /cmd {
            proxy_pass   http://127.0.0.1:8003;
        }
其中路径要做相应更改。
运行nginx。
注意nginx不能Ctrl+C直接关闭，需要执行命令nginx -s stop才能关闭。

4)编译运行例子
运行Build.cmd编译，运行Run.cmd从Chrome打开网页。

5)编辑
打开Client.hxproj，可以在FlashDevelop中编辑脚本。
