using System;
using System.Collections.Generic;
using System.Linq;
using Database.Entities;
using DB = Database.Database;

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
                return da.FromUserProfileSelectAllOrderById().Select(u => u.Name).ToList();
            }
        }

        public Boolean Login(String Name)
        {
            using (var da = dam.Create())
            {
                var u = da.FromUserProfileSelectOptionalByName(Name);
                if (u == null) { return false; }

                var dua = da.FromDirectUserAuthenticationSelectOptionalByName(u.Some.Name);
                if (dua.OnNone) { return false; }

                UserId = u.Some.Id;
                return true;
            }
        }

        public int GetMailCount()
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                return da.FromMailOwnerSelectCountByOwnerId(UserId);
            }
        }

        public List<MailHeader> GetMailHeaders(int Skip, int Take)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                var l = da.FromMailOwnerSelectRangeByOwnerIdOrderByOwnerIdAndTimeDesc(UserId, Skip, Take);
                var lh = new List<MailHeader>();
                foreach (var mo in l)
                {
                    var m = da.FromMailSelectOneById(mo.Id);
                    var From = da.FromUserProfileSelectOneById(m.FromId);
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
                if (da.FromMailOwnerSelectCountByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var mo = da.FromMailOwnerSelectOneByIdAndOwnerId(MailId, UserId);
                var m = da.FromMailSelectOneById(MailId);
                var From = da.FromUserProfileSelectOneById(m.FromId);
                var Tos = da.FromMailToSelectManyById(m.Id).Select(mt => da.FromUserProfileSelectOneById(mt.ToId)).ToList();
                var Attachments = da.FromMailAttachmentSelectManyForNameById(m.Id).ToList();
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
                var IsReadOnly = da.GetType().Name == "MemoryDataAccess";
                if (!IsReadOnly)
                {
                    mo.IsNew = false;
                    da.FromMailOwnerUpdateOne(mo);
                    da.Complete();
                }
                return v;
            }
        }

        public List<MailAttachment> GetMailAttachments(int MailId)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                if (da.FromMailOwnerSelectCountByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var Attachments = da.FromMailAttachmentSelectManyById(MailId).ToList();
                var l = Attachments.Select(a => new MailAttachment { Name = a.Name, Content = new List<Byte>(a.Content) }).ToList();
                return l;
            }
        }

        public void DeleteMail(int MailId)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                if (da.FromMailOwnerSelectCountByIdAndOwnerId(MailId, UserId) == 0) { throw new InvalidOperationException(); }
                var mo = da.FromMailOwnerSelectOneByIdAndOwnerId(MailId, UserId);
                da.FromMailOwnerDeleteOneByIdAndOwnerId(MailId, UserId);
                if (da.FromMailOwnerSelectCountById(MailId) == 0)
                {
                    da.FromMailAttachmentDeleteManyById(MailId);
                    da.FromMailToDeleteManyById(MailId);
                    da.FromMailDeleteOneById(MailId);
                }
                da.Complete();
            }
        }

        public int? GetUserIdByName(String Name)
        {
            using (var da = dam.Create())
            {
                var u = da.FromUserProfileSelectOptionalByName(Name);
                if (u == null) { return null; }
                return u.Some.Id;
            }
        }

        public void SendMail(MailInput m)
        {
            using (var da = dam.Create())
            {
                if (UserId == -1) { throw new InvalidOperationException(); }
                foreach (var mt in m.ToIds)
                {
                    if (da.FromUserProfileSelectCountById(mt) == 0) { throw new InvalidOperationException(); }
                }
                var Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
                var v = new DB.Mail { Title = m.Title, FromId = UserId, Time = Time, Content = m.Content };
                da.FromMailInsertOne(v);
                var Id = v.Id;
                da.FromMailToInsertMany(m.ToIds.Select(mt => new DB.MailTo { Id = Id, ToId = mt }).ToList());
                da.FromMailOwnerInsertMany(m.ToIds.Select(mt => new DB.MailOwner { Id = Id, OwnerId = mt, IsNew = true, Time = Time }).ToList());
                da.FromMailAttachmentInsertMany(m.Attachments.Select(ma => new DB.MailAttachment { Id = Id, Name = ma.Name, Content = ma.Content }).ToList());
                da.Complete();
            }
        }
    }
}
