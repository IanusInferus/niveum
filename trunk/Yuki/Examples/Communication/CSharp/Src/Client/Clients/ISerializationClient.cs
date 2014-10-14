﻿using System;

namespace Client
{
    public delegate void BinaryClientEventDelegate(String CommandName, UInt32 CommandHash, Byte[] Parameters);
    public interface IBinarySerializationClientAdapter
    {
        UInt64 Hash { get; }
        void DequeueCallback(String CommandName);
        void HandleResult(String CommandName, UInt32 CommandHash, Byte[] Parameters);
        event BinaryClientEventDelegate ClientEvent;
    }

    public delegate void JsonClientEventDelegate(String CommandName, UInt32 CommandHash, String Parameters);
    public interface IJsonSerializationClientAdapter
    {
        UInt64 Hash { get; }
        void DequeueCallback(String CommandName);
        void HandleResult(String CommandName, UInt32 CommandHash, String Parameters);
        event JsonClientEventDelegate ClientEvent;
    }
}