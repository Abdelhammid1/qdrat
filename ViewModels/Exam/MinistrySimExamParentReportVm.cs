using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // Sprint 18 (MSE-K / K1+K2): تقرير ولي الأمر — ملخص إحصائي فقط (بلا تفصيل سؤال بسؤال، قرار مُقفَل مع عبدالعظيم)
    // + مقارنة بمتوسط دفعة الطالب. StudentFullExamReportVm القديم كود ميت غير مستخدم — لا يُعاد استخدامه هنا.
    // Sprint 19 (MSE-K / K3+K4): أُضيف رأس خطاب حقيقي (InstituteName/InstituteAddress/SiteLogoUrl)،
    // اتجاه الأداء عبر محاولات سابقة (PreviousAttemptsTrend)، وحقل RecommendationsNote القابل للتحرير.
    public class MinistrySimExamParentReportVm
    {
        // رأس الخطاب — Sprint 19 (K4): مربوط فعليًا بـISystemSettingService بدل Placeholder ثابت
        public DateTime ReportGeneratedAt { get; set; } = DateTime.Now;
        public string InstituteName { get; set; }
        public string InstituteAddress { get; set; }
        public string SiteLogoUrl { get; set; }

        // الطالب والدورة
        public int MinistrySimExamId { get; set; }
        public string ExamTitle { get; set; }
        public string CourseName { get; set; }

        public int StudentId { get; set; }
        public string StudentFullName { get; set; }
        public string BatchName { get; set; }

        // ولي الأمر (من Entities/Parent.cs فقط)
        public string ParentFullName { get; set; }
        public string ParentPhoneNumber { get; set; }
        public string ParentRelationToStudent { get; set; }

        // نتيجة المحاولة الحالية — ملخص إحصائي فقط
        public int AttemptId { get; set; }
        public DateTime? AttemptCompletedAt { get; set; }
        public double TotalScorePercent { get; set; }
        public double QuantScorePercent { get; set; }
        public double VerbalScorePercent { get; set; }
        public List<MinistrySimExamParentReportStageVm> Stages { get; set; } = new List<MinistrySimExamParentReportStageVm>();

        // مقارنة بمتوسط دفعة الطالب (عبر GetBatchAverageStatsAsync — K1)
        public int BatchTotalStudents { get; set; }
        public double BatchAvgScorePercent { get; set; }
        public double BatchAvgQuantPercent { get; set; }
        public double BatchAvgVerbalPercent { get; set; }

        // اتجاه الأداء عبر محاولات سابقة لنفس الطالب في اختبارات محاكاة مختلفة — Sprint 19 (K3)
        public List<MinistrySimExamParentReportTrendPointVm> PreviousAttemptsTrend { get; set; } = new List<MinistrySimExamParentReportTrendPointVm>();

        // ملاحظات/توصيات حرة يكتبها الأدمن/المدرّس قبل الطباعة — Sprint 19 (K3)
        public string RecommendationsNote { get; set; }

        // true عندما تُعرض الشاشة في وضع الطباعة/PDF (Puppeteer) — تُخفي عناصر التحرير (K5)
        public bool IsPrintMode { get; set; }

        // توصية محفِّزة عامة على مستوى الاختبار كامل — مبنية عبر ResultToneHelper (بلا لغة رسوب إطلاقًا)
        public string OverallMotivationHeading { get; set; }
        public string OverallMotivationMessage { get; set; }
    }

    public class MinistrySimExamParentReportStageVm
    {
        public int StageNumber { get; set; }
        public double StagePercentScore { get; set; }
        public bool TimeExpired { get; set; }
        public int DurationMinutes { get; set; }

        // توصية محفِّزة خاصة بهذه المرحلة — مبنية عبر ResultToneHelper (بلا لغة رسوب إطلاقًا)
        public string MotivationMessage { get; set; }
    }

    // Sprint 19 (MSE-K / K3): نقطة واحدة على شارت اتجاه الأداء عبر المحاولات السابقة
    public class MinistrySimExamParentReportTrendPointVm
    {
        public string ExamTitle { get; set; }
        public DateTime CompletedAt { get; set; }
        public double TotalScorePercent { get; set; }
    }
}
