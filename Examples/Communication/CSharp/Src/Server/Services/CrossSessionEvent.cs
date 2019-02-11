using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public void RegisterCrossSessionEvents()
        {
            SessionContext.SessionLock.EnterWriteLock();
            try
            {
                Func<List<String>, Func<String>> GetVersionResolver = Versions =>
                {
                    var Sorted = Versions.Select(v => int.Parse(v)).OrderBy(v => v).ToList();
                    return () =>
                    {
                        var Version = SessionContext.Version;
                        if (Version == "") { return ""; }
                        if (Sorted.Count == 0) { return ""; }
                        var cv = int.Parse(Version);
                        return Sorted.TakeWhile(v => cv <= v).Last().ToString();
                    };
                };
                SessionContext.EventPump = CreateEventPump(GetVersionResolver);
            }
            finally
            {
                SessionContext.SessionLock.ExitWriteLock();
            }
        }

        public void UnregisterCrossSessionEvents()
        {
            SessionContext.SessionLock.EnterWriteLock();
            try
            {
                SessionContext.EventPump = null;
            }
            finally
            {
                SessionContext.SessionLock.ExitWriteLock();
            }
        }
    }
}
