using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Instructor.InterventionTasks
{
    // ── CompleteWithHomework page ──────────────────────────────────
    public class CompleteWithHomeworkVM
    {
        public int TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public string OwnerInstructions { get; set; } = string.Empty;
        public string SuggestedHomeworkTitle { get; set; } = string.Empty;
        public int? LectureId { get; set; }
        public DateTime HomeworkEndAt { get; set; }
        public List<TaskQuestionItem> Questions { get; set; } = new();
        public List<TaskLessonItem> Lessons { get; set; } = new();
        public List<BatchStudentItem> Students { get; set; } = new();
        public bool HasCarriedSelection { get; set; }
        public List<string> SelectedHomeworkQuestionIds { get; set; } = new();
        public List<string> SelectedLectureQuestionIds { get; set; } = new();
        public List<int> SelectedHomeworkLessonIds { get; set; } = new();
        public List<int> SelectedLectureLessonIds { get; set; } = new();
    }

    public class TaskQuestionItem
    {
        public string QuestionId { get; set; } = string.Empty;
        public string QuestionTitle { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public double ErrorPercentage { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public int AffectedStudentsCount { get; set; }
    }

    public class TaskLessonItem
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string SectionTitle { get; set; } = string.Empty;
        public double ErrorPercentage { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public int AffectedStudentsCount { get; set; }
        public int RelatedQuestionsCount { get; set; }
    }

    public class BatchStudentItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class SubmitCompletionInputVM
    {
        public int TaskId { get; set; }
        public string HomeworkTitle { get; set; } = string.Empty;
        public string? ExecutionNotes { get; set; }
        public List<string> HomeworkQuestionIds { get; set; } = new();
        public List<string> LectureQuestionIds { get; set; } = new();
        public List<int> LectureLessonIds { get; set; } = new();
        public List<int> HomeworkLessonIds { get; set; } = new();
        public string SendTo { get; set; } = "Batch";
        public List<int> SelectedStudentIds { get; set; } = new();
        public DateTime HomeworkEndAt { get; set; }
    }


    public class InstructorTaskListItemVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InstructorTaskDetailsVM
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string DeliveryMode { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OwnerInstructions { get; set; } = string.Empty;
        public string? InstructorExecutionNotes { get; set; }
        public string? TargetLessonsJson { get; set; }
        public string? TargetQuestionsJson { get; set; }
        public string? TargetStudentsJson { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string BatchName { get; set; } = string.Empty;
        public string? CurriculumTitle { get; set; }
        public string? LectureTitle { get; set; }
    }
}
