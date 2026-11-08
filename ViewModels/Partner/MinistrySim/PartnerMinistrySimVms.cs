namespace QdratNew.ViewModels.Partner.MinistrySim
{
    // APD-S5.2: صفحات متابعة معمل القياس في منطقة المدرسة الشريكة
    public sealed class PartnerMinistrySimIndexVm
    {
        public List<PartnerMinistrySimExamRowVm> Exams { get; init; } = new();
        public bool ShowHidden { get; init; }
        public int HiddenCount { get; init; }

        // عقد نشط يشمل معمل القياس: يسمح بالإخفاء والإظهار
        public bool CanManage { get; init; }
    }

    public sealed class PartnerMinistrySimExamRowVm
    {
        public int ExamId { get; init; }
        public string Title { get; init; } = "";
        public string CourseName { get; init; } = "";
        public List<PartnerMinistrySimBatchVm> Batches { get; init; } = new();

        // طلاب أُسند لهم فرديًا (لا عبر دفعة)
        public int IndividualCount { get; init; }

        // طلاب المدرسة في الاختبار بقاعدة IMinistrySimOwnershipQuery (دفعة أو فردي)
        public int StudentsCount { get; init; }
        public int CompletedCount { get; init; }
        public DateTime LastAssignedAt { get; init; }
    }

    public sealed class PartnerMinistrySimBatchVm
    {
        public int BatchId { get; init; }
        public string Name { get; init; } = "";
        public bool IsOnline { get; init; }
        public string? ReferenceCode { get; init; }
        public DateTime? VisibleFrom { get; init; }
    }

    public sealed class PartnerMinistrySimStudentsVm
    {
        public int ExamId { get; init; }
        public string Title { get; init; } = "";
        public string CourseName { get; init; } = "";

        // null = كل طلاب المدرسة في هذا الاختبار
        public int? BatchId { get; init; }
        public List<PartnerMinistrySimBatchVm> Batches { get; init; } = new();
        public List<PartnerMinistrySimStudentRowVm> Students { get; init; } = new();

        public int CompletedCount => Students.Count(s => s.IsCompleted);
        public int InProgressCount => Students.Count(s => s.HasAttempt && !s.IsCompleted);
        public int NotStartedCount => Students.Count(s => !s.HasAttempt);
    }

    public sealed class PartnerMinistrySimStudentRowVm
    {
        public int StudentId { get; init; }
        public string FullName { get; init; } = "";
        public string? BatchName { get; init; }
        public bool HasAttempt { get; init; }
        public bool IsCompleted { get; init; }
        public double? ScorePercent { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}
