using QdratNew.Analytics;
using QdratNew.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class StudentAnalyticsHelper
    {
        private readonly ApplicationDbContext _context;
        private readonly IStudentAnalyticsService _analytics;
        private readonly IStudentAnalyticsService _studentAnalyticsService;

        public StudentAnalyticsHelper(ApplicationDbContext context, IStudentAnalyticsService analytics, IStudentAnalyticsService studentAnalyticsService)
        {
            _context = context;
            _analytics = analytics;
            _studentAnalyticsService = studentAnalyticsService;
        }

        public async Task SafeRecordAnalyticsAsync(int studentId, Guid questionId, bool isCorrect)
        {
            try
            {
                var question = await _context.Questions
                    .Include(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                    .FirstOrDefaultAsync(q => q.Id == questionId);

                if (question == null)
                    return;

                if (!isCorrect)
                {
                    // 🔹 سجل نقطة ضعف
                    await _analytics.RecordWeaknessAsync(studentId, question.Id, false);
                }

                // 🔹 تحديث التقدم في المنهج
                var curriculumId = question.Lesson?.Section?.CurriculumId ?? 0;
                if (curriculumId > 0)
                {
                    await _analytics.UpdateProgressAsync(studentId, curriculumId, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [StudentAnalyticsHelper] Error: {ex.Message}");
                // لا نرمي استثناء علشان نحافظ على استقرار الأكشن
            }
        }


        public async Task SafeRecordAttendanceAsync(int studentId, int lectureId, bool isPresent)
        {
            try
            {
                await _studentAnalyticsService.RecordAttendanceAsync(studentId, lectureId, isPresent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SafeRecordAttendanceAsync Error: {ex.Message}");
            }
        }

        // ✅ لتحديث نسبة التقدم في المنهج بعد إنهاء واجب
        public async Task SafeUpdateProgressAsync(int studentId, int homeworkSetId)
        {
            try
            {
                // 🟢 تحديد المنهج المرتبط بالواجب
                var curriculumId = await _studentAnalyticsService.GetCurriculumIdByHomeworkAsync(homeworkSetId);
                if (curriculumId > 0)
                    await _studentAnalyticsService.UpdateProgressAsync(studentId, curriculumId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SafeUpdateProgressAsync Error: {ex.Message}");
            }
        }
        public async Task SafeRecordWeaknessAsync(int studentId, Guid questionId, bool isCorrect)
        {
            try
            {
                await _studentAnalyticsService.RecordWeaknessAsync(studentId, questionId, isCorrect);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SafeRecordWeaknessAsync Error: {ex.Message}");
            }
        }

        // ✅ لتحديث ترتيب الطالب في الدفعة بعد تسليم الواجب
        public async Task SafeUpdateRankAsync(int studentId, int batchId)
        {
            try
            {
                await _studentAnalyticsService.UpdateRankHistoryAsync(studentId, batchId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SafeUpdateRankAsync Error: {ex.Message}");
            }
        }





    }
}
