package
{
    import flash.display.Sprite;
    import flash.events.Event;
    import communication.*;

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
            var bc:BinaryClient;
            var jc:JsonClient;
        }
    }
}