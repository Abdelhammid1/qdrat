namespace QdratNew.ViewModels.QuestionReviewTasks
{
    /// <summary>فلاتر تقرير أداء المراجعين (QRT-S7.3): الفترة تُحتسب على تاريخ إنشاء المهمة، بتوقيت العرض.</summary>
    public sealed class ReviewersReportFilter
    {
        /// <summary>بداية الفترة (تاريخ محلي). الافتراضي: قبل 90 يومًا.</summary>
        public DateTime? From { get; init; }

        /// <summary>نهاية الفترة شاملة (تاريخ محلي). الافتراضي: اليوم.</summary>
        public DateTime? To { get; init; }

        public int? InstructorId { get; init; }
    }

    public sealed class ReviewerPerformanceRowVm
    {
        public int InstructorId { get; init; }
        public string InstructorName { get; init; } = string.Empty;
        public int AssignedTasks { get; init; }
        public int CompletedTasks { get; init; }
        /// <summary>مهام نشطة فات موعدها الآن + مهام اكتملت بعد موعدها.</summary>
        public int LateTasks { get; init; }
        /// <summary>أسئلة اعتمدها المدرب نفسه (اعتماد مباشر أو بعد تعديل) — دون اعتماد الإدارة.</summary>
        public int ApprovedQuestions { get; init; }
        public int ReturnedQuestions { get; init; }
        public int HandledQuestions { get; init; }
        public int PendingQuestions { get; init; }
        /// <summary>المُرجَع ÷ ما تصرّف فيه المدرب.</summary>
        public int ReturnRatePercent { get; init; }
        /// <summary>متوسط (اكتمال − إنشاء) للمهام المكتملة بالساعات؛ null إن لم تكتمل أي مهمة.</summary>
        public double? AvgCompletionHours { get; init; }
    }

    public sealed class ReviewersReportVm
    {
        public string FromLocal { get; init; } = string.Empty;   // yyyy-MM-dd
        public string ToLocal { get; init; } = string.Empty;     // yyyy-MM-dd
        public int? InstructorId { get; init; }
        public List<AdminFilterOptionVm> Instructors { get; init; } = new();
        public List<ReviewerPerformanceRowVm> Rows { get; init; } = new();
        public int TotalAssigned { get; init; }
        public int TotalApproved { get; init; }
        public int TotalReturned { get; init; }
    }
}
