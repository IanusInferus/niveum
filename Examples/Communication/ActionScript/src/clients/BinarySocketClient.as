package clients
{
    import flash.errors.*;
    import flash.events.*;
    import flash.net.Socket;
    import flash.utils.ByteArray;
    import com.brooksandrus.utils.ISO8601Util;
    import communication.*;
    import context.*;

    public class BinarySocketClient implements IBinarySender
    {
        private var ci:IClientImplementation;
        private var innerClientValue:BinaryClient;
        public function get innerClient():BinaryClient { return innerClientValue; }
        private var c:ClientContext;
        public function get context():context.ClientContext { return c; }

        private var bindings:Vector.<Binding>
        private var bindingInfos:Vector.<BindingInfo>
        private var index:int;
        private var onceConnected:Boolean = false;
        private var readBuffer:ByteArray; //接收缓冲区
        private var readBufferLength:int;
        private var writeBuffer:ByteArray; //发送缓冲区
        private var bsm:BufferStateMachine;

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

        /// handleResult : (commandName : String, params : String) -> unit
        public function BinarySocketClient(bindings:Vector.<Binding>)
        {
            c = new ClientContext();
            ci = new ClientImplementation(c);
            this.innerClientValue = new BinaryClient(this, ci);
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
            c.dequeueCallback = innerClientValue.dequeueCallback;
            readBuffer = new ByteArray();
            readBuffer.length = 8 * 1024;
            readBufferLength = 0;
            writeBuffer = new ByteArray();
            bsm = new BufferStateMachine();
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

        private var iso:ISO8601Util = new ISO8601Util();
        public function sendChat(ba:ByteArray):void
        {
            if (ba.length == 0)
            {
                return;
            }

            if (!connected)
            {
                writeBuffer.writeBytes(ba, 0, ba.length);
                doConnect();
                return;
            }

            flushRequest();

            socket.writeBytes(ba, 0, ba.length);
            socket.flush();
        }

        public function send(commandName:String, commandHash:uint, parameters:ByteArray):void
        {
            var ts:String = iso.formatExtendedDateTime(new Date()) + " /" + commandName;
            //trace(ts);
            var ba:ByteArray = new ByteArray();
            BinaryTranslator.stringToBinary(ba, commandName);
            BinaryTranslator.uint32ToBinary(ba, commandHash);
            BinaryTranslator.int32ToBinary(ba, parameters.length);
            ba.writeBytes(parameters);
            sendChat(ba);
        }

        private function readResponse():void
        {
            var firstPosition:int = 0;
            var bytesCount:uint = socket.bytesAvailable;
            socket.readBytes(readBuffer, readBufferLength, bytesCount);
            readBufferLength += bytesCount;
            while (true)
            {
                var r:TryShiftResult = bsm.TryShift(readBuffer, firstPosition, readBufferLength - firstPosition);
                if (r == null)
                {
                    break;
                }
                firstPosition = r.position;

                if (r.command != null)
                {
                    var cmd:Command = r.command;
                    innerClientValue.handleResult(cmd.commandName, cmd.commandHash, cmd.parameters);
                }
            }
            if (firstPosition > 0)
            {
                var copyLength:int = readBufferLength - firstPosition;
                var nba:ByteArray = new ByteArray();
                nba.length = copyLength;
                readBuffer.position = firstPosition;
                readBuffer.readBytes(nba, 0, copyLength);
                readBuffer.position = 0;
                readBuffer.writeBytes(nba, 0, copyLength);
                readBufferLength = copyLength;
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
