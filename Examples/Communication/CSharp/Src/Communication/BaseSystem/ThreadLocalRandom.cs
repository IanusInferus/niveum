using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BaseSystem
{
    public class ThreadLocalRandom
    {
        //用于保证线程安全
        private static ThreadLocal<Random> _RNG = new ThreadLocal<Random>(() => new Random());
        public static Random RNG
        {
            get
            {
                return _RNG.Value;
            }
        }
    }
}
