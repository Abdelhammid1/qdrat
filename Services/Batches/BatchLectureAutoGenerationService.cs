using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.Batches
{
    public class BatchLectureAutoGenerationService : IBatchLectureAutoGenerationService
    {
        private const int LecturesPerCurriculum = 10;
        private const int WeeksPerCurriculum = 2;
        private const int LecturesPerWeek = 5;

        private readonly ApplicationDbContext _context;

        public BatchLectureAutoGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task GenerateForBatchAsync(int batchId, int courseId, DateTime firstLectureStartDateTime, int? lectureDurationMinutes = null)
        {
            var batchGender = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Gender)
                .FirstOrDefaultAsync();

            var courseCurriculums = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == courseId)
                .OrderBy(cc => cc.Id)
                .Select(cc => new
                {
                    CourseCurriculumId = cc.Id,
                    cc.CurriculumId,
                    CurriculumTitle = cc.Curriculum.Title
                })
                .ToListAsync();

            if (courseCurriculums.Count == 0)
            {
                throw new InvalidOperationException("لا توجد مناهج مرتبطة بالدورة المختارة، لذلك لا يمكن إنشاء المحاضرات تلقائيًا.");
            }

            var sectionRows = await (
                from cc in _context.CourseCurriculums.AsNoTracking()
                join section in _context.Sections.AsNoTracking()
                    on cc.CurriculumId equals section.CurriculumId
                where cc.CourseId == courseId
                orderby cc.Id, section.Id
                select new
                {
                    cc.CurriculumId,
                    SectionId = section.Id
                })
                .ToListAsync();

            var batchInstructorRows = await (
                from cc in _context.CourseCurriculums.AsNoTracking()
                join relation in _context.InstructorCurriculumBatches.AsNoTracking()
                    on cc.CurriculumId equals relation.CurriculumId
                where cc.CourseId == courseId && relation.BatchId == batchId
                orderby cc.Id, relation.Id
                select new
                {
                    cc.CurriculumId,
                    relation.InstructorId
                })
                .ToListAsync();

            var curriculumInstructorQuery =
                from cc in _context.CourseCurriculums.AsNoTracking()
                join relation in _context.CurriculumInstructors.AsNoTracking()
                    on cc.CurriculumId equals relation.CurriculumId
                join instructor in _context.Instructors.AsNoTracking()
                    on relation.InstructorId equals instructor.Id
                where cc.CourseId == courseId && instructor.IsActive && !instructor.IsDeleted
                select new
                {
                    CourseCurriculumId = cc.Id,
                    cc.CurriculumId,
                    relation.InstructorId,
                    instructor.Gender
                };

            if (batchGender == BatchGender.ذكور)
            {
                curriculumInstructorQuery = curriculumInstructorQuery.Where(x => x.Gender == GenderType.Male);
            }
            else if (batchGender == BatchGender.إناث)
            {
                curriculumInstructorQuery = curriculumInstructorQuery.Where(x => x.Gender == GenderType.Female);
            }

            var curriculumInstructorRows = await curriculumInstructorQuery
                .OrderBy(x => x.CourseCurriculumId)
                .ThenBy(x => x.InstructorId)
                .Select(x => new
                {
                    x.CurriculumId,
                    x.InstructorId
                })
                .ToListAsync();

            var firstSunday = MoveToNextSunday(firstLectureStartDateTime);

            TimeSpan? scheduledTime = firstLectureStartDateTime.TimeOfDay != TimeSpan.Zero
                ? firstLectureStartDateTime.TimeOfDay
                : null;

            TimeSpan? scheduledEndTime = scheduledTime.HasValue && lectureDurationMinutes.HasValue && lectureDurationMinutes.Value > 0
                ? scheduledTime.Value.Add(TimeSpan.FromMinutes(lectureDurationMinutes.Value))
                : null;

            var lectures = new List<Lecture>(courseCurriculums.Count * LecturesPerCurriculum);

            for (int curriculumIndex = 0; curriculumIndex < courseCurriculums.Count; curriculumIndex++)
            {
                var curriculum = courseCurriculums[curriculumIndex];

                var sectionId = sectionRows
                    .Where(x => x.CurriculumId == curriculum.CurriculumId)
                    .Select(x => x.SectionId)
                    .FirstOrDefault();

                if (sectionId == 0)
                {
                    throw new InvalidOperationException($"لا يوجد محور مرتبط بالمنهج: {curriculum.CurriculumTitle}");
                }

                var instructorId = batchInstructorRows
                    .Where(x => x.CurriculumId == curriculum.CurriculumId)
                    .Select(x => x.InstructorId)
                    .FirstOrDefault();

                if (instructorId == 0)
                {
                    instructorId = curriculumInstructorRows
                        .Where(x => x.CurriculumId == curriculum.CurriculumId)
                        .Select(x => x.InstructorId)
                        .FirstOrDefault();
                }

                if (instructorId == 0)
                {
                    throw new InvalidOperationException($"لا يوجد مدرب مناسب مرتبط بالمنهج: {curriculum.CurriculumTitle}");
                }

                int weekOffset = curriculumIndex * WeeksPerCurriculum;

                for (int lectureIndex = 0; lectureIndex < LecturesPerCurriculum; lectureIndex++)
                {
                    int weekInsideCurriculum = lectureIndex / LecturesPerWeek;
                    int dayInsideWeek = lectureIndex % LecturesPerWeek;
                    int dayOffset = ((weekOffset + weekInsideCurriculum) * 7) + dayInsideWeek;
                    DateTime lectureDate = firstSunday.AddDays(dayOffset);

                    lectures.Add(new Lecture
                    {
                        Title = $"{curriculum.CurriculumTitle} - محاضرة {lectureIndex + 1}",
                        Location = "",
                        Date = lectureDate,
                        ScheduledTime = scheduledTime,
                        DurationMinutes = lectureDurationMinutes,
                        ScheduledEndTime = scheduledEndTime,
                        InstructorId = instructorId,
                        SectionId = sectionId,
                        CourseId = courseId,
                        BatchId = batchId
                    });
                }
            }

            await _context.BulkInsertAsync(lectures);
        }

        private static DateTime MoveToNextSunday(DateTime dateTime)
        {
            if (dateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return dateTime;
            }

            int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)dateTime.DayOfWeek + 7) % 7;

            if (daysUntilSunday == 0)
            {
                daysUntilSunday = 7;
            }

            return dateTime.AddDays(daysUntilSunday);
        }
    }
}
