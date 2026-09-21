using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public enum AchievementType
    {
        TestPassed,      // ✅ اجتياز اختبار
        ExercisesCompleted, // ✅ إكمال جميع التمارين
        TopPerformance,  // ✅ أفضل أداء في التحديات
        AttendanceStreak, // ✅ حضور مستمر
        Custom          // ✅ نوع مخصص
    }

    public class StudentAchievement
    {
        [Key]
        public int Id { get; set; }

        // ✅ الطالب المرتبط بهذا الإنجاز
        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public required Student Student { get; set; }

        // ✅ الدورة التي تم تحقيق الإنجاز فيها
        [ForeignKey("Course")]
        public int CourseID { get; set; }
        public required Course Course { get; set; }

        // ✅ نوع الإنجاز باستخدام `enum`
        public AchievementType Type { get; set; } = AchievementType.Custom;

        // ✅ تاريخ تحقيق الإنجاز
        public DateTime DateAchieved { get; set; } = DateTime.UtcNow;

        // ✅ وصف الإنجاز
        public required string Description { get; set; }

        // ✅ عدد النقاط المكتسبة عند تحقيق الإنجاز
        public int PointsAwarded { get; set; } = 0;

        // ✅ تحديد ما إذا كان الطالب حصل على شارة لهذا الإنجاز
        public required string Badge { get; set; } // ✅ يجبر المطور على تعيين قيمة عند الإنشاء

        // ✅ هل تم اعتماد الإنجاز من قبل الإدارة؟
        public bool IsApproved { get; set; } = true;
    }
}
