using System;

namespace Server
{
    public delegate void BinaryServerEventDelegate(String CommandName, UInt32 CommandHash, Byte[] Parameters);
    public interface IBinarySerializationServerAdapter
    {
        UInt64 Hash { get; }
        Boolean HasCommand(String CommandName, UInt32 CommandHash);
        Byte[] ExecuteCommand(String CommandName, UInt32 CommandHash, Byte[] Parameters);
        event BinaryServerEventDelegate ServerEvent;
    }

    public delegate void JsonServerEventDelegate(String CommandName, UInt32 CommandHash, String Parameters);
    public interface IJsonSerializationServerAdapter
    {
        UInt64 Hash { get; }
        Boolean HasCommand(String CommandName);
        Boolean HasCommand(String CommandName, UInt32 CommandHash);
        String ExecuteCommand(String CommandName, String Parameters);
        String ExecuteCommand(String CommandName, UInt32 CommandHash, String Parameters);
        event JsonServerEventDelegate ServerEvent;
    }
}
