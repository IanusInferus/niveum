using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Linq
{
    public partial class LinqDataAccess : IDataAccess
    {
        public List<UserProfile> SelectAllUserProfileOrderById()
        {
            return dbr.UserProfiles.OrderBy(u => u.Id).ToList();
        }

        public int SelectCountUserProfileById(int Id)
        {
            return dbr.UserProfiles.WhereIdIs(Id).Count();
        }

        public UserProfile SelectOneUserProfileById(int Id)
        {
            return dbr.UserProfiles.ById(Id);
        }

        public Optional<UserProfile> SelectOptionalUserProfileByName(string Name)
        {
            var l = dbr.UserProfiles.WhereNameIs(Name).ToArray();
            if (l.Length == 0)
            {
                return null;
            }
            return l.Single();
        }

        public Optional<DirectUserAuthentication> SelectOptionalDirectUserAuthenticationByName(String Name)
        {
            var l = dbr.DirectUserAuthentication.WhereNameIs(Name).ToArray();
            if (l.Length == 0)
            {
                return null;
            }
            return l.Single();
        }

        public Mail SelectOneMailById(int Id)
        {
            return dbr.Mails.ById(Id);
        }

        public List<MailTo> SelectManyMailToById(int Id)
        {
            return dbr.MailTos.WhereIdIs(Id).ToList();
        }

        public MailOwner SelectOneMailOwnerByIdAndOwnerId(int Id, int OwnerId)
        {
            return dbr.MailOwners.ByIdAndOwnerId(Id, OwnerId);
        }

        public int SelectCountMailOwnerById(int Id)
        {
            return dbr.MailOwners.WhereIdIs(Id).Count();
        }

        public int SelectCountMailOwnerByIdAndOwnerId(int Id, int OwnerId)
        {
            return dbr.MailOwners.WhereIdAndOwnerIdIs(Id, OwnerId).Count();
        }

        public List<MailOwner> SelectManyMailOwnerById(int Id)
        {
            return dbr.MailOwners.WhereIdIs(Id).ToList();
        }

        public int SelectCountMailOwnerByOwnerId(int OwnerId)
        {
            return dbr.MailOwners.WhereOwnerIdIs(OwnerId).Count();
        }

        public List<MailOwner> SelectRangeMailOwnerByOwnerIdOrderByOwnerIdAndTimeDesc(int OwnerId, int Skip, int Take)
        {
            return dbr.MailOwners.WhereOwnerIdIs(OwnerId).OrderBy(mo => mo.OwnerId).ThenByDescending(mo => mo.Time).Skip(Skip).Take(Take).ToList();
        }

        public List<String> SelectManyMailAttachmentNameById(int Id)
        {
            return dbr.MailAttachments.WhereIdIs(Id).Select(ma => ma.Name).ToList();
        }

        public List<MailAttachment> SelectManyMailAttachmentById(int Id)
        {
            return dbr.MailAttachments.WhereIdIs(Id).ToList();
        }

        public void InsertOneMail(Mail v)
        {
            dbr.Mails.Add(v);
            dbr.SaveChanges();
        }

        public void InsertManyMailTo(List<MailTo> l)
        {
            foreach (var v in l)
            {
                dbr.MailTos.Add(v);
            }
            dbr.SaveChanges();
        }

        public void InsertManyMailOwner(List<MailOwner> l)
        {
            foreach (var v in l)
            {
                dbr.MailOwners.Add(v);
            }
            dbr.SaveChanges();
        }

        public void InsertManyMailAttachment(List<MailAttachment> l)
        {
            foreach (var v in l)
            {
                dbr.MailAttachments.Add(v);
            }
            dbr.SaveChanges();
        }

        public void UpdateOneMailOwner(MailOwner v)
        {
            dbr.SaveChanges();
        }

        public void DeleteOneMailById(int Id)
        {
            foreach (var Mail in dbr.Mails.Where(m => m.Id == Id))
            {
                dbr.Mails.Remove(Mail);
            }
            dbr.SaveChanges();
        }

        public void DeleteManyMailToById(int Id)
        {
            foreach (var MailTo in dbr.MailTos.Where(m => m.Id == Id))
            {
                dbr.MailTos.Remove(MailTo);
            }
            dbr.SaveChanges();
        }

        public void DeleteOneMailOwnerByIdAndOwnerId(int Id, int OwnerId)
        {
            foreach (var MailOwner in dbr.MailOwners.Where(m => m.Id == Id && m.OwnerId == OwnerId))
            {
                dbr.MailOwners.Remove(MailOwner);
            }
            dbr.SaveChanges();
        }

        public void DeleteManyMailAttachmentById(int Id)
        {
            foreach (var MailAttachment in dbr.MailAttachments.Where(m => m.Id == Id))
            {
                dbr.MailAttachments.Remove(MailAttachment);
            }
            dbr.SaveChanges();
        }
    }
}
