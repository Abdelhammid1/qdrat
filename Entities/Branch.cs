using System;
using System.Collections.Generic;
using System.Linq;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class Branch
    {
        public int Id { get; set; }  // 🔹 المفتاح الأساسي
        public required string Name { get; set; }  // 🔹 اسم الفرع
        public required string Location { get; set; }  // 🔹 العنوان
        public required string City { get; set; }  // 🔹 المدينة
        public required string State { get; set; }  // 🔹 المحافظة
        public required string Country { get; set; }  // 🔹 الدولة
        public bool IsPartner { get; set; }  // 🔹 نوع الفرع (شريك أو متعاون)
        public DateTime EstablishedDate { get; set; }  // 🔹 تاريخ إنشاء الفرع
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; } = false;

        // ✅ تحسين حساب متوسط أداء الطلاب مع معالجة `null`
        public double AveragePerformance => Students?.Any() == true
            ? Students.Average(s => s.StudentPerformances?.Any() == true
                ? s.StudentPerformances.Average(sp => sp.Score)
                : 0)
            : 0;

        public int? PartnerId { get; set; }
        public Partner? Partner { get; set; }





        // 🔹 علاقة `One-to-Many` مع `Project` (المشاريع)
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

        // 🔹 علاقة `One-to-Many` مع `Student` (الطلاب)
        public virtual ICollection<QdratNew.Entities.Student> Students { get; set; } = new List<QdratNew.Entities.Student>();

        // 🔹 علاقة `One-to-Many` مع `Course` (الدورات)
        public virtual ICollection<Course> Courses { get; set; } = new List<Course>();
    }
}
