using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Student
    {
        [Key]
        public int StudentID { get; set; }

        [Required, StringLength(14, MinimumLength = 10)]
        public string NationalID { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        [Required]
        public string Gender { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Range(3, 100)]
        public int Age { get; set; }

        public string School { get; set; }
        public string? Level { get; set; }

        public int? ParentId { get; set; }
        public Parent Parent { get; set; }

        public string? UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        public ICollection<StudentCourseEnrollment> StudentCourseEnrollments { get; set; } = new List<StudentCourseEnrollment>();
        public virtual ICollection<StudentPerformance> StudentPerformances { get; set; } = new List<StudentPerformance>();

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public string EnrollmentStatus { get; set; } = "نشط";

        public ICollection<ChallengeParticipation> ChallengeParticipations { get; set; } = new List<ChallengeParticipation>();
        public ICollection<RemedialPlan> RemedialPlans { get; set; } = new List<RemedialPlan>();
        public ICollection<StudentAchievement> StudentAchievements { get; set; } = new List<StudentAchievement>();

        public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
        public ICollection<StudentProgress> StudentProgressRecords { get; set; } = new List<StudentProgress>();
        public ICollection<StudyPlan> StudyPlans { get; set; } = new List<StudyPlan>();

        public string? ProfileImagePath { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsRegular { get; set; } = true;

        // =========================
        // 🔹 حالة التفعيل التعليمي
        // =========================
        /// <summary>
        /// يحدد ما إذا كان الطالب مفعّل تعليميًا في الاشتراك الحالي
        /// </summary>
        public bool IsActiveForLearning { get; set; } = true;

        // =========================
        // 🔹 سلب/منح الدخول لاريا الطالب (مستقل عن IsActive العام)
        // =========================
        /// <summary>
        /// يتحكم به المالك أو المبرمج فقط، مرتبط بسياق أرشفة الدفعات
        /// </summary>
        public bool CanAccessStudentArea { get; set; } = true;
        public DateTime? AccessRevokedAt { get; set; }
        public string? AccessRevokedByUserId { get; set; }


        [InverseProperty(nameof(StudentBatchEnrollment.Student))]
        public ICollection<StudentBatchEnrollment> BatchEnrollments { get; set; }
      = new List<StudentBatchEnrollment>();



        public int? PartnerSubscriptionPeriodId { get; set; }

        [ForeignKey(nameof(PartnerSubscriptionPeriodId))]
        public PartnerSubscriptionPeriod PartnerSubscriptionPeriod { get; set; }

    }
}
