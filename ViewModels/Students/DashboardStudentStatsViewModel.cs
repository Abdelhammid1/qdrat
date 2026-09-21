using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Students
{
    public class DashboardStudentStatsViewModel
    {
        // 🔢 الإحصائيات العامة
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }

        // 🧑‍🤝‍🧑 الجنس
        public int MaleStudents { get; set; }
        public int FemaleStudents { get; set; }

        public int? SelectedBranchId { get; set; }
        public int? SelectedBatchId { get; set; }

        public List<SelectListItem> Branches { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();


        // 🏫 الفروع
        public Dictionary<string, int> StudentsByBranch { get; set; } = new();

        // 🎓 المرحلة التعليمية
        public Dictionary<string, int> StudentsByLevel { get; set; } = new();

        // 📈 التسجيل الشهري
        public Dictionary<string, int> RegistrationsByMonth { get; set; } = new();

        // ⚠️ الطلاب المتعثرين (ضعيف الأداء أو بدون خطة)
        public List<string> AtRiskStudents { get; set; } = new();
    }
}
