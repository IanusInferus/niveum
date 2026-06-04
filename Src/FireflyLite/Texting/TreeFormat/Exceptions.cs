using System;
using System.Collections.Generic;

namespace Firefly.Texting.TreeFormat.Syntax
{
    public class FileTextRange
    {
        public Text Text;
        public Optional<TextRange> Range;
    }

    public class InvalidSyntaxException : Exception
    {
        public InvalidSyntaxException()
        {
        }

        public InvalidSyntaxException(string Message)
            : base(Message)
        {
        }
        public InvalidSyntaxException(string Message, Exception InnerException)
            : base(Message, InnerException)
        {
        }
        public InvalidSyntaxException(string Message, Optional<FileTextRange> Range)
            : base(GetMessage(Message, Range))
        {
            RangeValue = Range;
        }
        public InvalidSyntaxException(string Message, Optional<FileTextRange> Range, Exception InnerException)
            : base(GetMessage(Message, Range), InnerException)
        {
            RangeValue = Range;
        }

        private Optional<FileTextRange> RangeValue;

        public Optional<FileTextRange> Range
        {
            get { return RangeValue; }
        }

        protected static string GetMessage(string Message, Optional<FileTextRange> Range)
        {
            var l = new List<string>();
            if (Range.OnSome)
            {
                var RangeValue = Range.Value;
                if (RangeValue.Text != null && RangeValue.Text.Path != "") l.Add(RangeValue.Text.Path);
                if (RangeValue.Range.OnSome)
                {
                    l.Add(RangeValue.Range.Value.ToString());
                }
                if (Message != "")
                {
                    if (l.Count > 0)
                    {
                        l.Add(" : {0}".Formats(Message));
                    }
                    else
                    {
                        l.Add(Message);
                    }
                }
            }
            return string.Join("", l);
        }
    }

    public class InvalidTokenException : InvalidSyntaxException
    {
        public InvalidTokenException()
        {
        }

        public InvalidTokenException(string Message)
            : base(Message)
        {
        }
        public InvalidTokenException(string Message, Exception InnerException)
            : base(Message, InnerException)
        {
        }
        public InvalidTokenException(string Message, Optional<FileTextRange> Range, string Token)
            : base(GetTokenMessage(Message, Range, Token))
        {
            TokenValue = Token;
        }
        public InvalidTokenException(string Message, Optional<FileTextRange> Range, string Token, Exception InnerException)
            : base(GetTokenMessage(Message, Range, Token), InnerException)
        {
            TokenValue = Token;
        }

        private string TokenValue;

        public string Token
        {
            get { return TokenValue; }
        }

        private static string GetTokenMessage(string Message, Optional<FileTextRange> Range, string Token)
        {
            if (Message == "")
            {
                return GetMessage("'{0}' : InvalidToken".Formats(Token), Range);
            }
            return GetMessage("'{0}' : {1}".Formats(Token, Message), Range);
        }
    }

    public class InvalidSyntaxRuleException : InvalidSyntaxException
    {
        public InvalidSyntaxRuleException()
        {
        }

        public InvalidSyntaxRuleException(string Message)
            : base(Message)
        {
        }
        public InvalidSyntaxRuleException(string Message, Exception InnerException)
            : base(Message, InnerException)
        {
        }
        public InvalidSyntaxRuleException(string Message, Optional<FileTextRange> Range, Token Token)
            : base(GetTokenMessage(Message, Range, Token))
        {
            TokenValue = Token;
        }
        public InvalidSyntaxRuleException(string Message, Optional<FileTextRange> Range, Token Token, Exception InnerException)
            : base(GetTokenMessage(Message, Range, Token), InnerException)
        {
            TokenValue = Token;
        }

        private Token TokenValue;

        public Token Token
        {
            get { return TokenValue; }
        }

        private static string GetTokenMessage(string Message, Optional<FileTextRange> Range, Token Token)
        {
            if (Message == "")
            {
                return GetMessage("'{0}' : InvalidSyntaxAtToken".Formats(Token.ToString()), Range);
            }
            return GetMessage("'{0}' : {1}".Formats(Token.ToString(), Message), Range);
        }
    }

    public class InvalidEvaluationException : InvalidSyntaxException
    {
        public InvalidEvaluationException()
        {
        }

        public InvalidEvaluationException(string Message)
            : base(Message)
        {
        }
        public InvalidEvaluationException(string Message, Exception InnerException)
            : base(Message, InnerException)
        {
        }
        public InvalidEvaluationException(string Message, Optional<FileTextRange> Range, object SyntaxRule)
            : base(GetTokenMessage(Message, Range))
        {
            SyntaxRuleValue = SyntaxRule;
        }
        public InvalidEvaluationException(string Message, Optional<FileTextRange> Range, object SyntaxRule, Exception InnerException)
            : base(GetTokenMessage(Message, Range), InnerException)
        {
            SyntaxRuleValue = SyntaxRule;
        }

        private object SyntaxRuleValue;

        public object SyntaxRule
        {
            get { return SyntaxRuleValue; }
        }

        private static string GetTokenMessage(string Message, Optional<FileTextRange> Range)
        {
            if (Message == "")
            {
                return GetMessage("InvalidSyntaxRule", Range);
            }
            return GetMessage(Message, Range);
        }
    }
}
