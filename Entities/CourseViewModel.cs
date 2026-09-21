namespace QdratNew.Entities
{
    public class CourseViewModel
    {
        public int CourseID { get; set; }
        public required string Name { get; set; }
        public required string ProjectName { get; set; } // ✅ اسم المشروع المرتبط بالدورة
        public required string BranchName { get; set; } // ✅ اسم الفرع
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public int StudentCount { get; set; } // ✅ عدد الطلاب المسجلين في الدورة
        public int InstructorCount { get; set; } // ✅ عدد المدربين
    }
}
