namespace QdratNew.ViewModels.Remedial
{
    public class RemedialBatchReportVm
    {
        public string StudentName { get; set; }
        public bool HasPlan { get; set; }
        public bool HasSession { get; set; }
        public bool Attended { get; set; }

        public string Status
        {
            get
            {
                if (!HasPlan)
                    return "❌ لم يتم إنشاء خطة علاجية";
                if (HasPlan && !HasSession)
                    return "🟡 لديه خطة علاجية بدون جلسة محددة";
                if (HasSession && !Attended)
                    return "🔴 تغيب عن الجلسة المحددة";
                return "🟢 أكمل الجلسة العلاجية";
            }
        }

        public string StatusColor =>
            !HasPlan ? "bg-secondary text-white" :
            !HasSession ? "bg-warning text-dark" :
            !Attended ? "bg-danger text-white" :
            "bg-success text-white";
    }
}
