using System;

namespace Server
{
    /// <summary>
    /// 实现时，本接口的所有公共成员均应是线程安全的。
    /// </summary>
    public interface ILogger : IDisposable
    {
        void Start();
        /// <summary>只能在Start之后，Stop之前调用，线程安全</summary>
        void Push(SessionLogEntry e);
    }
}
