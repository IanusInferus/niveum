using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Database.Entities
{
    [Record]
    public struct Unit
    {
    }

    public class RecordAttribute : Firefly.Mapping.MetaSchema.RecordAttribute
    {
    }

    public class AliasAttribute : Firefly.Mapping.MetaSchema.AliasAttribute
    {
    }

    public class TaggedUnionAttribute : Firefly.Mapping.MetaSchema.TaggedUnionAttribute
    {
    }

    public class TagAttribute : Firefly.Mapping.MetaSchema.TagAttribute
    {
    }

    public class TupleAttribute : Firefly.Mapping.MetaSchema.TupleAttribute
    {
    }

    public enum OptionalTag
    {
        NotHasValue = 0,
        HasValue = 1
    }
    [TaggedUnion]
    public struct Optional<T>
    {
        [Tag]
        public OptionalTag _Tag { get; set; }

        public Unit NotHasValue { get; set; }
        public T HasValue { get; set; }

        public static Optional<T> CreateNotHasValue() { return new Optional<T> { _Tag = OptionalTag.NotHasValue, NotHasValue = new Unit() }; }
        public static Optional<T> CreateHasValue(T Value) { return new Optional<T> { _Tag = OptionalTag.HasValue, HasValue = Value }; }

        public Boolean OnNotHasValue { get { return _Tag == OptionalTag.NotHasValue; } }
        public Boolean OnHasValue { get { return _Tag == OptionalTag.HasValue; } }

        public static Optional<T> Empty { get { return CreateNotHasValue(); } }
        public static implicit operator Optional<T>(T v) { return Optional<T>.CreateHasValue(v); }
        public static explicit operator T(Optional<T> v)
        {
            if (v.OnNotHasValue)
            {
                throw new InvalidOperationException();
            }
            return v.HasValue;
        }
        public static Boolean operator ==(Optional<T> Left, Optional<T> Right)
        {
            return Equals(Left, Right);
        }
        public static Boolean operator !=(Optional<T> Left, Optional<T> Right)
        {
            return !Equals(Left, Right);
        }
        public static Boolean operator ==(Optional<T>? Left, Optional<T>? Right)
        {
            return Equals(Left, Right);
        }
        public static Boolean operator !=(Optional<T>? Left, Optional<T>? Right)
        {
            return !Equals(Left, Right);
        }
        public override Boolean Equals(Object obj)
        {
            return Equals(this, obj);
        }
        public override Int32 GetHashCode()
        {
            if (OnNotHasValue) { return 0; }
            return HasValue.GetHashCode();
        }

        private static Boolean Equals(Optional<T> Left, Optional<T> Right)
        {
            if (Left.OnNotHasValue && Right.OnNotHasValue)
            {
                return true;
            }
            if (Left.OnNotHasValue || Right.OnNotHasValue)
            {
                return false;
            }
            return Left.HasValue.Equals(Right.HasValue);
        }
        private static Boolean Equals(Optional<T>? Left, Optional<T>? Right)
        {
            if ((!Left.HasValue || Left.Value.OnNotHasValue) && (!Right.HasValue || Right.Value.OnNotHasValue))
            {
                return true;
            }
            if (!Left.HasValue || Left.Value.OnNotHasValue || !Right.HasValue || Right.Value.OnNotHasValue)
            {
                return false;
            }
            return Equals(Left.Value, Right.Value);
        }

        public T ValueOrDefault(T Default)
        {
            if (OnHasValue)
            {
                return HasValue;
            }
            else
            {
                return Default;
            }
        }
    }

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
        public Int32 Id { get; set; }
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
        public Int32 Id { get; set; }
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
