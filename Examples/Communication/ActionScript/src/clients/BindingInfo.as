package clients 
{
    import flash.net.Socket;
    public class BindingInfo 
    {
        public static const INITIALIZED:int = 0;
        public static const RUNNING:int = 1;
        public static const CLOSED:int = 2;
        
        public var socket:Socket;
        public var status:int = INITIALIZED;
   }
}
