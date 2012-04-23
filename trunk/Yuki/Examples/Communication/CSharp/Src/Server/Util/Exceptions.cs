using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public class QuitException : Exception
    {
        public QuitException() {}
        public QuitException(String Message) : base(Message) { }
        public QuitException(String Message, Exception InnerException) : base(Message, InnerException) { }
    }
}
