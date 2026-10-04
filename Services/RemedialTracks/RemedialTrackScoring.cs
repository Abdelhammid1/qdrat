namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S5.3: تصحيح اختبارَي 101/102 — دوال نقية.
    /// قاعدة المقارنة منسوخة حرفيًا من Areas/Students/Controllers/ExamsController (حفظ الإجابة):
    /// string.Equals(selected.Trim(), correct?.Trim(), OrdinalIgnoreCase). لا تطبيع HTML في الأصل فلا نضيفه هنا.
    /// </summary>
    public static class RemedialTrackScoring
    {
        /// <summary>لا إجابة مختارة أو لا إجابة صحيحة معرّفة للسؤال ← غير صحيحة.</summary>
        public static bool IsCorrect(string? selected, string? correctAnswer)
        {
            if (string.IsNullOrWhiteSpace(selected) || string.IsNullOrWhiteSpace(correctAnswer)) return false;
            return string.Equals(selected.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>النسبة بمنزلة عشرية واحدة (مثل ExamResultEngine). إجمالي 0 ← 0.</summary>
        public static double ScorePercent(int correct, int total) =>
            total <= 0 ? 0 : Math.Round(correct * 100.0 / total, 1);

        /// <summary>D3: النجاح score >= PassPercent.</summary>
        public static bool IsPassed(double scorePercent, int passPercent) => scorePercent >= passPercent;
    }
}
