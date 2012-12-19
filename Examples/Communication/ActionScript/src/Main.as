//==========================================================================
//
//  File:        Main.as
//  Location:    Yuki.Examples <ActionScript>
//  Description: 聊天客户端
//  Version:     2012.12.19.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

package
{
    import flash.display.Sprite;
    import flash.events.Event;
    import communication.*;
    import clients.*;

    public class Main extends Sprite
    {

        public function Main():void
        {
            if (stage) init();
            else addEventListener(Event.ADDED_TO_STAGE, init);
        }

        private function init(e:Event = null):void
        {
            removeEventListener(Event.ADDED_TO_STAGE, init);
            // entry point

            try
            {
                MainInner();
            }
            catch (ex:Error)
            {
                trace(ex);
            }
        }

        public function MainInner():void
        {
            //EnableBinaryClient();
            EnableJsonClient();
        }
        
        public function EnableBinaryClient():void
        {
            var bindings:Vector.<Binding> = new Vector.<Binding>();
            var binding:Binding = new Binding();
            binding.host = "localhost";
            binding.port = 8001;
            bindings.push(binding);
            var bsc:BinarySocketClient = new BinarySocketClient(bindings);
            bsc.doConnect();
			bsc.innerClient.error = function(e:ErrorEvent):void
			{
				var m:String = "调用'" + e.commandName + "'发生错误:" + e.message;
				trace(m);
			}
			ReadLineAndSendLoop(bsc.innerClient);
        }

        public function EnableJsonClient():void
        {
            var bindings:Vector.<Binding> = new Vector.<Binding>();
            var binding:Binding = new Binding();
            binding.host = "localhost";
            binding.port = 8002;
            bindings.push(binding);
            var jsc:JsonSocketClient = new JsonSocketClient(bindings);
            jsc.doConnect();
			jsc.innerClient.error = function(e:ErrorEvent):void
			{
				var m:String = "调用'" + e.commandName + "'发生错误:" + e.message;
				trace(m);
			}
			ReadLineAndSendLoop(jsc.innerClient);
        }

		public function ReadLineAndSendLoop(InnerClient:IApplicationClient):void
		{
			InnerClient.messageReceived = function(e:MessageReceivedEvent):void
			{
				trace(e.content);
			}
            var req:SendMessageRequest = new SendMessageRequest();
            req.content = "Hello.";
            InnerClient.sendMessage(req, function(r:SendMessageReply):void
            {
               if (r.onTooLong)
               {
                   trace("消息过长。");
               }
            });
		}
    }
}
