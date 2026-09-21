using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.Enums;
using QdratNew.Services.Exams.Models;

namespace QdratNew.Services.Exams.Interfaces
{
    // محرك توليد أسئلة اختبار معمل القياس + بوابة الاكتمال الصارمة قبل النشر (يُنفَّذ في Sprint 4/5 — MSE-C)
    public interface IMinistrySimExamGeneratorService
    {
        // يُنفَّذ عند الضغط على "بناء الأسئلة" لكل مرحلة — لا يحفظ نشرًا نهائيًا، فقط يملأ MinistrySimExamStageQuestion
        Task<MinistrySimExamGenerationResult> GenerateStageQuestionsAsync(
            int stageId,
            List<IndicatorSelectionInput> quantSelections,
            List<IndicatorSelectionInput> verbalSelections,
            List<Guid> excludeQuestionIds = null);

        // بوابة النشر الصارمة — تُستدعى قبل تعيين IsPublished = true
        Task<MinistrySimExamValidationResult> ValidateExactCountsAsync(int ministrySimExamId);

        // Sprint 7 (MSE-D / D3): يستبعد سؤالاً واحدًا من المرحلة — حذف الرابط فقط بلا أي سحب تعويضي تلقائي (القرار #2)
        Task RemoveStageQuestionAsync(int stageQuestionId);

        // Sprint 7 (MSE-D / D3): يعيد توليد كل أسئلة مؤشر واحد (Lesson+Difficulty) بالكامل ضمن مرحلة باختيار عشوائي جديد
        Task<MinistrySimExamGenerationResult> RegenerateIndicatorAsync(int stageId, int lessonId, DifficultyLevel difficulty);

        // Sprint 7 (MSE-D / D4): يضيف سؤالاً واحدًا محددًا يدويًا من نفس المؤشر/الصعوبة إلى المرحلة
        Task<MinistrySimExamManualAddResult> AddQuestionManuallyAsync(int stageId, int lessonId, DifficultyLevel difficulty, Guid questionId);
    }
}
