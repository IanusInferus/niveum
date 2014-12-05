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

        Mail FromMailSelectOneById(Int64 Id);
        List<MailTo> FromMailToSelectManyById(Int64 Id);
        MailOwner FromMailOwnerSelectOneByIdAndOwnerId(Int64 Id, int OwnerId);
        int FromMailOwnerSelectCountById(Int64 Id);
        int FromMailOwnerSelectCountByIdAndOwnerId(Int64 Id, int OwnerId);
        List<MailOwner> FromMailOwnerSelectManyById(Int64 Id);
        int FromMailOwnerSelectCountByOwnerId(int OwnerId);
        List<MailOwner> FromMailOwnerSelectRangeByOwnerIdOrderByOwnerIdAndTimeDesc(int OwnerId, int Skip, int Take);
        List<String> FromMailAttachmentSelectManyForNameById(Int64 Id);
        List<MailAttachment> FromMailAttachmentSelectManyById(Int64 Id);

        void FromMailInsertOne(Mail v);
        void FromMailToInsertMany(List<MailTo> l);
        void FromMailOwnerInsertMany(List<MailOwner> l);
        void FromMailAttachmentInsertMany(List<MailAttachment> l);
        void FromMailOwnerUpdateOne(MailOwner v);
        void FromMailDeleteOneById(Int64 Id);
        void FromMailDeleteManyToById(Int64 Id);
        void FromMailOwnerDeleteOneByIdAndOwnerId(Int64 Id, int OwnerId);
        void FromMailAttachmentDeleteManyById(Int64 Id);
    }
}
