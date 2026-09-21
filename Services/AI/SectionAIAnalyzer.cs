using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Data;

namespace QdratNew.Services.AI
{
    public class SectionAIAnalyzer
    {
        private readonly ApplicationDbContext _context;

        public SectionAIAnalyzer(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(float score, string notes)> AnalyzeSectionAsync(int sectionId)
        {
            var section = await _context.Sections
                .Include(s => s.SectionUnits)
                    .ThenInclude(su => su.Unit)
                        .ThenInclude(u => u.Lessons)
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                return (0f, "❌ لم يتم العثور على المحور.");

            // ✅ استخراج الوحدات المرتبطة بالمحور عبر SectionUnits
            var units = section.SectionUnits.Select(su => su.Unit).ToList();

            if (!units.Any())
                return (0f, "❗ لا توجد وحدات مرتبطة بالمحور.");

            int totalUnits = units.Count;
            int totalLessons = units.SelectMany(u => u.Lessons ?? new List<Lesson>()).Count(); // ✅ تأكد من أن Lessons ليست Null

            float score = (totalUnits >= 3 && totalLessons >= 5) ? 0.9f :
                          (totalUnits >= 2 && totalLessons >= 3) ? 0.7f : 0.4f;

            string notes = score switch
            {
                >= 0.9f => "✅ المحور مكتمل ومغطى جيدًا.",
                >= 0.7f => "⚠️ المحور جيد لكن ينقصه بعض الدروس.",
                _ => "❗ المحور يحتاج إلى تطوير وإضافة وحدات ومحتوى."
            };

            return (score, notes);
        }

    }

}
