using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TcpSendReceive
{
    public enum Mode
    {
        Escaped,
        Line,
        Binary
    }

    public class Configuration
    {
        public String IP;
        public int Port;
        public Mode Mode;
        public List<String> SchemaPaths;
    }
}
