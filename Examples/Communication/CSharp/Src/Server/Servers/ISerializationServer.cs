using System;

namespace Server
{
    public delegate void BinaryServerEventDelegate(String CommandName, UInt32 CommandHash, Byte[] Parameters);
    public interface IBinarySerializationServerAdapter
    {
        UInt64 Hash { get; }
        Boolean HasCommand(String CommandName, UInt32 CommandHash);
        void ExecuteCommand(String CommandName, UInt32 CommandHash, Byte[] Parameters, Action<Byte[]> OnSuccess, Action<Exception> OnFailure);
        event BinaryServerEventDelegate ServerEvent;
    }

    public delegate void JsonServerEventDelegate(String CommandName, UInt32 CommandHash, String Parameters);
    public interface IJsonSerializationServerAdapter
    {
        UInt64 Hash { get; }
        Boolean HasCommand(String CommandName);
        Boolean HasCommand(String CommandName, UInt32 CommandHash);
        void ExecuteCommand(String CommandName, String Parameters, Action<String> OnSuccess, Action<Exception> OnFailure);
        void ExecuteCommand(String CommandName, UInt32 CommandHash, String Parameters, Action<String> OnSuccess, Action<Exception> OnFailure);
        event JsonServerEventDelegate ServerEvent;
    }
}
