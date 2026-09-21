namespace QdratNew.ViewModels.Exam
{
    // Sprint 8 (MSE-E / E1): شاشة اختيار دفعة/دفعات لإسناد اختبار معمل القياس
    public class MinistrySimExamAssignBatchPageVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public List<MinistrySimExamBatchOptionVm> Batches { get; set; } = new();
    }

    public class MinistrySimExamBatchOptionVm
    {
        public int BatchId { get; set; }
        public string Name { get; set; }
        public int EnrolledStudentsCount { get; set; }
        public bool AlreadyAssigned { get; set; }
    }

    // Sprint 8 (MSE-E / E2): شاشة اختيار طالب/طلاب محددين لإسناد اختبار معمل القياس
    public class MinistrySimExamAssignStudentPageVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string CourseName { get; set; }
        public List<MinistrySimExamBatchDropdownVm> Batches { get; set; } = new();
    }

    public class MinistrySimExamBatchDropdownVm
    {
        public int BatchId { get; set; }
        public string Name { get; set; }
    }

    // Sprint 8 (MSE-E / E2): يُرجَع عبر AJAX عند اختيار دفعة — قائمة طلابها لعرضهم كـ Checkboxes
    public class MinistrySimExamStudentPickerVm
    {
        public int BatchId { get; set; }
        public List<MinistrySimExamStudentOptionVm> Students { get; set; } = new();
    }

    public class MinistrySimExamStudentOptionVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public bool AlreadyAssigned { get; set; }
        public bool HasAttempt { get; set; }
    }
}
