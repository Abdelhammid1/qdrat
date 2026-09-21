using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamAssignmentToBatch
    {
        public int Id { get; set; }


        public int? ExamId { get; set; }

        [ForeignKey("ExamId")]
        public virtual Exam Exam { get; set; }


        [Required]
        public int BatchId { get; set; }

        [ForeignKey("BatchId")]
        public virtual Batch Batch { get; set; }

        public int? SectionId { get; set; }
        public Section Section { get; set; }

        public int? CurriculumId { get; set; } // ✅ يجعلها nullable
        public Curriculum Curriculum { get; set; }

        public int? LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;

        public bool IsSentToStudents { get; set; } = false;

        [Required]
        [Display(Name = "عنوان الاختبار")]
        public string Title { get; set; }

        [Display(Name = "عدد الأسئلة")]
        public int TotalQuestions { get; set; } = 10;

        [Display(Name = "عدد الأسئلة السهلة")]
        public int EasyQuestionCount { get; set; } = 3;

        [Display(Name = "عدد الأسئلة المتوسطة")]
        public int MediumQuestionCount { get; set; } = 4;

        [Display(Name = "عدد الأسئلة الصعبة")]
        public int HardQuestionCount { get; set; } = 3;

        [Display(Name = "المدة الزمنية بالدقائق")]
        public int DurationMinutes { get; set; } = 30;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "كلمة المرور لفتح الاختبار")]
        public string? ExamPassword { get; set; }

        public bool IsOnline { get; set; } = true;
        public bool IsInLab { get; set; } = false;
        public bool RequireAttendanceBeforeExam { get; set; } = true;

        [Display(Name = "موعد بدء الاختبار")]
        public DateTime? ScheduledDate { get; set; }

        [Display(Name = "هل تم إرسال إشعار بالموعد؟")]
        public bool IsScheduleNotified { get; set; } = false;

        // ✅ ربط الأسئلة التي تم اختيارها لهذا الاختبار لهذه الدفعة

        [Display(Name = "وقت نهاية الاختبار")]
        public DateTime? EndAt { get; set; }

        public int? CreatedByInstructorId { get; set; } // Nullable في حال أنشأه الأدمن
        public Instructor? CreatedByInstructor { get; set; } // Navigation اختياري

        public List<ExamQuestion> Questions { get; set; } = new();
        public string? ReferenceCode { get; internal set; }
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }
    }
}
