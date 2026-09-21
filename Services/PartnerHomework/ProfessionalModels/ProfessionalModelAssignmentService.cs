using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.PartnerHomework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.PartnerHomework.ProfessionalModels
{
    public class ProfessionalModelAssignmentService
        : IProfessionalModelAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPartnerHomeworkAssignmentService _assignmentService;

        public ProfessionalModelAssignmentService(
            ApplicationDbContext context,
            IPartnerHomeworkAssignmentService assignmentService)
        {
            _context = context;
            _assignmentService = assignmentService;
        }

        public void SendModelToStudents(
            int modelId,
            int partnerId,
            int subscriptionPeriodId,
            List<Guid>? overrideQuestionIds = null)
        {


            var now = DateTime.UtcNow;

            var subscription = _context.PartnerSubscriptions
                .FirstOrDefault(s =>
                    s.PartnerId == partnerId &&
                    s.StartDate <= now &&
                    s.EndDate >= now);


            if (subscription == null)
                throw new InvalidOperationException("لا يوجد عقد شراكة نشط.");

            if (!subscription.CanUseProfessionalModels)
                throw new InvalidOperationException(
                    "عقد الشراكة الحالي لا يسمح باستخدام النماذج الاحترافية."
                );

            // ===============================
            // 1️⃣ جلب النموذج
            // ===============================
            var model = _context.ProfessionalModels
                .Include(m => m.Questions)
                .FirstOrDefault(m => m.Id == modelId);

            if (model == null)
                throw new InvalidOperationException("النموذج غير موجود.");

            // ===============================
            // 2️⃣ تحديد مجموعة الأسئلة النهائية
            // ===============================
            List<Guid> finalQuestionIds;

            if (overrideQuestionIds != null && overrideQuestionIds.Any())
            {
                finalQuestionIds = overrideQuestionIds.Distinct().ToList();
            }
            else
            {
                finalQuestionIds = model.Questions
                    .Where(q => q.QuestionId.HasValue)
                    .OrderBy(q => q.OrderNumber)
                    .Select(q => q.QuestionId!.Value)
                    .ToList();
            }

            if (!finalQuestionIds.Any())
                throw new InvalidOperationException("لا توجد أسئلة صالحة للإرسال.");

            // ===============================
            // 3️⃣ تحديد الكورس من أول سؤال
            // (التزامًا بترتيب المشروع)
            // ===============================
            var firstQuestionId = finalQuestionIds.First();

            // ===============================
            // 3️⃣ تحديد المنهج من السؤال
            // ===============================
            var curriculumId = _context.Questions
                .Where(q => q.Id == firstQuestionId)
                .Select(q => q.Lesson.Section.CurriculumId)
                .FirstOrDefault();

            if (curriculumId == 0)
                throw new InvalidOperationException("تعذر تحديد المنهج المرتبط بالسؤال.");

            // ===============================
            // 4️⃣ تحديد الدورة من المنهج (عبر جدول الربط)
            // ===============================
            var courseId = _context.CourseCurriculums
                .Where(cc => cc.CurriculumId == curriculumId)
                .Select(cc => cc.CourseId)
                .FirstOrDefault();

            if (courseId == 0)
                throw new InvalidOperationException("تعذر تحديد الدورة المرتبطة بالمنهج.");


            if (courseId == 0)
                throw new InvalidOperationException("تعذر تحديد الدورة.");

            // ===============================
            // 4️⃣ الإرسال الفعلي (إعادة استخدام المنطق)
            // ===============================
            _assignmentService.SendToStudentsWithQuestions(
                partnerId,
                subscriptionPeriodId,
                courseId,
                model.Title,
                finalQuestionIds
            );
        }
    }
}
