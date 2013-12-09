//==========================================================================
//
//  Notice:      This file is varmatically generated.
//               Please don't modify this file.
//
//==========================================================================

//Reference:

using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        private class EventPump : IEventPump
        {
            public Action<ErrorEvent> Error { get; set; }
            public Action<ErrorCommandEvent> ErrorCommand { get; set; }
            public Action<ServerShutdownEvent> ServerShutdown { get; set; }
            public Action<MessageReceivedEvent> MessageReceived { get; set; }
            public Action<TestMessageReceivedEvent> TestMessageReceived { get; set; }
        }
        private IEventPump CreateEventPump(Func<String> GetVersion)
        {
            var ep = new EventPump();
            ep.Error = e => { if (Error != null) { Error(e); } };
            ep.ErrorCommand = e => { if (ErrorCommand != null) { ErrorCommand(e); } };
            ep.ServerShutdown = e => { if (ServerShutdown != null) { ServerShutdown(e); } };
            ep.MessageReceived = eHead =>
            {
                var Version = GetVersion();
                if (Version == "")
                {
                    if (MessageReceived != null) { MessageReceived(eHead); }
                    return;
                }
                if (Version == "1")
                {
                    var e = MessageReceivedAt1EventFromHead(eHead);
                    if (MessageReceivedAt1 != null) { MessageReceivedAt1(e); }
                    return;
                }
                throw new InvalidOperationException();
            };
            ep.TestMessageReceived = e => { if (TestMessageReceived != null) { TestMessageReceived(e); } };
            return ep;
        }

        public SendMessageAt1Reply SendMessageAt1(SendMessageAt1Request r)
        {
            var HeadRequest = SendMessageAt1RequestToHead(r);
            var HeadReply = SendMessage(HeadRequest);
            var Reply = SendMessageAt1ReplyFromHead(HeadReply);
            return Reply;
        }
        //public SendMessageRequest SendMessageAt1RequestToHead(SendMessageAt1Request o)
        //{
        //    var ho = new SendMessageRequest();
        //    ho.Content = o.Content;
        //    return ho;
        //}
        //public SendMessageAt1Reply SendMessageAt1ReplyFromHead(SendMessageReply ho)
        //{
        //    if (ho.OnSuccess)
        //    {
        //        return SendMessageAt1Reply.CreateSuccess();
        //    }
        //    if (ho.OnTooLong)
        //    {
        //        return SendMessageAt1Reply.CreateTooLong(ho.TooLong);
        //    }
        //    throw new InvalidOperationException();
        //}

        public event Action<MessageReceivedAt1Event> MessageReceivedAt1;
        //public MessageReceivedAt1Event MessageReceivedAt1EventFromHead(MessageReceivedEvent ho)
        //{
        //    var o = new MessageReceivedAt1Event();
        //    o.Title = ho.Title;
        //    o.Lines = ho.Lines;
        //    return o;
        //}
    }
}
