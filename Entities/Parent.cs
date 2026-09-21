using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Entities; // ✅ استيراد `ApplicationUser`

namespace QdratNew.Entities
{
    public class Parent
    {
        public int ParentID { get; set; }
        public string NationalID { get; set; }

        [Required]
        public string FullName { get; set; }

        [EmailAddress]
        public string  Email { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        // ✅ تحويل العلاقة إلى `Enum` بدلاً من النص الحر
        public ParentRelation RelationToStudent { get; set; }

        // ✅ ربط ولي الأمر بحساب `AspNetUsers`
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        // ✅ العلاقة مع الطلاب (ولي الأمر يمكنه الإشراف على عدة طلاب)
        public ICollection<Student> Students { get; set; } = new List<Student>();

        // ✅ تسجيل تاريخ إنشاء الحساب
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Phone]
        public string? WhatsAppNumber { get; set; }  // رقم الواتساب

    }

    // ✅ تعريف `Enum` لعلاقة ولي الأمر بالطالب
    public enum ParentRelation
    {
        أب,
        أم,
        أخو,
        أخرى
    }
}
