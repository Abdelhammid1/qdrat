namespace QdratNew.Helpers
{
    // تسمية "القسم" في اختبار معمل القياس (مرحلة رقم N تُعرض للطالب كـ "القسم الأول/الثاني/..."). العرض فقط — لا تأثير على البيانات.
    public static class SectionNameHelper
    {
        private static readonly string[] Ordinals =
        {
            "الأول", "الثاني", "الثالث", "الرابع", "الخامس",
            "السادس", "السابع", "الثامن", "التاسع", "العاشر"
        };

        // "الأول" / "الثاني" ... وإلا الرقم نفسه
        public static string Ordinal(int stageNumber)
            => stageNumber >= 1 && stageNumber <= Ordinals.Length ? Ordinals[stageNumber - 1] : stageNumber.ToString();

        // "القسم الأول"
        public static string Name(int stageNumber) => "القسم " + Ordinal(stageNumber);
    }
}
