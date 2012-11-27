using System;
using System.Collections.Generic;
using System.Linq;
using Database.Linq;

namespace Database
{
    public interface IDataAccess : IDisposable
    {
        void Complete();

        //{Select, Lock} x {Optional, One, Many, Range, All, Count} x {RecordName} (x {By} x {IndexName})? (x {OrderBy} x {IndexName})?
        //{Insert, Update, Upsert} x {One, Many} x {RecordName}
        //{Delete} x {One, Many, All} x {RecordName} x {By} x {IndexName}

        List<UserProfile> SelectAllUserProfileOrderById();
        int SelectCountUserProfileById(int Id);
        UserProfile SelectOneUserProfileById(int Id);
        Optional<UserProfile> SelectOptionalUserProfileByName(String Name);
        Optional<DirectUserAuthentication> SelectOptionalDirectUserAuthenticationByName(String Name);

        Mail SelectOneMailById(int Id);
        List<MailTo> SelectManyMailToById(int Id);
        MailOwner SelectOneMailOwnerByIdAndOwnerId(int Id, int OwnerId);
        int SelectCountMailOwnerById(int Id);
        int SelectCountMailOwnerByIdAndOwnerId(int Id, int OwnerId);
        List<MailOwner> SelectManyMailOwnerById(int Id);
        int SelectCountMailOwnerByOwnerId(int OwnerId);
        List<MailOwner> SelectRangeMailOwnerByOwnerIdOrderByOwnerIdAndTimeDesc(int OwnerId, int Skip, int Take);
        List<String> SelectManyMailAttachmentNameById(int Id);
        List<MailAttachment> SelectManyMailAttachmentById(int Id);

        void InsertOneMail(Mail v);
        void InsertManyMailTo(List<MailTo> l);
        void InsertManyMailOwner(List<MailOwner> l);
        void InsertManyMailAttachment(List<MailAttachment> l);
        void UpdateOneMailOwner(MailOwner v);
        void DeleteOneMailById(int Id);
        void DeleteManyMailToById(int Id);
        void DeleteOneMailOwnerByIdAndOwnerId(int Id, int OwnerId);
        void DeleteManyMailAttachmentById(int Id);
    }
}
