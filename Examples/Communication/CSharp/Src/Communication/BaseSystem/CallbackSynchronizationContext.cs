using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BaseSystem
{
    public class CallbackSynchronizationContext : SynchronizationContext
    {
        private Action<Action> QueueUserWorkItem;
        public CallbackSynchronizationContext(Action<Action> QueueUserWorkItem)
        {
            this.QueueUserWorkItem = QueueUserWorkItem;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            QueueUserWorkItem(() => d.Invoke(state));
        }
        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException();
        }
    }
}
