namespace QdratNew.ViewModels.Exam
{
    // Sprint 9 (MSE-F / F1): شاشة الترحيب بالمرحلة — تُعرض للطالب قبل استدعاء StartStageAsync
    public class MinistrySimExamStageWelcomeVm
    {
        public int MinistrySimExamId { get; set; }
        public string ExamTitle { get; set; }

        public int StageNumber { get; set; }
        public int TotalStages { get; set; }

        public string QuantSectionTitle { get; set; }
        public string VerbalSectionTitle { get; set; }
        public int QuantQuestionCount { get; set; }
        public int VerbalQuestionCount { get; set; }
        public int TotalQuestionCount => QuantQuestionCount + VerbalQuestionCount;

        public int DurationMinutes { get; set; }

        // true لو الطالب يعود لمرحلة بدأها بالفعل ولم تُقفل بعد (تحديث صفحة مثلاً)
        public bool IsResuming { get; set; }

        // الثواني المتبقية قبل البدء التلقائي للقسم (من القسم الثاني فصاعدًا) — null = لا عدّاد (القسم الأول أو الاستئناف)
        public int? AutoStartSeconds { get; set; }

        // ملخص القسم السابق الذي انتهى وقته — يُعرض مع عدّاد البدء التلقائي
        public int PreviousStageAnswered { get; set; }
        public int PreviousStageTotal { get; set; }

        // true لو أتم الطالب كل المراحل بالفعل (لا مزيد من المراحل لعرضها)
        public bool IsExamFinished { get; set; }
    }
}
