using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    // Sprint 12 (MSE-G / G2): شاشة نتيجة بسيطة للطالب — نتيجة إجمالية + نتيجة كل مرحلة
    // إعادة تصميم لاحقة: كروت إحصاءات + رسوم بيانية (صحيحة/خاطئة/متخطاة، أداء كل مرحلة، كمي مقابل لفظي)
    public class MinistrySimExamResultVm
    {
        public int MinistrySimExamId { get; set; }
        public string ExamTitle { get; set; }

        // Sprint 14 (MSE-H / H1): يُملأ فقط عند استدعاء الخدمة من سياق Admin (Drill-down) — الطالب نفسه لا يحتاجه لعرض نتيجته الخاصة
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public double TotalScorePercent { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<MinistrySimExamStageResultVm> Stages { get; set; } = new List<MinistrySimExamStageResultVm>();

        public int TotalQuestions { get; set; }
        public int TotalCorrect { get; set; }
        public int TotalWrong { get; set; }
        public int TotalUnanswered { get; set; }

        public int QuantTotal { get; set; }
        public int QuantCorrect { get; set; }
        public int QuantWrong { get; set; }
        public int QuantUnanswered { get; set; }
        public int VerbalTotal { get; set; }
        public int VerbalCorrect { get; set; }
        public int VerbalWrong { get; set; }
        public int VerbalUnanswered { get; set; }

        public double QuantPercent => QuantTotal > 0 ? Math.Round(QuantCorrect * 100.0 / QuantTotal, 1) : 0;
        public double VerbalPercent => VerbalTotal > 0 ? Math.Round(VerbalCorrect * 100.0 / VerbalTotal, 1) : 0;
    }

    public class MinistrySimExamStageResultVm
    {
        public int StageNumber { get; set; }
        public double StagePercentScore { get; set; }
        public bool TimeExpired { get; set; }
        public int DurationMinutes { get; set; }

        public int QuestionCount { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int UnansweredCount { get; set; }
    }
}
