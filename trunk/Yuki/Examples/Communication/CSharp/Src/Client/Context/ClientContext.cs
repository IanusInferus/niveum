using System;

namespace Client
{
    public class ClientContext
    {
        public Action<String> DequeueCallback;

        public Object Lockee = new Object();

        public int NumOnline;
        public int Num;
        public Action Completed;

        public Int64 Sum = 0;

        public BinaryCountPacketClientContext BinaryCountPacketContext
        {
            get
            {
                if (bcpc == null)
                {
                    bcpc = new BinaryCountPacketClientContext();
                }
                return bcpc;
            }
        }
        private BinaryCountPacketClientContext bcpc;
    }
}
