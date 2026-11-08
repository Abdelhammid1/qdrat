namespace QdratNew.ViewModels.Reports
{
    // APD-S5.2: رأس خطاب بديل لـ _ReportLayout (تقرير ولي الأمر للمدرسة الشريكة).
    // نموذج الصفحة الذي لا يطبّق الواجهة، أو يرجع Letterhead = null، يبقى برأس المعهد من الإعدادات كما هو.
    public interface IReportLetterhead
    {
        ReportLetterhead? Letterhead { get; }
    }

    public sealed class ReportLetterhead
    {
        public string Name { get; init; } = "";
        public string? Address { get; init; }
        public string? LogoUrl { get; init; }

        // سطر تحت الاسم، مثل «بالتعاون مع معهد القدرات»
        public string? CollaborationNote { get; init; }
    }
}
