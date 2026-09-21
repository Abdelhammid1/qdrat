using System;

namespace QdratNew.ViewModels.Admin
{
    public class ArchivedStudentRowDto
    {
        public int StudentId { get; set; }
        public string? UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? NationalID { get; set; }
        public string? Phone { get; set; }
        public string? School { get; set; }
        public string? Level { get; set; }
        public string? EnrollmentStatus { get; set; }
        public DateTime EnrolledAt { get; set; }
        // true = الطالب لديه تسجيل في دفعة نشطة أخرى (سيظهر في الرئيسي)
        public bool HasActiveEnrollment { get; set; }
    }
}
