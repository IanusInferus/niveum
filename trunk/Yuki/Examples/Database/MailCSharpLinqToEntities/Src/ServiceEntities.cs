using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Database.Entities
{
    /// <summary>性别</summary>
    public enum Gender_ : int // Entity Framework 内部表示CLR类型时不带命名空间，会与其他类型混淆，导致出错。
    {
        /// <summary>男</summary>
        Male = 0,
        /// <summary>女</summary>
        Female = 1,
    }

    /// <summary>用户账号信息</summary>
    public sealed class UserProfile
    {
        /// <summary>用户号</summary>
        public Int32 Id { get; set; }
        /// <summary>用户名</summary>
        public String Name { get; set; }
        /// <summary>邮件地址</summary>
        public Optional<String> EmailAddress { get; set; }
        /// <summary>性别</summary>
        public Gender_ Gender { get; set; }
    }

    /// <summary>邮件头</summary>
    public sealed class MailHeader
    {
        /// <summary>邮件ID</summary>
        public Int64 Id { get; set; }
        /// <summary>标题</summary>
        public String Title { get; set; }
        /// <summary>发件用户</summary>
        public UserProfile From { get; set; }
        /// <summary>是否是新邮件</summary>
        public Boolean IsNew { get; set; }
        /// <summary>时间(UTC)：yyyy-MM-ddTHH:mm:ssZ形式</summary>
        public String Time { get; set; }
    }

    /// <summary>邮件详细</summary>
    public sealed class MailDetail
    {
        /// <summary>邮件ID</summary>
        public Int64 Id { get; set; }
        /// <summary>标题</summary>
        public String Title { get; set; }
        /// <summary>发件用户</summary>
        public UserProfile From { get; set; }
        /// <summary>收件用户</summary>
        public List<UserProfile> Tos { get; set; }
        /// <summary>时间(UTC)：yyyy-MM-ddTHH:mm:ssZ形式</summary>
        public String Time { get; set; }
        /// <summary>内容</summary>
        public String Content { get; set; }
        /// <summary>附件</summary>
        public List<String> Attachments { get; set; }
    }

    /// <summary>邮件</summary>
    public sealed class MailInput
    {
        /// <summary>标题</summary>
        public String Title { get; set; }
        /// <summary>收件用户ID</summary>
        public List<int> ToIds { get; set; }
        /// <summary>内容</summary>
        public String Content { get; set; }
        /// <summary>附件</summary>
        public List<MailAttachment> Attachments { get; set; }
    }

    /// <summary>邮件附件</summary>
    public sealed class MailAttachment
    {
        /// <summary>名称</summary>
        public String Name { get; set; }
        /// <summary>内容</summary>
        public List<Byte> Content { get; set; }
    }
}
