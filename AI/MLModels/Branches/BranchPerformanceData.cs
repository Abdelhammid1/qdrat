using Microsoft.ML.Data;

namespace QdratNew.AI.MLModels.Branches
{
    public class BranchPerformanceData
    {
        public float Score { get; set; }              // القيمة الحقيقية
        public float EngagementRate { get; set; }     // معدل التفاعل
        public float AttendanceCount { get; set; }    // عدد الحضور
        public float StudyHours { get; set; }         // عدد ساعات الدراسة
    }
}
