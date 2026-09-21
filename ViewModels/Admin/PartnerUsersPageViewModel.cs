namespace QdratNew.ViewModels.Admin
{
    public class PartnerUsersPageViewModel
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public string? LogoPath { get; set; }
        public bool IsActive { get; set; }
        public int? ActiveSubscriptionPeriodId { get; set; }

        public List<PartnerUserRowDto> Students { get; set; } = new();
        public List<PartnerUserRowDto> Instructors { get; set; } = new();
        public List<PartnerUserRowDto> Admins { get; set; } = new();

        // ── Homework ──────────────────────────────────────
        public List<PartnerAdminHomeworkRow> HomeworkSets { get; set; } = new();
        public List<PartnerAdminHomeworkDraftRow> HomeworkDrafts { get; set; } = new();

        // ── Exams ─────────────────────────────────────────
        public List<PartnerAdminExamRow> ExamAssignments { get; set; } = new();
        public List<PartnerAdminExamDraftRow> ExamDrafts { get; set; } = new();

        // ── Partner infrastructure ─────────────────────────
        public List<PartnerAdminBranchRow> Branches { get; set; } = new();
        public List<PartnerAdminBatchRow> Batches { get; set; } = new();
    }

    public class PartnerUserRowDto
    {
        public string? UserId { get; set; }
        public int? StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? Email { get; set; }
        public string? NationalID { get; set; }
        public string? Phone { get; set; }
        public string? School { get; set; }
        public string? Level { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    // ── Homework rows ─────────────────────────────────────
    public class PartnerAdminHomeworkRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateTime SentAt { get; set; }
        public int TotalStudents { get; set; }
        public int SubmittedCount { get; set; }
    }

    public class PartnerAdminHomeworkDraftRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int QuestionsCount { get; set; }
    }

    // ── Exam rows ─────────────────────────────────────────
    public class PartnerAdminExamRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateTime AssignedAt { get; set; }
        public int TotalStudents { get; set; }
        public int AttemptedCount { get; set; }
    }

    public class PartnerAdminExamDraftRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int QuestionsCount { get; set; }
    }

    // ── Infrastructure rows ───────────────────────────────
    public class PartnerAdminBranchRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class PartnerAdminBatchRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int BranchId { get; set; }
        public bool IsActive { get; set; }
    }

    // ── Homework report ───────────────────────────────────
    public class PartnerAdminHomeworkReportVM
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; } = "";
        public string BatchName { get; set; } = "";
        public DateTime SentAt { get; set; }
        public List<PartnerAdminHomeworkStudentRow> Students { get; set; } = new();
    }

    public class PartnerAdminHomeworkStudentRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? NationalID { get; set; }
        public bool IsSubmitted { get; set; }
        public double? Score { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    // ── Exam report ───────────────────────────────────────
    public class PartnerAdminExamReportVM
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public int AssignmentId { get; set; }
        public string ExamTitle { get; set; } = "";
        public string BatchName { get; set; } = "";
        public DateTime AssignedAt { get; set; }
        public List<PartnerAdminExamStudentRow> Students { get; set; } = new();
    }

    public class PartnerAdminExamStudentRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? NationalID { get; set; }
        public bool IsSubmitted { get; set; }
        public double? Score { get; set; }
        public string Status { get; set; } = "لم يبدأ";
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    // ── Per-student exam detail (admin) ───────────────────
    public class PartnerAdminExamDetailVM
    {
        public int AssignmentId { get; set; }
        public int StudentId { get; set; }
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string BatchName { get; set; } = "";
        public string ExamTitle { get; set; } = "";

        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double ScorePercentage { get; set; }
        public double TimeSpentMinutes { get; set; }

        public List<ExamDetailQuestionVm> Questions { get; set; } = new();
        public List<ExamDetailSectionVm> SectionsPerformance { get; set; } = new();
        public List<ExamDetailLessonVm> LessonsPerformance { get; set; } = new();
        public QdratNew.ViewModels.Reports.ExamRecommendationVm? Recommendation { get; set; }
    }

    public class ExamDetailQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string? QuestionTitle { get; set; }
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public double TimeTakenSeconds { get; set; }
    }

    public class ExamDetailSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public double Accuracy { get; set; }
    }

    public class ExamDetailLessonVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double SuccessRate { get; set; }
        public double TimeSpentMinutes { get; set; }
    }
}
