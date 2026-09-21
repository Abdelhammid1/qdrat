using System;

namespace QdratNew.Constants
{
    /// <summary>
    /// ثوابت وأدوات مساعدة لخيار "لا أعرف الإجابة" في اختبارات تحديد المستوى.
    /// هذا الخيار لا يُخزَّن كـ QuestionOption حقيقي في قاعدة البيانات؛
    /// يُحقن في الواجهة فقط عندما يكون Exam.AllowDontKnowOption = true.
    /// النص المعروض يتبع اتجاه المنهج (Curriculum.IsRTL): عربي عند التفعيل، إنجليزي عند التعطيل —
    /// بنفس منطق الترجمة الثنائي المستخدم في باقي صفحات المراجعة (دالة T في الـ Views).
    /// </summary>
    public static class ExamAnswerConstants
    {
        public const string DontKnowOptionTextAr = "لا أعرف الإجابة";
        public const string DontKnowOptionTextEn = "I don't know";

        /// <summary>يعيد نص خيار "لا أعرف الإجابة" بالعربي أو الإنجليزي حسب اتجاه المنهج.</summary>
        public static string GetDontKnowOptionText(bool isRTL) =>
            isRTL ? DontKnowOptionTextAr : DontKnowOptionTextEn;

        /// <summary>يتحقق هل النص المُدخَل (بأي من اللغتين) يمثل اختيار "لا أعرف الإجابة".</summary>
        public static bool IsDontKnowOption(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();

            return string.Equals(trimmed, DontKnowOptionTextAr, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, DontKnowOptionTextEn, StringComparison.OrdinalIgnoreCase);
        }
    }
}
