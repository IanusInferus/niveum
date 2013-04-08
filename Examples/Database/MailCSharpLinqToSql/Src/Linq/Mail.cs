using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Linq
{
    public partial class LinqDataAccess : IDataAccess
    {
        public List<UserProfile> FromUserProfileSelectAllOrderById()
        {
            return dbr.UserProfiles.OrderBy(u => u.Id).ToList();
        }

        public int FromUserProfileSelectCountById(int Id)
        {
            return dbr.UserProfiles.WhereIdIs(Id).Count();
        }

        public UserProfile FromUserProfileSelectOneById(int Id)
        {
            return dbr.UserProfiles.ById(Id);
        }

        public Optional<UserProfile> FromUserProfileSelectOptionalByName(string Name)
        {
            var l = dbr.UserProfiles.WhereNameIs(Name).ToArray();
            if (l.Length == 0)
            {
                return null;
            }
            return l.Single();
        }

        public Optional<DirectUserAuthentication> FromDirectUserAuthenticationSelectOptionalByName(String Name)
        {
            var l = dbr.DirectUserAuthentication.WhereNameIs(Name).ToArray();
            if (l.Length == 0)
            {
                return null;
            }
            return l.Single();
        }

        public Mail FromMailSelectOneById(int Id)
        {
            return dbr.Mails.ById(Id);
        }

        public List<MailTo> FromMailToSelectManyById(int Id)
        {
            return dbr.MailTos.WhereIdIs(Id).ToList();
        }

        public MailOwner FromMailOwnerSelectOneByIdAndOwnerId(int Id, int OwnerId)
        {
            return dbr.MailOwners.ByIdAndOwnerId(Id, OwnerId);
        }

        public int FromMailOwnerSelectCountById(int Id)
        {
            return dbr.MailOwners.WhereIdIs(Id).Count();
        }

        public int FromMailOwnerSelectCountByIdAndOwnerId(int Id, int OwnerId)
        {
            return dbr.MailOwners.WhereIdAndOwnerIdIs(Id, OwnerId).Count();
        }

        public List<MailOwner> FromMailOwnerSelectManyById(int Id)
        {
            return dbr.MailOwners.WhereIdIs(Id).ToList();
        }

        public int FromMailOwnerSelectCountByOwnerId(int OwnerId)
        {
            return dbr.MailOwners.WhereOwnerIdIs(OwnerId).Count();
        }

        public List<MailOwner> FromMailOwnerSelectRangeByOwnerIdOrderByOwnerIdAndTimeDesc(int OwnerId, int Skip, int Take)
        {
            return dbr.MailOwners.WhereOwnerIdIs(OwnerId).OrderBy(mo => mo.OwnerId).ThenByDescending(mo => mo.Time).Skip(Skip).Take(Take).ToList();
        }

        public List<String> FromMailAttachmentSelectManyForNameById(int Id)
        {
            return dbr.MailAttachments.WhereIdIs(Id).Select(ma => ma.Name).ToList();
        }

        public List<MailAttachment> FromMailAttachmentSelectManyById(int Id)
        {
            return dbr.MailAttachments.WhereIdIs(Id).ToList();
        }

        public void FromMailInsertOne(Mail v)
        {
            dbr.Mails.InsertOnSubmit(v);
            dbr.SubmitChanges();
        }

        public void FromMailToInsertMany(List<MailTo> l)
        {
            dbr.MailTos.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void FromMailOwnerInsertMany(List<MailOwner> l)
        {
            dbr.MailOwners.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void FromMailAttachmentInsertMany(List<MailAttachment> l)
        {
            dbr.MailAttachments.InsertAllOnSubmit(l);
            dbr.SubmitChanges();
        }

        public void FromMailOwnerUpdateOne(MailOwner v)
        {
            dbr.SubmitChanges();
        }

        public void FromMailDeleteOneById(int Id)
        {
            dbr.Mails.DeleteAllOnSubmit(dbr.Mails.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }

        public void FromMailDeleteManyToById(int Id)
        {
            dbr.MailTos.DeleteAllOnSubmit(dbr.MailTos.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }

        public void FromMailOwnerDeleteOneByIdAndOwnerId(int Id, int OwnerId)
        {
            dbr.MailOwners.DeleteAllOnSubmit(dbr.MailOwners.Where(m => m.Id == Id && m.OwnerId == OwnerId));
            dbr.SubmitChanges();
        }

        public void FromMailAttachmentDeleteManyById(int Id)
        {
            dbr.MailAttachments.DeleteAllOnSubmit(dbr.MailAttachments.Where(m => m.Id == Id));
            dbr.SubmitChanges();
        }
    }
}
