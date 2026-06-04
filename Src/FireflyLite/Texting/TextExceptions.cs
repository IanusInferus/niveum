using System;
using System.Collections.Generic;
using System.IO;

namespace Firefly.Texting
{
    public class FileLocationInformation
    {
        public string Path = "";
        public int LineNumber = 0;
        public int ColumnNumber = 0;
    }

    public interface IFileLocationInformationProvider
    {
        FileLocationInformation FileLocationInformation { get; }
    }

    public class InvalidTextFormatException : Exception
    {
        public InvalidTextFormatException() { }

        public InvalidTextFormatException(string Message, FileLocationInformation i)
            : base(GetMessage(Message, i))
        {
            FileLocationInformationValue = i;
        }
        public InvalidTextFormatException(string Message, FileLocationInformation i, Exception InnerException)
            : base(GetMessage(Message, i), InnerException)
        {
            FileLocationInformationValue = i;
        }

        private FileLocationInformation FileLocationInformationValue;

        public FileLocationInformation FileLocationInformation
        {
            get { return FileLocationInformationValue; }
        }

        private static string GetMessage(string Message, FileLocationInformation i)
        {
            var l = new List<string>();
            if (i.Path != "") l.Add(i.Path);
            if (i.LineNumber != 0 && i.ColumnNumber != 0)
            {
                l.Add("({0}, {1})".Formats(i.LineNumber, i.ColumnNumber));
            }
            else
            {
                if (i.LineNumber != 0) l.Add("({0})".Formats(i.LineNumber));
                if (i.ColumnNumber != 0) l.Add("({0})".Formats(i.ColumnNumber));
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
            return string.Join("", l.ToArray());
        }
    }

    public class InvalidTextFormatOrEncodingException : Exception
    {
        public InvalidTextFormatOrEncodingException() { }

        public InvalidTextFormatOrEncodingException(string Message, FileLocationInformation i)
            : base(GetMessage(Message, i))
        {
            FileLocationInformationValue = i;
        }
        public InvalidTextFormatOrEncodingException(string Message, FileLocationInformation i, Exception InnerException)
            : base(GetMessage(Message, i), InnerException)
        {
            FileLocationInformationValue = i;
        }

        private FileLocationInformation FileLocationInformationValue;

        public FileLocationInformation FileLocationInformation
        {
            get { return FileLocationInformationValue; }
        }

        private static string GetMessage(string Message, FileLocationInformation i)
        {
            var l = new List<string>();
            if (i.Path != "") l.Add(i.Path);
            if (i.LineNumber != 0 && i.ColumnNumber != 0)
            {
                l.Add("({0}, {1})".Formats(i.LineNumber, i.ColumnNumber));
            }
            else
            {
                if (i.LineNumber != 0) l.Add("({0})".Formats(i.LineNumber));
                if (i.ColumnNumber != 0) l.Add("({0})".Formats(i.ColumnNumber));
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
            return string.Join("", l.ToArray());
        }
    }
}
