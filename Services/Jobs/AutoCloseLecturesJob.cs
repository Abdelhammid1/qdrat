using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Jobs
{
    public class AutoCloseLecturesJob
    {
        private readonly ApplicationDbContext _context;

        public AutoCloseLecturesJob(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RunAsync()
        {
            var now = DateTime.UtcNow;

            var lectures = await _context.Lecture
                .Where(l =>
                    l.ActualEndTime == null &&
                    l.ActualStartTime != null &&
                    l.DurationMinutes != null)
                .ToListAsync();

            var toClose = new List<Entities.Lecture>();

            foreach (var lecture in lectures)
            {
                var plannedEnd = lecture.ActualStartTime!.Value
                    .AddMinutes(lecture.DurationMinutes!.Value);

                if (plannedEnd < now)
                    toClose.Add(lecture);
            }

            if (!toClose.Any())
                return;

            foreach (var lecture in toClose)
            {
                lecture.ActualEndTime = lecture.ActualStartTime!.Value
                    .AddMinutes(lecture.DurationMinutes!.Value);
                lecture.EndedByUserId = null;
                lecture.EndedByRole = "System";
            }

            await _context.SaveChangesAsync();
        }
    }
}
