using System;
using System.Collections.Generic;
using System.Linq;
using Database.Linq;

namespace Database
{
    public interface IDataAccess : IDisposable
    {
        void Complete();

        // {From} x {RecordName} x {Select, Lock} x {Optional, One, Many, Range, All, Count} (x {By} x {IndexName})? (x {OrderBy} x {IndexName})?
        // {From} x {RecordName} x {Insert, Update, Upsert} x {One, Many}
        // {From} x {RecordName} x {Delete} x {One, Many, All} x {By} x {IndexName}

        List<UserProfile> FromUserProfileSelectAllOrderById();
        int FromUserProfileSelectCountById(int Id);
        UserProfile FromUserProfileSelectOneById(int Id);
        Optional<UserProfile> FromUserProfileSelectOptionalByName(String Name);
        Optional<DirectUserAuthentication> FromDirectUserAuthenticationSelectOptionalByName(String Name);

        Mail FromMailSelectOneById(int Id);
        List<MailTo> FromMailToSelectManyById(int Id);
        MailOwner FromMailOwnerSelectOneByIdAndOwnerId(int Id, int OwnerId);
        int FromMailOwnerSelectCountById(int Id);
        int FromMailOwnerSelectCountByIdAndOwnerId(int Id, int OwnerId);
        List<MailOwner> FromMailOwnerSelectManyById(int Id);
        int FromMailOwnerSelectCountByOwnerId(int OwnerId);
        List<MailOwner> FromMailOwnerSelectRangeByOwnerIdOrderByOwnerIdAndTimeDesc(int OwnerId, int Skip, int Take);
        List<String> FromMailAttachmentSelectManyForNameById(int Id);
        List<MailAttachment> FromMailAttachmentSelectManyById(int Id);

        void FromMailInsertOne(Mail v);
        void FromMailToInsertMany(List<MailTo> l);
        void FromMailOwnerInsertMany(List<MailOwner> l);
        void FromMailAttachmentInsertMany(List<MailAttachment> l);
        void FromMailOwnerUpdateOne(MailOwner v);
        void FromMailDeleteOneById(int Id);
        void FromMailDeleteManyToById(int Id);
        void FromMailOwnerDeleteOneByIdAndOwnerId(int Id, int OwnerId);
        void FromMailAttachmentDeleteManyById(int Id);
    }
}
