using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Interfaces;
using QdratNew.ViewModels.Partner;
using System.Security.Claims;

namespace QdratNew.Services.Implementations
{
    public class UserPartnerDropdownService : IUserPartnerDropdownService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public UserPartnerDropdownService(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<PartnerDropdownViewModel> GetAsync(
            ClaimsPrincipal user,
            int? activePartnerId)
        {
            await using var context = _contextFactory.CreateDbContext();

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return new PartnerDropdownViewModel();
            }

            var partners = await context.UserPartners
                .AsNoTracking()
                .Where(up => up.UserId == userId)
                .Select(up => new PartnerItem
                {
                    Id = up.PartnerId,
                    Name = up.Partner.Name
                })
                .ToListAsync();

            var active = partners.FirstOrDefault(p => p.Id == activePartnerId);

            return new PartnerDropdownViewModel
            {
                ActivePartnerId = activePartnerId,
                ActivePartnerName = active?.Name,
                Partners = partners
            };
        }
    }
}
