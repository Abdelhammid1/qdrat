using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Partner;
using QdratNew.ViewModels.Partner.Instructor;

namespace QdratNew.Services.Partner
{
    public interface IPartnerInstructorService
    {
        Task<List<PartnerInstructorBatchVm>> GetInstructorBatchesAsync(int instructorId, int partnerId);
    }

    public class PartnerInstructorService : IPartnerInstructorService
    {
        private readonly ApplicationDbContext _context;

        public PartnerInstructorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PartnerInstructorBatchVm>> GetInstructorBatchesAsync(
            int instructorId,
            int partnerId)
        {
            return await (
                from icb in _context.InstructorCurriculumBatches
                join b in _context.Batches on icb.BatchId equals b.Id
                join br in _context.Branches on b.BranchId equals br.Id
                join c in _context.Curriculums on icb.CurriculumId equals c.Id
                where icb.InstructorId == instructorId
                      && br.PartnerId == partnerId
                select new PartnerInstructorBatchVm
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CurriculumId = c.Id,
                    CurriculumTitle = c.Title
                }
            )
            .AsNoTracking()
            .ToListAsync();
        }
    }
}