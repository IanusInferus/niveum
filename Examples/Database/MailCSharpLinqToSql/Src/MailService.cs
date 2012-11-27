using System;
using System.Collections.Generic;
using System.Linq;
using Database.Entities;
using DB = Database.Linq;

namespace Database
{
    public class MailService
    {
        private DataAccessManager dam;
        public MailService(DataAccessManager dam)
        {
            this.dam = dam;
        }

        private int UserId = -1;

        public List<String> GetUsers()
        {
            using (var da = dam.Create())
            {
                return da.SelectAllUserProfileOrderById().Select(u => u.Name).ToList();
            }
        }

        public Boolean Login(String Name)
        {
            using (var da = dam.Create())
            {
                var u = da.SelectOptionalUserProfileByName(Name);
                if (u == null) { return false; }

                var dua = da.SelectOptionalDirectUserAuthenticationByName(u.HasValue.Name);
                if (dua.OnNotHasValue) { return false; }

                UserId = u.HasValue.Id;
                return true;
            }
        }

        public int GetMailCount()
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                return da.SelectCountMailOwnerByOwnerId(UserId);
            }
        }

        public List<MailHeader> GetMailHeaders(int Skip, int Take)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                var l = da.SelectRangeMailOwnerByOwnerIdOrderByOwnerIdAndTimeDesc(UserId, Skip, Take);
                var lh = new List<MailHeader>();
                foreach (var mo in l)
                {
                    var m = da.SelectOneMailById(mo.Id);
                    var From = da.SelectOneUserProfileById(m.FromId);
                    lh.Add
                    (
                        new MailHeader
                        {
                            Id = m.Id,
                            Title = m.Title,
                            From = new UserProfile { Id = From.Id, Name = From.Name, EmailAddress = From.EmailAddress, Gender = (Gender)(From.Gender) },
                            IsNew = mo.IsNew,
                            Time = m.Time
                        }
                    );
                }
                return lh;
            }
        }

        public MailDetail GetMail(int MailId)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                if (da.SelectCountMailOwnerByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var mo = da.SelectOneMailOwnerByIdAndOwnerId(MailId, UserId);
                var m = da.SelectOneMailById(MailId);
                var From = da.SelectOneUserProfileById(m.FromId);
                var Tos = da.SelectManyMailToById(m.Id).Select(mt => da.SelectOneUserProfileById(mt.ToId)).ToList();
                var Attachments = da.SelectManyMailAttachmentNameById(m.Id).ToList();
                var v = new MailDetail
                {
                    Id = m.Id,
                    Title = m.Title,
                    From = new UserProfile { Id = From.Id, Name = From.Name, EmailAddress = From.EmailAddress, Gender = (Gender)(From.Gender) },
                    Tos = Tos.Select(t => new UserProfile { Id = t.Id, Name = t.Name, EmailAddress = t.EmailAddress, Gender = (Gender)(From.Gender) }).ToList(),
                    Time = m.Time,
                    Content = m.Content,
                    Attachments = Attachments
                };
                mo.IsNew = false;
                da.UpdateOneMailOwner(mo);
                da.Complete();
                return v;
            }
        }

        public List<MailAttachment> GetMailAttachments(int MailId)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                if (da.SelectCountMailOwnerByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var Attachments = da.SelectManyMailAttachmentById(MailId).ToList();
                var l = Attachments.Select(a => new MailAttachment { Name = a.Name, Content = new List<Byte>(a.Content) }).ToList();
                return l;
            }
        }

        public void DeleteMail(int MailId)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                if (da.SelectCountMailOwnerByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var mo = da.SelectOneMailOwnerByIdAndOwnerId(MailId, UserId);
                da.DeleteOneMailOwnerByIdAndOwnerId(MailId, UserId);
                if (da.SelectCountMailOwnerById(MailId) == 0)
                {
                    da.DeleteManyMailAttachmentById(MailId);
                    da.DeleteManyMailToById(MailId);
                    da.DeleteOneMailById(MailId);
                }
                da.Complete();
            }
        }

        public int? GetUserIdByName(String Name)
        {
            using (var da = dam.Create())
            {
                var u = da.SelectOptionalUserProfileByName(Name);
                if (u == null) { return null; }
                return u.HasValue.Id;
            }
        }

        public void SendMail(MailInput m)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                foreach (var mt in m.ToIds)
                {
                    if (da.SelectCountUserProfileById(mt) == 0) { throw new InvalidOperationException(); }
                }
                var Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
                var v = new DB.Mail { Title = m.Title, FromId = UserId, Time = Time, Content = m.Content };
                da.InsertOneMail(v);
                var Id = v.Id;
                da.InsertManyMailTo(m.ToIds.Select(mt => new DB.MailTo { Id = Id, ToId = mt }).ToList());
                da.InsertManyMailOwner(m.ToIds.Select(mt => new DB.MailOwner { Id = Id, OwnerId = mt, IsNew = true, Time = Time }).ToList());
                da.InsertManyMailAttachment(m.Attachments.Select(ma => new DB.MailAttachment { Id = Id, Name = ma.Name, Content = ma.Content.ToArray() }).ToList());
                da.Complete();
            }
        }
    }
}
