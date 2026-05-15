using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Firefly
{
    public sealed class ExceptionInfo
    {
        private ExceptionInfo() { }

        public static string AssemblyName
        {
            get { return Assembly.GetEntryAssembly().GetName().Name; }
        }
        public static string AssemblyTitle
        {
            get
            {
                var Attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), true);
                if (Attributes.Length >= 1)
                {
                    var Str = ((AssemblyTitleAttribute)Attributes[0]).Title;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyDescription
        {
            get
            {
                var Attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true);
                if (Attributes.Length >= 1)
                {
                    var Str = ((AssemblyDescriptionAttribute)Attributes[0]).Description;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyDescriptionOrTitle
        {
            get
            {
                var Description = AssemblyDescription;
                if (Description != "") return Description;
                var Title = AssemblyTitle;
                if (Title != "") return Title;
                return "";
            }
        }
        public static string AssemblyCompany
        {
            get
            {
                var Attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
                if (Attributes.Length >= 1)
                {
                    var Str = ((AssemblyCompanyAttribute)Attributes[0]).Company;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyProduct
        {
            get
            {
                var DescriptionAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), true);
                if (DescriptionAttributes.Length >= 1)
                {
                    var Str = ((AssemblyProductAttribute)DescriptionAttributes[0]).Product;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyCopyright
        {
            get
            {
                var DescriptionAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true);
                if (DescriptionAttributes.Length >= 1)
                {
                    var Str = ((AssemblyCopyrightAttribute)DescriptionAttributes[0]).Copyright;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyTrademark
        {
            get
            {
                var DescriptionAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTrademarkAttribute), true);
                if (DescriptionAttributes.Length >= 1)
                {
                    var Str = ((AssemblyTrademarkAttribute)DescriptionAttributes[0]).Trademark;
                    if (Str != "") return Str;
                }
                return "";
            }
        }
        public static string AssemblyVersion
        {
            get { return Assembly.GetEntryAssembly().GetName().Version.ToString(); }
        }
        public static string AssemblyFileVersion
        {
            get
            {
                var DescriptionAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true);
                if (DescriptionAttributes.Length >= 1)
                {
                    var Str = ((AssemblyFileVersionAttribute)DescriptionAttributes[0]).Version;
                    if (Str != "") return Str;
                }
                return AssemblyVersion;
            }
        }

        private static void GetExceptionInfoWithoutParent(Exception ex, StringBuilder msg, int Level)
        {
            if (ex.InnerException != null && !object.ReferenceEquals(ex.InnerException, ex) && Level < 3)
            {
                GetExceptionInfoWithoutParent(ex.InnerException, msg, Level + 1);
                msg.AppendLine(new string('-', 20));
            }
            msg.AppendLine(string.Format("{0}:" + System.Environment.NewLine + "{1}", ex.GetType().FullName, ex.Message));
            msg.AppendLine();
            msg.Append(GetStackTrace(ex));
        }
        public static string GetExceptionInfo(Exception ex)
        {
            return GetExceptionInfo(ex, new StackTrace(2, true));
        }
        public static string GetExceptionInfo(Exception ex, StackTrace ParentTrace)
        {
            var msg = new StringBuilder();
            GetExceptionInfoWithoutParent(ex, msg, 0);
            if (ParentTrace != null) msg.AppendLine(GetStackTrace(ParentTrace));
            return msg.ToString();
        }
        public static string GetStackTrace(Exception ex, StackTrace ParentTrace)
        {
            return GetStackTrace(ex) + GetStackTrace(ParentTrace);
        }
        public static string GetStackTrace(Exception ex)
        {
            var f = typeof(Exception).GetField("captured_traces", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                var captured_traces = f.GetValue(ex) as StackTrace[];
                if (captured_traces != null)
                {
                    return string.Join(new string('-', 20) + System.Environment.NewLine, captured_traces.Concat(new StackTrace[] { new StackTrace(ex, true) }).Select(s => GetStackTrace(s)));
                }
            }
            return GetStackTrace(new StackTrace(ex, true));
        }
        public static string GetStackTrace(StackTrace Trace)
        {
            if (Trace == null) return null;
            if (Trace.FrameCount == 0) return "";
            StringBuilder sb = new StringBuilder();
            foreach (var Frame in Trace.GetFrames())
            {
                sb.AppendLine(StackFrameToString(Frame));
            }
            return sb.ToString();
        }
        public static string StackFrameToString(StackFrame Frame)
        {
            var mi = Frame.GetMethod();
            if (mi == null) return "<unknown method>";
            var Params = new List<string>();
            foreach (var param in mi.GetParameters())
            {
                if (param.Name == "")
                {
                    Params.Add(param.ParameterType.Name);
                }
                else
                {
                    Params.Add(param.ParameterType.Name + " " + param.Name);
                }
            }

            var Pos = new List<string>();
            if (Frame.GetFileLineNumber() > 0) Pos.Add(string.Format("Line {0}", Frame.GetFileLineNumber()));
            if (Frame.GetFileColumnNumber() > 0) Pos.Add(string.Format("Column {0}", Frame.GetFileColumnNumber()));
            if (Frame.GetILOffset() != StackFrame.OFFSET_UNKNOWN) Pos.Add(string.Format("IL {0:X4}", Frame.GetILOffset()));
            if (Frame.GetNativeOffset() != StackFrame.OFFSET_UNKNOWN) Pos.Add(string.Format("N {0:X6}", Frame.GetNativeOffset()));

            var l = new List<string>();
            if (mi.DeclaringType != null) l.Add(StringDescape.Formats("{0}.", mi.DeclaringType.FullName));
            l.Add(mi.Name);
            l.Add(StringDescape.Formats("({0})", string.Join(", ", Params.ToArray())));
            if (Frame.GetFileName() != "") l.Add(StringDescape.Formats(" {0}", Frame.GetFileName()));
            if (Pos.Count > 0)
            {
                l.Add(" : ");
                l.Add(string.Join(", ", Pos.ToArray()));
            }
            return string.Join("", l.ToArray());
        }
    }
}
