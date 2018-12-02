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
using CommunicationPerformance;
using Firefly;
using Firefly.TextEncoding;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public SendMessageRequest SendMessageRequestAt1ToHead(SendMessageRequestAt1 o)
        {
            var ho = new SendMessageRequest();
            ho.Content = (o.Title != "" ? o.Title + "\r\n" : "") + String.Join("\r\n", o.Lines);
            return ho;
        }
        public SendMessageReplyAt1 SendMessageReplyAt1FromHead(SendMessageReply ho)
        {
            if (ho.OnSuccess)
            {
                return SendMessageReplyAt1.CreateSuccess();
            }
            if (ho.OnTooLong)
            {
                return SendMessageReplyAt1.CreateLinesTooLong();
            }
            throw new InvalidOperationException();
        }
        public MessageReceivedEventAt1 MessageReceivedEventAt1FromHead(MessageReceivedEvent ho)
        {
            var o = new MessageReceivedEventAt1();
            o.Title = "";
            o.Lines = ho.Content.UnifyNewLineToLf().Split('\n').ToList();
            return o;
        }
        public TestAddRequest TestAddRequestAt1ToHead(TestAddRequestAt1 o)
        {
            var ho = new TestAddRequest();
            ho.Left = o.Operand1;
            ho.Right = o.Operand2;
            return ho;
        }
    }
}
