using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.TextEncoding;
using Communication;
using CommunicationDuplication;
using BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public event Action<CommunicationDuplication.ErrorEvent> CommunicationDuplicationDotError;

        public CommunicationDuplication.ServerTimeReply CommunicationDuplicationDotServerTime(CommunicationDuplication.ServerTimeRequest r)
        {
            return CommunicationDuplication.ServerTimeReply.CreateSuccess(DateTime.UtcNow.DateTimeUtcToString());
        }
    }
}
