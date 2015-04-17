using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Firefly;
using Firefly.TextEncoding;
using Firefly.Texting;
using BaseSystem;

namespace Server
{
    public class FileLoggerSync
    {
        public static void WriteLog(String Path, String Text)
        {
            var Dir = FileNameHandling.GetFileDirectory(Path);
            if (Dir != "" && !Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }
            String[] Lines;
            if (!Text.Contains('\n'))
            {
                Lines = new String[] { Text };
            }
            else
            {
                var l = new List<String>();
                l.Add("****************");
                foreach (var m in Text.UnifyNewLineToLf().Split('\n'))
                {
                    l.Add(m);
                }
                l.Add("****************");
                Lines = l.ToArray();
            }
            var e = Encoding.UTF8;
            if (File.Exists(Path))
            {
                e = Txt.GetEncoding(Path, e);
            }
            using (var r = new StreamWriter(Path, true, e))
            {
                foreach (var Line in Lines)
                {
                    r.WriteLine(Line);
                }
            }
        }

        private static String DecorateWithQoutesIfComplex(String s)
        {
            if (s.Any(c => c == ' ' || c == '\"' || Char.IsControl(c)))
            {
                return @"""" + s.Escape().Replace(@"""", @"""""") + @"""";
            }
            return s;
        }
        private static String ExtendToMultipleOf(String s, int Multiple)
        {
            return s + new String(' ', (Multiple - (s.Length % Multiple)) % Multiple);
        }
        public static String[] GetLines(SessionLogEntry Entry)
        {
            var Time = Entry.Time.DateTimeUtcWithMillisecondsToString();
            String Start;
            {
                var l = new List<String>();
                l.Add(DecorateWithQoutesIfComplex(Time));
                l.Add(DecorateWithQoutesIfComplex(Entry.RemoteEndPoint.ToString()));
                l.Add(DecorateWithQoutesIfComplex(Entry.Token));
                l.Add(ExtendToMultipleOf(DecorateWithQoutesIfComplex(Entry.Type), 4));
                l.Add(DecorateWithQoutesIfComplex(Entry.Name));
                Start = String.Join(" ", l.ToArray());
            }
            String[] Lines;
            if (!(Entry.Message.StartsWith(" ") || Entry.Message.Contains('\n')))
            {
                Lines = new String[] { Start + " " + Entry.Message };
            }
            else
            {
                var l = new List<String>();
                l.Add(Start);
                l.Add("****************");
                foreach (var m in Entry.Message.UnifyNewLineToLf().Split('\n'))
                {
                    if (m.TrimStart(' ').StartsWith("*"))
                    {
                        l.Add(" " + m);
                    }
                    else
                    {
                        l.Add(m);
                    }
                }
                l.Add("****************");
                Lines = l.ToArray();
            }
            return Lines;
        }

        public static void WriteLog(String LogDir, SessionLogEntry Entry)
        {
            var LocalTime = Entry.Time.ToLocalTime();
            var Dir = FileNameHandling.GetPath(LogDir, LocalTime.Date.ToString("yyyyMMdd"));
            if (Dir != "" && !Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }
            var Path = FileNameHandling.GetPath(Dir, String.Format("{0}-{1}.log", Entry.RemoteEndPoint.ToString().Replace(".", "_").Replace(":", "_"), Entry.Token));
            var e = Encoding.UTF8;
            var Lines = GetLines(Entry);
            if (File.Exists(Path))
            {
                e = Txt.GetEncoding(Path, e);
            }
            using (var r = new StreamWriter(Path, true, e))
            {
                foreach (var Line in Lines)
                {
                    r.WriteLine(Line);
                }
            }
        }
    }
}
