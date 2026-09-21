using QdratNew.Enums;

namespace QdratNew.Services.Exams.Generators
{
    public interface ICurriculumExamGeneratorService
    {
        // ✅ لتوليد اختبار على منهج + دفعة + محور
        Task<int> GenerateExamForCurriculumAsync(
            int curriculumId,
            int batchId,
            int sectionId,
            int? createdByInstructorId = null);

        // ✅ لتوليد اختبار على منهج + دفعة فقط (بدون محور) مع تحديد عدد الأسئلة ونوع الاختبار
        Task<int> GenerateExamForCurriculumWithQuestionsAsync(
            int curriculumId,
            int batchId,
            int questionCount,
            int? createdByInstructorId = null,
            ExamType examType = ExamType.Course);

        // ✅ لتوليد اختبار موحد لدفعة واحدة يحتوي على أكثر من منهج
        Task<int> GenerateMergedExamForCurriculumsAsync(
            int batchId,
            List<(int CurriculumId, int QuestionCount)> curriculumData,
            int? createdByInstructorId = null,
            ExamType examType = ExamType.Course);



        Task<int> AssignExamToSpecificStudentsAsync(int examAssignmentId, List<int> studentIds);

    }
}
