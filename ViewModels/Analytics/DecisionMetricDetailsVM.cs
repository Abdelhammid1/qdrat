namespace QdratNew.ViewModels.Admin.Analytics
{
    public class DecisionMetricDetailsVM
    {
        // ─── هوية المؤشر ────────────────────────────────────────────────────
        public string MetricKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// primary | info | warning | danger | success | violet
        /// </summary>
        public string Theme { get; set; } = "primary";

        // ─── القيمة الرئيسية ────────────────────────────────────────────────
        public string Value { get; set; } = "0";
        public string Unit { get; set; } = string.Empty;

        // ─── حالة المؤشر ────────────────────────────────────────────────────
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// warning | danger | success
        /// </summary>
        public string StatusClass { get; set; } = "warning";

        // ─── نصوص القرار ────────────────────────────────────────────────────
        public string DecisionSummary { get; set; } = string.Empty;
        public string DecisionExplanation { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;

        // ─── الزر الرئيسي ───────────────────────────────────────────────────
        public string PrimaryActionText { get; set; } = string.Empty;
        public string PrimaryActionName { get; set; } = string.Empty;

        // ─── بطاقات KPI ─────────────────────────────────────────────────────
        public List<DecisionMetricCardVM> Cards { get; set; } = new();

        // ─── صفوف الجدول ────────────────────────────────────────────────────
        public List<DecisionMetricRowVM> Rows { get; set; } = new();

        // ─── فلاتر محددة (تُمرر للروابط) ────────────────────────────────────
        public int? SelectedCurriculumId { get; set; }
        public int? SelectedBatchId { get; set; }
        public int? SelectedInstructorId { get; set; }
        public List<AnalyticsFilterOptionVM> InstructorsFilter { get; set; } = new();





      
        public string DecisionOwner { get; set; } = "الإدارة التعليمية";

     
    }

    public class DecisionMetricCardVM
    {


        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        /// <summary>Bootstrap Icons class مثال: bi-layers-fill</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>primary | info | warning | danger | success</summary>
        public string Theme { get; set; } = "primary";

        /// <summary>نص توضيحي صغير أسفل القيمة.</summary>
        public string Hint { get; set; } = string.Empty;


    }

    public class DecisionMetricRowVM
    {

        public string Name { get; set; } = string.Empty;
        public string? Group { get; set; }
        public string? Owner { get; set; }
        public string PrimaryValue { get; set; } = string.Empty;
        public string? SecondaryValue { get; set; }
        public string? RiskLevel { get; set; }
        public string? Recommendation { get; set; }

        // روابط drill-down اختيارية
        public string? ActionName { get; set; }
        public int? StudentId { get; set; }
        public int? BatchId { get; set; }
        public int? LessonId { get; set; }
        public int? InstructorId { get; set; }


      
    }
}
