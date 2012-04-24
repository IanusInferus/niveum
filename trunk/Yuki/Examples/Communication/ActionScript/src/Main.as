package
{
    import context.ClientContext;
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
            EnableJsonClient();
        }
        
        public function EnableBinaryClient():void
        {
            var bc:BinaryClient;
        }

        public function EnableJsonClient():void
        {
            var bindings:Vector.<Binding> = new Vector.<Binding>();
            var binding:Binding = new Binding();
            binding.host = "localhost";
            binding.port = 8001;
            bindings.push(binding);
            var jsc:JsonSocketClient = new JsonSocketClient(bindings);
            jsc.doConnect();
            var jc:JsonClient = jsc.innerClient;
            var req:SendMessageRequest = new SendMessageRequest();
            req.content = "Hello.";
            jc.sendMessage(req, function(r:SendMessageReply):void
            {
               var r:SendMessageReply;
               if (r.onTooLong)
               {
                   trace("消息过长。");
               }
            });
        }
    }
}