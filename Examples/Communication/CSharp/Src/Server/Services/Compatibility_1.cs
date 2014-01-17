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
using Firefly;
using Firefly.TextEncoding;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public SendMessageRequest SendMessageAt1RequestToHead(SendMessageAt1Request o)
        {
            var ho = new SendMessageRequest();
            ho.Content = (o.Title != "" ? o.Title + "\r\n" : "") + String.Join("\r\n", o.Lines);
            return ho;
        }
        public SendMessageAt1Reply SendMessageAt1ReplyFromHead(SendMessageReply ho)
        {
            if (ho.OnSuccess)
            {
                return SendMessageAt1Reply.CreateSuccess();
            }
            if (ho.OnTooLong)
            {
                return SendMessageAt1Reply.CreateLinesTooLong();
            }
            throw new InvalidOperationException();
        }
        public MessageReceivedAt1Event MessageReceivedAt1EventFromHead(MessageReceivedEvent ho)
        {
            var o = new MessageReceivedAt1Event();
            o.Title = "";
            o.Lines = ho.Content.UnifyNewLineToLf().Split('\n').ToList();
            return o;
        }
        public TestAddRequest TestAddAt1RequestToHead(TestAddAt1Request o)
        {
            var ho = new TestAddRequest();
            ho.Left = o.Operand1;
            ho.Right = o.Operand2;
            return ho;
        }
    }
}
