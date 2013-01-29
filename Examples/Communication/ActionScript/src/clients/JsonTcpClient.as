package clients
{
    import flash.errors.*;
    import flash.events.*;
    import flash.net.Socket;
    import flash.utils.ByteArray;
    import com.brooksandrus.utils.ISO8601Util;
    import communication.*;

    public class JsonTcpClient implements IJsonSender
    {
        private var innerClientValue:JsonSerializationClient;
        public function get innerClient():IApplicationClient { return innerClientValue; }

        private var bindings:Vector.<Binding>
        private var bindingInfos:Vector.<BindingInfo>
        private var index:int;
        private var onceConnected:Boolean = false;
        private var readBuffer:ByteArray; //接收缓冲区
        private var writeBuffer:ByteArray; //发送缓冲区

        public function get binding():Binding
        {
            return bindings[index];
        }

        private function get socket():Socket
        {
            return bindingInfos[index].socket;
        }

        public function get connected():Boolean
        {
            return bindingInfos[index].status == BindingInfo.RUNNING;
        }

        public function close():void
        {
            if (bindingInfos[index].socket != null)
            {
                bindingInfos[index].socket.close();
                bindingInfos[index].socket = null;
                bindingInfos[index].status = BindingInfo.CLOSED;
            }
        }

        public function JsonTcpClient(bindings:Vector.<Binding>)
        {
            this.innerClientValue = new JsonSerializationClient(this);
            if (bindings.length == 0)
            {
                throw new RangeError("bindings");
            }
            this.bindings = bindings;
            this.bindingInfos = new Vector.<BindingInfo>();
            for (var i:int = 0; i < bindings.length; i += 1)
            {
                bindingInfos[i] = new BindingInfo();
            }
            innerClient.errorCommand = function(e:communication.ErrorCommandEvent):void
            {
                innerClient.dequeueCallback(e.commandName);
            };
            readBuffer = new ByteArray();
            writeBuffer = new ByteArray();
        }

        public function doConnect():void
        {
            if (socket != null) { return; }
            var currentIndex:int = index;
            var b:Binding = bindings[currentIndex];
            var host:String = b.host;
            var port:int = b.port;
            var ts:String = iso.formatExtendedDateTime(new Date()) + " " + "连接到: host=" + host + " port=" + port;
            trace(ts);

            var s:Socket = new Socket();
            bindingInfos[currentIndex].socket = s;
            bindingInfos[currentIndex].status = BindingInfo.INITIALIZED;
            s.connect(host, port);
            s.addEventListener(Event.CLOSE, function(event:Event):void
            {
                bindingInfos[currentIndex].socket = null;
                bindingInfos[currentIndex].status = BindingInfo.CLOSED;
                var ts:String = iso.formatExtendedDateTime(new Date()) + " 连接关闭: host=" + host + " port=" + port + " " + event;
                trace(ts);
            });
            s.addEventListener(Event.CONNECT, function(event:Event):void
            {
                bindingInfos[currentIndex].status = BindingInfo.RUNNING;
                onceConnected = true;
                var ts:String = iso.formatExtendedDateTime(new Date()) + " 连接成功: host=" + host + " port=" + port + " " + event;
                trace(ts);
                flushRequest();
            });
            s.addEventListener(IOErrorEvent.IO_ERROR, function(event:IOErrorEvent):void
            {
                bindingInfos[currentIndex].socket = null;
                bindingInfos[currentIndex].status = BindingInfo.CLOSED;
                var ts:String = iso.formatExtendedDateTime(new Date()) + " 连接失败: host=" + host + " port=" + port + " " + event;
                trace(ts);

                if (!onceConnected && (currentIndex == index))
                {
                    if (index + 1 < bindings.length)
                    {
                        index += 1;
                    }
                    else
                    {
                        index = 0;
                    }

                    var ts2:String = iso.formatExtendedDateTime(new Date()) + " 选择地址: " + bindings[index].host;
                    //trace(ts2);

                    if (index != 0)
                    {
                        doConnect();
                    }
                }
            });
            s.addEventListener(SecurityErrorEvent.SECURITY_ERROR, function(event:SecurityErrorEvent):void
            {
                bindingInfos[currentIndex].socket = null;
                bindingInfos[currentIndex].status = BindingInfo.CLOSED;
                var ts:String = iso.formatExtendedDateTime(new Date()) + " 安全错误: host=" + host + " port=" + port + " " + event;
                trace(ts);

                if (!onceConnected && (currentIndex == index))
                {
                    if (index + 1 < bindings.length)
                    {
                        index += 1;
                    }
                    else
                    {
                        index = 0;
                    }

                    var ts2:String = iso.formatExtendedDateTime(new Date()) + " 选择地址: " + bindings[index].host;
                    //trace(ts2);

                    if (index != 0)
                    {
                        doConnect();
                    }
                }
            });
            s.addEventListener(ProgressEvent.SOCKET_DATA, function(event:ProgressEvent):void
            {
                //trace("接受服务器信息: " + event);
                readResponse();
            });
        }

        private function toHex8String(v:uint):String
        {
            var s:String = v.toString(16).toUpperCase();
            if (s.length >= 8)
            {
                return s;
            }
            return "00000000".substr(0, 8 - s.length) + s;
        }
        private var iso:ISO8601Util = new ISO8601Util();
        public function sendChat(s:String):void
        {
            if (s.length == 0)
            {
                return;
            }

            if (!connected)
            {
                writeBuffer.writeUTFBytes(s);
                doConnect();
                return;
            }

            flushRequest();

            socket.writeUTFBytes(s);
            socket.flush();
        }

        public function send(commandName:String, commandHash:uint, params:String):void
        {
            var ts:String = iso.formatExtendedDateTime(new Date()) + " /" + commandName;
            //trace(ts);
            sendChat("/" + commandName + "@" + toHex8String(commandHash) + " " + params + "\r\n");
        }

        private function parseServerData(data:String):void
        {
            var arr:Array = data.split(" ", 3);
            var cmd:String = arr[1];
            var cmdParts:Array = cmd.split("@", 2);
            var cmdName:String = cmdParts[0];
            var cmdHash:uint = (uint)(parseInt(cmdParts[1], 16));
            try
            {
                var ts:String = iso.formatExtendedDateTime(new Date()) + " /svr " + cmd;
                //trace(ts);
                innerClientValue.handleResult(cmdName, cmdHash, arr[2]);
            }
            catch (ex:Error)
            {
                trace("命令'" + cmd + "'出错 : " + ex.getStackTrace());
            }
        }

        private function readResponse():void
        {
            var bytesCount:uint = socket.bytesAvailable;
            for (var k:int = 0; k < bytesCount; k += 1)
            {
                var b:int = socket.readByte();
                if (b == 0xD) { continue; } //0D CR
                if (b == 0xA) //0A LF
                {
                    var usableBytesCount:uint = readBuffer.position;
                    readBuffer.position = 0;
                    var s:String = readBuffer.readUTFBytes(usableBytesCount);
                    readBuffer.position = 0;
                    parseServerData(s);
                    continue;
                }
                readBuffer.writeByte(b);
            }
        }

        private function flushRequest():void
        {
            if (writeBuffer.length > 0)
            {
                socket.writeBytes(writeBuffer);
                writeBuffer = new ByteArray();
            }
            socket.flush();
        }
    }
}
