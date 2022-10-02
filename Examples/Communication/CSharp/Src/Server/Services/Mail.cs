using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public GetUserProfileReply GetUserProfile(GetUserProfileRequest r)
        {
            //TODO

            return GetUserProfileReply.CreateNotExist();
        }
    }
}
