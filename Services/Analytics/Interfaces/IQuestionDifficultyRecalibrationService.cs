using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics.Interfaces
{
    public interface IQuestionDifficultyRecalibrationService
    {
        /// <summary>
        /// يحسب التصنيف الجديد لكل سؤال دون حفظ — للمعاينة فقط.
        /// </summary>
        Task<QuestionDifficultyRecalibrationResultVM> PreviewAsync(QuestionDifficultyRecalibrationInputVM input);

        /// <summary>
        /// يحسب التصنيف الجديد ويحفظه على قاعدة البيانات مباشرة.
        /// </summary>
        Task<QuestionDifficultyRecalibrationResultVM> ApplyAsync(QuestionDifficultyRecalibrationInputVM input);

        /// <summary>
        /// يجلب توزيع الأسئلة الحالي حسب مستوى الصعوبة مجمّعاً لكل منهج.
        /// </summary>
        Task<List<CurriculumQuestionDistributionVM>> GetDistributionAsync(int? curriculumId);
    }
}
