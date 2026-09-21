using QdratNew.Entities;

namespace QdratNew.Data.Seeders
{
    public static class SystemSettingsSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            var settings = new List<SystemSetting>
            {
                new("DefaultHomeworkQuestionsPerLesson", "3", "عدد الأسئلة في كل درس داخل الواجب"),
                new("SectionExamQuestionsCount", "20", "عدد الأسئلة في اختبار المحور"),
                new("CurriculumExamQuestionsCount", "40", "عدد الأسئلة في اختبار المنهج"),
                new("CourseExamQuestionsCount", "60", "عدد الأسئلة في اختبار الدورة"),

                // 🧠 إعدادات اختبار المنهج المجمع (Final Course Exam)
                new("FinalExamSharedQuantitativeCount", "10", "عدد الأسئلة الكمية في القسم الأول من اختبار المنهج المجمع"),
                new("FinalExamSharedVerbalCount", "10", "عدد الأسئلة اللفظية في القسم الأول من اختبار المنهج المجمع"),
                new("FinalExamPerCurriculumQuestionCount", "20", "عدد الأسئلة في كل قسم خاص بمنهج ضمن اختبار المنهج المجمع"),

                // 🧠 إعدادات الاختبارات المتقدمة الأخرى
                new("PromoExamQuestionCount", "10", "عدد الأسئلة في الاختبار الترويجي"),
                new("DefaultExamDurationMinutes", "45", "المدة الافتراضية للاختبار بالدقائق"),
                new("ExamTimePerQuestionSeconds", "60", "الزمن المخصص لكل سؤال (بالثواني)"),
                new("ExamEnforceAttendanceForLab", "true", "هل يجب تسجيل الحضور قبل الاختبارات الحضورية؟"),
                new("ExamAllowOnline", "true", "هل يُسمح بالاختبارات الأونلاين؟"),
                new("ExamAllowInLab", "true", "هل يُسمح باختبارات المعمل فقط؟"),
                new("ExamPassPercentage", "60", "النسبة المطلوبة لاجتياز أي اختبار (%)"),
                new("ExamShowAnswersAfterSubmit", "false", "عرض الإجابات الصحيحة بعد الحل"),
                new("ExamEnableAIRecommendations", "true", "تفعيل توصيات الذكاء الاصطناعي بعد الاختبار"),
                new("SelfAssessmentExamQuestionCount", "35", "عدد الأسئلة في الاختبار التقييمي الذاتي للطالب"),

                // 🟢 عدد المحاضرات القياسي لكل منهج
                new("CurriculumLectureCount", "10", "عدد المحاضرات القياسي لكل منهج"),

                // 🎨 إعدادات الهوية البصرية
                new("SiteName", "معهد القدرات للتدريب", "اسم المشروع الظاهر في الترويسة والمتصفح"),
                new("SiteMetaDescription", "أقوى منصة تعليمية للتدريب والتقييم باستخدام الذكاء الاصطناعي", "وصف الميتا للموقع"),
                new("SiteMetaKeywords", "تدريب, منصة, قدرات, ذكاء اصطناعي, اختبارات", "الكلمات المفتاحية"),
                new("SiteLogoPath", "/assets/logo.png", "مسار الشعار"),
                new("SiteFaviconPath", "/assets/favicon.ico", "مسار فافيكون المتصفح"),
                new("SupportEmail", "support@qdrat.com", "بريد الدعم الفني"),
                new("FacebookPageUrl", "https://facebook.com/qdrat", "رابط صفحة الفيسبوك"),
                new("FooterCopyright", "جميع الحقوق محفوظة © لمعهد القدرات", "الحقوق في الفوتر"),

                // 📍 عنوان المعهد — يُستخدم في رأس الخطابات/التقارير الرسمية القابلة للطباعة (Sprint 19 / MSE-K / K4)
                new("InstituteAddress", "المملكة العربية السعودية", "عنوان المعهد الظاهر في رأس التقارير والخطابات الرسمية"),
            };

            var newSettings = settings
                .Where(s => !context.SystemSettings.Any(x => x.Key == s.Key))
                .ToList();

            if (newSettings.Any())
            {
                context.SystemSettings.AddRange(newSettings);
                context.SaveChanges();
            }
        }
    }
}
