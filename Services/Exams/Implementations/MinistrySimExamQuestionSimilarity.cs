using System;
using System.Linq;
using System.Text;
using QdratNew.Enums;
using QdratNew.Helpers;

namespace QdratNew.Services.Exams.Implementations
{
    // بيانات السؤال اللازمة لفحص التكرار داخل اختبار معمل القياس — إسقاط خفيف بدل تحميل كيان Question كاملًا
    public sealed class MinistrySimExamQuestionText
    {
        public Guid Id { get; set; }
        public int LessonId { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public int? VerbalPassageId { get; set; }
        public string ImageUrl { get; set; }
        public string Title { get; set; }
        public string ValueA { get; set; }
        public string ValueB { get; set; }
        public string CorrectAnswer { get; set; }

        // موقع السؤال داخل الاختبار، لرسائل الأدمن
        public int StageQuestionId { get; set; }
        public int StageNumber { get; set; }
        public int StageOrder { get; set; }
    }

    // قواعد "هل السؤالان مكرران؟" لاختبار معمل القياس. التشابه يُحسب بـ TextSimilarityHelper نفسه المستخدم في فحص بنك الأسئلة.
    public static class MinistrySimExamQuestionSimilarity
    {
        // 0 = إيقاف فحص التشابه (يبقى منع السؤال نفسه دائمًا)
        public const string ThresholdSettingKey = "MinistrySimExam.QuestionSimilarityPercent";
        public const int DefaultThresholdPercent = 70;

        public static int ParseThreshold(string settingValue)
        {
            return int.TryParse(settingValue, out var percent) && percent >= 0 && percent <= 100
                ? percent
                : DefaultThresholdPercent;
        }

        // نسبة التشابه 0..100
        public static int ScorePercent(MinistrySimExamQuestionText left, MinistrySimExamQuestionText right)
        {
            if (left.Id == right.Id)
                return 100;

            // عنوان متطابق على قطعتين مختلفتين ("ما الفكرة الرئيسية؟") سؤالان مختلفان فعلًا
            if (left.VerbalPassageId.HasValue && right.VerbalPassageId.HasValue
                && left.VerbalPassageId != right.VerbalPassageId)
                return 0;

            var leftHasImage = !string.IsNullOrWhiteSpace(left.ImageUrl);
            var rightHasImage = !string.IsNullOrWhiteSpace(right.ImageUrl);

            // وكذلك عنوان متطابق على شكلين مختلفين ("أوجد قيمة س")
            if (leftHasImage && rightHasImage
                && !string.Equals(left.ImageUrl, right.ImageUrl, StringComparison.OrdinalIgnoreCase))
                return 0;

            var leftText = BuildText(left);
            var rightText = BuildText(right);

            // سؤالان بلا نص وبالصورة نفسها
            if (leftHasImage && rightHasImage
                && string.IsNullOrWhiteSpace(TextSimilarityHelper.NormalizeForSimilarity(leftText))
                && string.IsNullOrWhiteSpace(TextSimilarityHelper.NormalizeForSimilarity(rightText)))
                return 100;

            return (int)Math.Round(TextSimilarityHelper.CalculateSimilarity(leftText, rightText) * 100, MidpointRounding.AwayFromZero);
        }

        public static bool IsDuplicate(MinistrySimExamQuestionText left, MinistrySimExamQuestionText right, int thresholdPercent)
        {
            if (left.Id == right.Id)
                return true;

            return thresholdPercent > 0 && ScorePercent(left, right) >= thresholdPercent;
        }

        private static string BuildText(MinistrySimExamQuestionText question)
        {
            var joined = string.Join(" ",
                new[] { question.Title, question.ValueA, question.ValueB, question.CorrectAnswer }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

            return UnifyPersianLetters(joined);
        }

        // بنك الأسئلة يحوي نصوصًا منسوخة بحروف وأرقام فارسية (ک ی ۸) فتبدو كلمات مختلفة وهي نفسها
        private static string UnifyPersianLetters(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                builder.Append(character switch
                {
                    'ک' => 'ك',
                    'ی' => 'ي',
                    >= '۰' and <= '۹' => (char)('0' + (character - '۰')),
                    _ => character
                });
            }

            return builder.ToString();
        }
    }
}
