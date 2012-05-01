package
{
    import communication.*;
    import context.*;

    public class ClientImplementation implements IClientImplementation
    {
        private var c:ClientContext;
        
        public function ClientImplementation(c:ClientContext) 
        {
            this.c = c;
        }

        /** 错误 */
        public function error(e:ErrorEvent):void
        {
            var m:String = "调用'" + e.commandName + "'发生错误:" + e.message;
            trace(m);
            try
            {
                c.dequeueCallback(e.commandName);
            }
            catch (err:Error)
            {
                trace(err);
            }
        }
        
        /** 接收到消息 */
        public function messageReceived(e:MessageReceivedEvent):void
        {
            trace(e.content);
        }
        /** 接收到消息 */
        public function messageReceivedAt1(e:MessageReceivedAt1Event):void
        {
        }
        /** 接到群发消息 */
        public function testMessageReceived(e:TestMessageReceivedEvent):void
        {
        }
    }
}
