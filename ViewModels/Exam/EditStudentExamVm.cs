using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class EditStudentExamVm
    {
        public int AssignmentId { get; set; }
        public int ExamId { get; set; }

        public string Title { get; set; }

        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public bool UseProfessionalModel { get; set; }
        public bool UseAutoGeneration { get; set; }

        public int? ProfessionalModelId { get; set; }

        public int? CurriculumId { get; set; }
        public List<EditSectionVm> Sections { get; set; } = new();

        public DateTime ScheduledDate { get; set; }
        public DateTime EndAt { get; set; }
        public int DurationMinutes { get; set; }

        public List<EditQuestionVm> Questions { get; set; } = new();
        public List<ProfessionalModelOptionVm> ProfessionalModels { get; set; } = new();
        public List<CurriculumOptionVm> Curriculums { get; set; } = new();


        public bool IsOnline { get; set; } = true;
        public bool IsInLab { get; set; } = false;

        public string? ReferenceCode { get; set; }

        // Sprint 2 (EB2) — تأكيد إجباري + سبب عند وجود محاولات إجابة سابقة على هذا التكليف
        public bool ConfirmResetAttempts { get; set; } = false;
        public string? EditReason { get; set; }

    }

    public class EditSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int RequestedCount { get; set; }
    }

    public class EditQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string Difficulty { get; set; }
        public int Order { get; set; }
        public int SectionId { get; set; }
    }

    public class ProfessionalModelOptionVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

    public class CurriculumOptionVm
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
