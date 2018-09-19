﻿//==========================================================================
//
//  Notice:      This file is automatically generated.
//               Please don't modify this file.
//
//==========================================================================

using System;
using System.Collections.Generic;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;
using UInt8 = System.Byte;
using UInt16 = System.UInt16;
using UInt32 = System.UInt32;
using UInt64 = System.UInt64;
using Int8 = System.SByte;
using Int16 = System.Int16;
using Int32 = System.Int32;
using Int64 = System.Int64;
using Float32 = System.Single;
using Float64 = System.Double;

namespace Communication
{
    /// <summary>关闭服务器</summary>
    [Record]
    public sealed class ShutdownRequest
    {
    }
    public enum ShutdownReplyTag
    {
        /// <summary>成功</summary>
        Success = 0
    }
    /// <summary>关闭服务器</summary>
    [TaggedUnion]
    public sealed class ShutdownReply
    {
        [Tag] public ShutdownReplyTag _Tag;

        /// <summary>成功</summary>
        public Unit Success;

        /// <summary>成功</summary>
        public static ShutdownReply CreateSuccess() { return new ShutdownReply { _Tag = ShutdownReplyTag.Success, Success = default(Unit) }; }

        /// <summary>成功</summary>
        public Boolean OnSuccess { get { return _Tag == ShutdownReplyTag.Success; } }
    }

    /// <summary>错误</summary>
    [Record]
    public sealed class ErrorEvent
    {
        /// <summary>错误信息</summary>
        public String Message;
    }

    /// <summary>错误命令</summary>
    [Record]
    public sealed class ErrorCommandEvent
    {
        /// <summary>客户端命令名称</summary>
        public String CommandName;
    }

    /// <summary>服务器时间</summary>
    [Record]
    public sealed class ServerTimeRequest
    {
    }
    public enum ServerTimeReplyTag
    {
        /// <summary>服务器时间</summary>
        Success = 0
    }
    /// <summary>服务器时间</summary>
    [TaggedUnion]
    public sealed class ServerTimeReply
    {
        [Tag] public ServerTimeReplyTag _Tag;

        /// <summary>服务器时间</summary>
        public String Success;

        /// <summary>服务器时间</summary>
        public static ServerTimeReply CreateSuccess(String Value) { return new ServerTimeReply { _Tag = ServerTimeReplyTag.Success, Success = Value }; }

        /// <summary>服务器时间</summary>
        public Boolean OnSuccess { get { return _Tag == ServerTimeReplyTag.Success; } }
    }

    /// <summary>退出</summary>
    [Record]
    public sealed class QuitRequest
    {
    }
    public enum QuitReplyTag
    {
        /// <summary>成功</summary>
        Success = 0
    }
    /// <summary>退出</summary>
    [TaggedUnion]
    public sealed class QuitReply
    {
        [Tag] public QuitReplyTag _Tag;

        /// <summary>成功</summary>
        public Unit Success;

        /// <summary>成功</summary>
        public static QuitReply CreateSuccess() { return new QuitReply { _Tag = QuitReplyTag.Success, Success = default(Unit) }; }

        /// <summary>成功</summary>
        public Boolean OnSuccess { get { return _Tag == QuitReplyTag.Success; } }
    }

    /// <summary>检测类型结构版本</summary>
    [Record]
    public sealed class CheckSchemaVersionRequest
    {
        /// <summary>版本散列</summary>
        public String Hash;
    }
    public enum CheckSchemaVersionReplyTag
    {
        /// <summary>最新</summary>
        Head = 0,
        /// <summary>支持</summary>
        Supported = 1,
        /// <summary>不支持</summary>
        NotSupported = 2
    }
    /// <summary>检测类型结构版本</summary>
    [TaggedUnion]
    public sealed class CheckSchemaVersionReply
    {
        [Tag] public CheckSchemaVersionReplyTag _Tag;

        /// <summary>最新</summary>
        public Unit Head;
        /// <summary>支持</summary>
        public Unit Supported;
        /// <summary>不支持</summary>
        public Unit NotSupported;

        /// <summary>最新</summary>
        public static CheckSchemaVersionReply CreateHead() { return new CheckSchemaVersionReply { _Tag = CheckSchemaVersionReplyTag.Head, Head = default(Unit) }; }
        /// <summary>支持</summary>
        public static CheckSchemaVersionReply CreateSupported() { return new CheckSchemaVersionReply { _Tag = CheckSchemaVersionReplyTag.Supported, Supported = default(Unit) }; }
        /// <summary>不支持</summary>
        public static CheckSchemaVersionReply CreateNotSupported() { return new CheckSchemaVersionReply { _Tag = CheckSchemaVersionReplyTag.NotSupported, NotSupported = default(Unit) }; }

