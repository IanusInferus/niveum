using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.BaseSystem
{
    public class LockedVariable<T>
    {
        private T Value;
        private Object Lockee = new Object();
        public LockedVariable(T Value)
        {
            this.Value = Value;
        }

        public S Check<S>(Func<T, S> Map)
        {
            lock (Lockee)
            {
                return Map(Value);
            }
        }

        public void DoAction(Action<T> Action)
        {
            lock (Lockee)
            {
                Action(Value);
            }
        }

        public void Update(Func<T, T> Map)
        {
            lock (Lockee)
            {
                Value = Map(Value);
            }
        }
    }
}
