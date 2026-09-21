using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class HomeworkSetStudent
    {
        public int Id { get; set; }

        public int HomeworkSetId { get; set; }
        public HomeworkSet HomeworkSet { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        // ✅ هل الواجب تم تسليمه فعلاً من الطالب؟
        public bool IsSubmitted { get; set; } = false;

        // ✅ وقت إرسال الواجب للطالب
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        // ✅ وقت تسليم الطالب للواجب
        public DateTime? SubmittedAt { get; set; }

        // ✅ درجة الطالب النهائية في هذا الواجب
        public double? Score { get; set; }

        // ✅ هل تم إرسال إشعار للطالب؟
        public bool NotificationSent { get; set; } = false;

        // ✅ متابعة الطلاب المتأخرين عن حل الواجب
        public bool StudentReminderContacted { get; set; } = false;
        public DateTime? StudentReminderContactedAt { get; set; }

        // ✅ متابعة ولي الأمر في الحالات عالية المخاطرة
        public bool ParentContacted { get; set; } = false;
        public DateTime? ParentContactedAt { get; set; }

        // ✅ آخر تحديث للحالة
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
