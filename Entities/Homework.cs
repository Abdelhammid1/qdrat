using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    public class Homework
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public bool IsCompleted { get; set; } = false;

        public string? StudentAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        // ✅ تاريخ الإنشاء (ثابت تلقائيًا)
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ تاريخ التسليم (يتم تعيينه عند إرسال الواجب)
        public DateTime? SubmittedAt { get; set; }
        public int HomeworkSetId { get; set; }
        public HomeworkSet HomeworkSet { get; set; }

        public int? LessonId { get; set; }
        public Lesson Lesson { get; set; }

        public HomeworkStatus Status { get; set; } = HomeworkStatus.Pending; // ✅ Enum

        public DateTime? StartTime { get; set; }          // وقت بدء السؤال
        public DateTime? AnsweredAt { get; set; }         // وقت الحل
        public int? TimeSpentSeconds { get; set; }        // مدة الإجابة بالثواني

        public bool ViewedVideo { get; set; } = false;    // هل فتح الفيديو

        public bool IsSent { get; set; } = false;

        // ✅ إضافة خاصية الدرجة
        public double? Score { get; set; }

        public int? LectureId { get; set; } // المحاضرة التي صدر منها الواجب
        public Lecture Lecture { get; set; }

    }

}
