using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    // شاشة تصفّح إسنادات اختبار معمل القياس — 3 تابات: دفعات / طلاب المعهد (إسناد فردي) / طلاب ضيوف
    public class MinistrySimExamAssignmentsOverviewVm
    {
        public List<MinistrySimExamBatchCardVm> Batches { get; set; } = new();
        public List<MinistrySimExamInstituteStudentRowVm> InstituteStudents { get; set; } = new();
        public List<MinistrySimExamGuestCardVm> Guests { get; set; } = new();
    }

    public class MinistrySimExamBatchCardVm
    {
        public int BatchId { get; set; }
        public string Name { get; set; }
        public string CourseName { get; set; }
        public int EnrolledStudentsCount { get; set; }
        public int AssignedExamsCount { get; set; }
    }

    public class MinistrySimExamInstituteStudentRowVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public List<string> AssignedExamTitles { get; set; } = new();
    }

    public class MinistrySimExamGuestCardVm
    {
        public int GuestId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public List<string> AssignedExamTitles { get; set; } = new();
    }

    // شاشة تفاصيل دفعة — الاختبارات المُسنَدة لها حاليًا + نموذج إسناد اختبار جديد من نفس دورة الدفعة
    public class MinistrySimExamBatchDetailsVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public List<MinistrySimExamAssignedExamRowVm> AssignedExams { get; set; } = new();
        public List<SelectListItem> AssignableExams { get; set; } = new();
    }

    // شاشة تفاصيل ضيف — الاختبارات المُسنَدة له حاليًا + نموذج إسناد اختبار جديد (من كل الاختبارات المنشورة)
    public class MinistrySimExamGuestDetailsVm
    {
        public int GuestId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public List<MinistrySimExamAssignedExamRowVm> AssignedExams { get; set; } = new();
        public List<SelectListItem> AssignableExams { get; set; } = new();
    }

    public class MinistrySimExamAssignedExamRowVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
