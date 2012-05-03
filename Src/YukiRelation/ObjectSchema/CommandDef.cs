//==========================================================================
//
//  File:        CommandDef.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 命令定义
//  Version:     2012.04.15.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaSchema;

namespace Yuki.ObjectSchema
{
    public enum CommandDefTag
    {
        Client,
        Server
    }
    [TaggedUnion, DebuggerDisplay("{ToString()}")]
    public sealed class CommandDef
    {
        [Tag]
        public CommandDefTag _Tag;
        public ClientCommandDef Client;
        public ServerCommandDef Server;

        public static CommandDef CreateClient(ClientCommandDef Value) { return new CommandDef { _Tag = CommandDefTag.Client, Client = Value }; }
        public static CommandDef CreateServer(ServerCommandDef Value) { return new CommandDef { _Tag = CommandDefTag.Server, Server = Value }; }

        public Boolean OnClient { get { return _Tag == CommandDefTag.Client; } }
        public Boolean OnServer { get { return _Tag == CommandDefTag.Server; } }

        public override string ToString()
        {
            return DebuggerDisplayer.ConvertToString(this);
        }
    }
}
