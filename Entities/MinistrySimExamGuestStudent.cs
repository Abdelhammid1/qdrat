using System;
using System.Collections.Generic;

namespace QdratNew.Entities
{
    // طبقة "طالب ضيف/زائر" مستقلة تمامًا عن جدول Students — لمن لا ينتمي لأي دورة/دفعة في المعهد
    // ويشترك فقط في اختبارات محاكاة الوزارة. لا يوجد ربط بـ ApplicationUser/تسجيل دخول بعد — هذا الكيان
    // يغطي فقط طبقة البيانات (الهوية + الإسناد)؛ تفعيل دخول الطالب الضيف الفعلي لحل الاختبار مهمة منفصلة لاحقًا.
    public class MinistrySimExamGuestStudent
    {
        public int Id { get; set; }

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<MinistrySimExamAssignmentToGuest> Assignments { get; set; } = new List<MinistrySimExamAssignmentToGuest>();
    }
}
