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

        public UserProfile SelectOptionalUserProfileByName(string Name)
        {
            var l = dbr.UserProfiles.WhereNameIs(Name).ToArray();
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

        public int InsertOneMail(Mail v)
        {
            dbr.Mails.InsertOnSubmit(v);
            dbr.SubmitChanges();
            return v.Id;
        }

        public void InsertManyMailTo(List<MailTo> l)
        {
            dbr.MailTos.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void InsertManyMailOwner(List<MailOwner> l)
        {
            dbr.MailOwners.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void InsertManyMailAttachment(List<MailAttachment> l)
        {
            dbr.MailAttachments.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void UpdateOneMailOwner(MailOwner v)
        {
            dbr.SubmitChanges();
        }

        public void DeleteOneMailById(int Id)
        {
            dbr.Mails.DeleteAllOnSubmit(dbr.Mails.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }

        public void DeleteManyMailToById(int Id)
        {
            dbr.MailTos.DeleteAllOnSubmit(dbr.MailTos.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }

        public void DeleteOneMailOwnerByIdAndOwnerId(int Id, int OwnerId)
        {
            dbr.MailOwners.DeleteAllOnSubmit(dbr.MailOwners.Where(m => m.Id == Id && m.OwnerId == OwnerId));
            dbr.SubmitChanges();
        }

        public void DeleteManyMailAttachmentById(int Id)
        {
            dbr.MailAttachments.DeleteAllOnSubmit(dbr.MailAttachments.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }
    }
}