        /// <summary>最新</summary>
        public Boolean OnHead { get { return _Tag == CheckSchemaVersionReplyTag.Head; } }
        /// <summary>支持</summary>
        public Boolean OnSupported { get { return _Tag == CheckSchemaVersionReplyTag.Supported; } }
        /// <summary>不支持</summary>
        public Boolean OnNotSupported { get { return _Tag == CheckSchemaVersionReplyTag.NotSupported; } }
    }

    /// <summary>服务器关闭</summary>
    [Record]
    public sealed class ServerShutdownEvent
    {
    }

    /// <summary>发送消息</summary>
    [Record]
    public sealed class SendMessageRequest
    {
        /// <summary>内容</summary>
        public String Content;
    }
    public enum SendMessageReplyTag
    {
        /// <summary>成功</summary>
        Success = 0,
        /// <summary>内容过长</summary>
        TooLong = 1
    }
    /// <summary>发送消息</summary>
    [TaggedUnion]
    public sealed class SendMessageReply
    {
        [Tag] public SendMessageReplyTag _Tag;

        /// <summary>成功</summary>
        public Unit Success;
        /// <summary>内容过长</summary>
        public Unit TooLong;

        /// <summary>成功</summary>
        public static SendMessageReply CreateSuccess() { return new SendMessageReply { _Tag = SendMessageReplyTag.Success, Success = default(Unit) }; }
        /// <summary>内容过长</summary>
        public static SendMessageReply CreateTooLong() { return new SendMessageReply { _Tag = SendMessageReplyTag.TooLong, TooLong = default(Unit) }; }

        /// <summary>成功</summary>
        public Boolean OnSuccess { get { return _Tag == SendMessageReplyTag.Success; } }
        /// <summary>内容过长</summary>
        public Boolean OnTooLong { get { return _Tag == SendMessageReplyTag.TooLong; } }
    }

    /// <summary>接收到消息</summary>
    [Record]
    public sealed class MessageReceivedEvent
    {
        /// <summary>内容</summary>
        public String Content;
    }

    /// <summary>加法</summary>
    [Record]
    public sealed class TestAddRequest
    {
        /// <summary>操作数1</summary>
        public Int Left;
        /// <summary>操作数2</summary>
        public Int Right;
    }
    public enum TestAddReplyTag
    {
        /// <summary>结果</summary>
        Result = 0
    }
    /// <summary>加法</summary>
    [TaggedUnion]
    public sealed class TestAddReply
    {
        [Tag] public TestAddReplyTag _Tag;

        /// <summary>结果</summary>
        public Int Result;

        /// <summary>结果</summary>
        public static TestAddReply CreateResult(Int Value) { return new TestAddReply { _Tag = TestAddReplyTag.Result, Result = Value }; }

        /// <summary>结果</summary>
        public Boolean OnResult { get { return _Tag == TestAddReplyTag.Result; } }
    }

    /// <summary>两百万次浮点乘法</summary>
    [Record]
    public sealed class TestMultiplyRequest
    {
        /// <summary>操作数</summary>
        public Real Operand;
    }
    public enum TestMultiplyReplyTag
    {
        /// <summary>结果</summary>
        Result = 0
    }
    /// <summary>两百万次浮点乘法</summary>
    [TaggedUnion]
    public sealed class TestMultiplyReply
    {
        [Tag] public TestMultiplyReplyTag _Tag;

        /// <summary>结果</summary>
        public Real Result;

        /// <summary>结果</summary>
        public static TestMultiplyReply CreateResult(Real Value) { return new TestMultiplyReply { _Tag = TestMultiplyReplyTag.Result, Result = Value }; }

        /// <summary>结果</summary>
        public Boolean OnResult { get { return _Tag == TestMultiplyReplyTag.Result; } }
    }

    /// <summary>文本原样返回</summary>
    [Record]
    public sealed class TestTextRequest
    {
        /// <summary>文本</summary>
        public String Text;
    }
    public enum TestTextReplyTag
    {
        /// <summary>文本</summary>
        Result = 0
    }
    /// <summary>文本原样返回</summary>
    [TaggedUnion]
    public sealed class TestTextReply
    {
        [Tag] public TestTextReplyTag _Tag;

        /// <summary>文本</summary>
        public String Result;

        /// <summary>文本</summary>
        public static TestTextReply CreateResult(String Value) { return new TestTextReply { _Tag = TestTextReplyTag.Result, Result = Value }; }

        /// <summary>文本</summary>
        public Boolean OnResult { get { return _Tag == TestTextReplyTag.Result; } }
    }

