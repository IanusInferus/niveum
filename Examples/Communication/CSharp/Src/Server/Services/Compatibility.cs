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
            ho.Content = (o.Message.Title != "" ? o.Message.Title + "\r\n" : "") + String.Join("\r\n", o.Message.Lines);
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
        public SendMessageRequest SendMessageAt2RequestToHead(SendMessageAt2Request o)
        {
            var ho = new SendMessageRequest();
            ho.Content = (o.Message.Title != "" ? o.Message.Title + "\r\n" : "") + String.Join("\r\n", o.Message.Lines);
            return ho;
        }
        public SendMessageAt2Reply SendMessageAt2ReplyFromHead(SendMessageReply ho)
        {
            if (ho.OnSuccess)
            {
                return SendMessageAt2Reply.CreateSuccess();
            }
            if (ho.OnTooLong)
            {
                return SendMessageAt2Reply.CreateLinesTooLong();
            }
            throw new InvalidOperationException();
        }
        public MessageReceivedAt2Event MessageReceivedAt2EventFromHead(MessageReceivedEvent ho)
        {
            var o = new MessageReceivedAt2Event();
            o.Title = "";
            o.Lines = ho.Content.UnifyNewLineToLf().Split('\n').ToList();
            return o;
        }
        public TestAddRequest TestAddAt1RequestToHead(TestAddAt1Request o)
        {
            var ho = new TestAddRequest();
            ho.Left = o.Operand1;
            ho.Right = o.Operand2.ValueOrDefault(0);
            return ho;
        }
        public TestAddRequest TestAddAt2RequestToHead(TestAddAt2Request o)
        {
            var ho = new TestAddRequest();
            ho.Left = o.Left.ValueOrDefault(0);
            ho.Right = o.Right;
            return ho;
        }
        public AverageResultAt1 AverageResultAt1FromHead(AverageResult ho)
        {
            var o = new AverageResultAt1();
            o.Value = (int)(ho.Value);
            return o;
        }
        public AverageInput AverageInputAt1ToHead(AverageInputAt1 o)
        {
            var ho = new AverageInput();
            ho.Value = o.Value;
            return ho;
        }
        public TestSumAt2Reply TestSumAt2(TestSumAt2Request r)
        {
            return TestSumAt2Reply.CreateResult(r.Values.Sum());
        }
    }
}
