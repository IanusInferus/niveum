//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.Examples <Visual C#>
//  Description: 邮件管理程序
//  Version:     2015.08.18.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using Firefly;
using BaseSystem;
using Database.Entities;

namespace Database
{
    public static class Program
    {
        public static int Main(String[] args)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return MainInner();
            }
            else
            {
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                    return -1;
                }
            }
        }

        public static int MainInner()
        {
            DisplayTitle();

            var CmdLine = CommandLine.GetCmdLine();

            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
            }

            var argv = CmdLine.Arguments;
            if (argv.Length != 1)
            {
                DisplayInfo();
                return -1;
            }

            var ConnectionString = argv[0];

            using (var cl = new CascadeLock())
            using (var dam = new DataAccessManager(ConnectionString, cl))
            {
                s = new MailService(dam);

                Console.WriteLine("输入help获得命令列表。");

                while (true)
                {
                    Console.Write("#");
                    var Line = Console.ReadLine();

                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        if (!RunLine(Line))
                        {
                            break;
                        }
                    }
                    else
                    {
                        try
                        {
                            if (!RunLine(Line))
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                            return -1;
                        }
                    }
                }
            }

            return 0;
        }

        private static MailService s = null;
        private static Boolean RunLine(String Line)
        {
            var Parts = Regex.Split(Line.Trim(), @"\s+");
            if (Parts.Length == 1 && Parts[0].ToLowerInvariant() == "help")
            {
                DisplayHelp();
            }
            else if (Parts.Length == 1 && Parts[0].ToLowerInvariant() == "exit")
            {
                return false;
            }
            else if (Parts.Length == 1 && Parts[0].ToLowerInvariant() == "users")
            {
                var Users = s.GetUsers();
                foreach (var u in Users)
                {
                    Console.WriteLine(u);
                }
            }
            else if (Parts.Length == 2 && Parts[0].ToLowerInvariant() == "login")
            {
                if (s.Login(Parts[1]))
                {
                    Console.WriteLine("登陆成功。");
                }
                else
                {
                    Console.WriteLine("登陆失败。");
                }
            }
            else if (Parts.Length == 1 && Parts[0].ToLowerInvariant() == "list")
            {
                List(0, 10);
            }
            else if (Parts.Length == 3 && Parts[0].ToLowerInvariant() == "list")
            {
                List(int.Parse(Parts[1]), int.Parse(Parts[2]));
            }
            else if (Parts.Length == 2 && Parts[0].ToLowerInvariant() == "view")
            {
                View(int.Parse(Parts[1]));
            }
            else if (Parts.Length == 2 && Parts[0].ToLowerInvariant() == "dump")
            {
                Dump(int.Parse(Parts[1]));
            }
            else if (Parts.Length == 2 && Parts[0].ToLowerInvariant() == "delete")
            {
                Delete(int.Parse(Parts[1]));
            }
            else if (Parts.Length == 1 && Parts[0].ToLowerInvariant() == "send")
            {
                Send();
            }
            else
            {
                Console.WriteLine("未知命令。");
            }
            return true;
        }

        private static void List(int Skip, int Take)
        {
            var Count = s.GetMailCount();
            Console.WriteLine(String.Format("{0}-{1}/{2}", Skip + 1, Skip + Take, Count));
            var MailHeaders = s.GetMailHeaders(Skip, Take);
            foreach (var mh in MailHeaders)
            {
                Console.WriteLine(String.Format("{0,4}  {1,-8} {2,-20} {3,-4} {4}", mh.Id, mh.From.Name, mh.Time, mh.IsNew ? "New" : "", mh.Title));
            }
        }

        private static void View(int MailId)
        {
            var m = s.GetMail(MailId);
            Console.WriteLine(m.Title);
            Console.WriteLine("发件人: " + m.From.Name);
            Console.WriteLine("收件人: " + String.Join(", ", m.Tos.Select(t => t.Name).ToArray()));
            if (m.Attachments.Count > 0)
            {
                Console.WriteLine("附件: " + String.Join(", ", m.Attachments.ToArray()));
            }
            Console.WriteLine(m.Content);
        }

        private static void Dump(int MailId)
        {
            var l = s.GetMailAttachments(MailId);
            foreach (var a in l)
            {
                Console.WriteLine(a.Name);
                File.WriteAllBytes(a.Name, a.Content.ToArray());
            }
        }

        private static void Delete(int MailId)
        {
            s.DeleteMail(MailId);
        }

        private static void Send()
        {
            List<int> ToIds;
            while (true)
            {
                Console.WriteLine("请输入收件人，以空格隔开:");
                var Line = Console.ReadLine().Trim();
                if (Line == "")
                {
                    Console.WriteLine("至少要有一个收件人。");
                    continue;
                }
                var Parts = Regex.Split(Line, @"\s+");
                var Users = Parts.Select(p => new { Id = s.GetUserIdByName(p), Name = p }).ToArray();
                var UsersNotExist = Users.Where(u => !u.Id.HasValue).ToArray();
                if (UsersNotExist.Length > 0)
                {
                    Console.WriteLine("收件人" + String.Join(" ", UsersNotExist.Select(u => u.Name).ToArray()) + "不存在。");
                    continue;
                }
                ToIds = Users.Select(u => u.Id.Value).ToList();
                break;
            }
            Console.WriteLine("请输入标题:");
            var Title = Console.ReadLine();

            var Lines = new List<String>();
            Console.WriteLine("请输入内容，输入单个的点号(.)结束:");
            while (true)
            {
                var Line = Console.ReadLine();
                if (Line == ".") { break; }
                Lines.Add(Line);
            }
            var Content = String.Join("\r\n", Lines.ToArray()) + "\r\n";

            List<MailAttachment> Attachments;
            while (true)
            {
                Console.WriteLine("请输入当前文件夹下的附件名，以空格隔开:");
                var Line = Console.ReadLine().Trim();
                if (Line == "")
                {
                    Attachments = new List<MailAttachment> { };
                    break;
                }
                var Parts = Regex.Split(Line, @"\s+");
                var AttachmentNames = Parts.Select(p => new { Exist = File.Exists(p), Name = p }).ToArray();
                var AttachmentsNotExist = AttachmentNames.Where(a => !a.Exist).ToArray();
                if (AttachmentsNotExist.Length > 0)
                {
                    Console.WriteLine("文件" + String.Join(" ", AttachmentsNotExist.Select(a => a.Name).ToArray()) + "不存在。");
                    continue;
                }
                Attachments = AttachmentNames.Select(a => new MailAttachment { Name = a.Name, Content = new List<Byte>(File.ReadAllBytes(a.Name)) }).ToList();
                break;
            }

            var Mail = new MailInput
            {
                Title = Title,
                ToIds = ToIds,
                Content = Content,
                Attachments = Attachments
            };
            s.SendMail(Mail);
        }

        public static void DisplayTitle()
        {
            Console.WriteLine(@"邮件管理程序");
            Console.WriteLine(@"Author:      F.R.C.");
            Console.WriteLine(@"Copyright(C) Public Domain");
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"用法:");
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name + @" <ConnectionString>");
            Console.WriteLine(@"ConnectionString 数据库连接字符串");
            Console.WriteLine(@"示例:");
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name + @" """ + DataAccessManager.GetConnectionStringExample() + @"""");
            Console.WriteLine(@"");
            DisplayHelp();
        }

        public static void DisplayHelp()
        {
            Console.WriteLine(@"命令:");
            Console.WriteLine(@"help                    显示帮助");
            Console.WriteLine(@"exit                    退出");
            Console.WriteLine(@"users                   列出所有用户");
            Console.WriteLine(@"login <user>            登陆");
            Console.WriteLine(@"list [skip=0 take=10]   列出当前用户的邮件");
            Console.WriteLine(@"view <emailId>          显示邮件");
            Console.WriteLine(@"dump <emailId>          导出附件到当前文件夹");
            Console.WriteLine(@"delete <emailId>        删除邮件");
            Console.WriteLine(@"send                    发送邮件");
        }
    }
}