    /// <summary>群发消息</summary>
    [Record]
    public sealed class TestMessageRequest
    {
        /// <summary>消息</summary>
        public String Message;
    }
    public enum TestMessageReplyTag
    {
        /// <summary>成功，在线人数</summary>
        Success = 0
    }
    /// <summary>群发消息</summary>
    [TaggedUnion]
    public sealed class TestMessageReply
    {
        [Tag] public TestMessageReplyTag _Tag;

        /// <summary>成功，在线人数</summary>
        public Int Success;

        /// <summary>成功，在线人数</summary>
        public static TestMessageReply CreateSuccess(Int Value) { return new TestMessageReply { _Tag = TestMessageReplyTag.Success, Success = Value }; }

        /// <summary>成功，在线人数</summary>
        public Boolean OnSuccess { get { return _Tag == TestMessageReplyTag.Success; } }
    }

    /// <summary>接到群发消息</summary>
    [Record]
    public sealed class TestMessageReceivedEvent
    {
        /// <summary>消息</summary>
        public String Message;
    }

    public interface IApplicationServer
    {
        /// <summary>关闭服务器</summary>
        ShutdownReply Shutdown(ShutdownRequest r);
        /// <summary>错误</summary>
        event Action<ErrorEvent> Error;
        /// <summary>错误命令</summary>
        event Action<ErrorCommandEvent> ErrorCommand;
        /// <summary>服务器时间</summary>
        ServerTimeReply ServerTime(ServerTimeRequest r);
        /// <summary>退出</summary>
        QuitReply Quit(QuitRequest r);
        /// <summary>检测类型结构版本</summary>
        CheckSchemaVersionReply CheckSchemaVersion(CheckSchemaVersionRequest r);
        /// <summary>服务器关闭</summary>
        event Action<ServerShutdownEvent> ServerShutdown;
        /// <summary>发送消息</summary>
        SendMessageReply SendMessage(SendMessageRequest r);
        /// <summary>接收到消息</summary>
        event Action<MessageReceivedEvent> MessageReceived;
        /// <summary>加法</summary>
        TestAddReply TestAdd(TestAddRequest r);
        /// <summary>两百万次浮点乘法</summary>
        TestMultiplyReply TestMultiply(TestMultiplyRequest r);
        /// <summary>文本原样返回</summary>
        TestTextReply TestText(TestTextRequest r);
        /// <summary>群发消息</summary>
        TestMessageReply TestMessage(TestMessageRequest r);
        /// <summary>接到群发消息</summary>
        event Action<TestMessageReceivedEvent> TestMessageReceived;
    }

    public interface IApplicationClient
    {
        UInt64 Hash { get; }
        void DequeueCallback(String CommandName);

        /// <summary>关闭服务器</summary>
        void Shutdown(ShutdownRequest r, Action<ShutdownReply> Callback);
        /// <summary>错误</summary>
        event Action<ErrorEvent> Error;
        /// <summary>错误命令</summary>
        event Action<ErrorCommandEvent> ErrorCommand;
        /// <summary>服务器时间</summary>
        void ServerTime(ServerTimeRequest r, Action<ServerTimeReply> Callback);
        /// <summary>退出</summary>
        void Quit(QuitRequest r, Action<QuitReply> Callback);
        /// <summary>检测类型结构版本</summary>
        void CheckSchemaVersion(CheckSchemaVersionRequest r, Action<CheckSchemaVersionReply> Callback);
        /// <summary>服务器关闭</summary>
        event Action<ServerShutdownEvent> ServerShutdown;
        /// <summary>发送消息</summary>
        void SendMessage(SendMessageRequest r, Action<SendMessageReply> Callback);
        /// <summary>接收到消息</summary>
        event Action<MessageReceivedEvent> MessageReceived;
        /// <summary>加法</summary>
        void TestAdd(TestAddRequest r, Action<TestAddReply> Callback);
        /// <summary>两百万次浮点乘法</summary>
        void TestMultiply(TestMultiplyRequest r, Action<TestMultiplyReply> Callback);
        /// <summary>文本原样返回</summary>
        void TestText(TestTextRequest r, Action<TestTextReply> Callback);
        /// <summary>群发消息</summary>
        void TestMessage(TestMessageRequest r, Action<TestMessageReply> Callback);
        /// <summary>接到群发消息</summary>
        event Action<TestMessageReceivedEvent> TestMessageReceived;
    }

    public interface IEventPump
    {
        /// <summary>错误</summary>
        Action<ErrorEvent> Error { get; }
        /// <summary>错误命令</summary>
        Action<ErrorCommandEvent> ErrorCommand { get; }
        /// <summary>服务器关闭</summary>
        Action<ServerShutdownEvent> ServerShutdown { get; }
        /// <summary>接收到消息</summary>
        Action<MessageReceivedEvent> MessageReceived { get; }
        /// <summary>接到群发消息</summary>
        Action<TestMessageReceivedEvent> TestMessageReceived { get; }
    }
}