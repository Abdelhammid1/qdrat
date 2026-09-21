// Services/Implementations/StudentIdentityService.cs
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class StudentIdentityService : IStudentIdentityService
    {
        private readonly ApplicationDbContext _context;

        public StudentIdentityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetCurrentStudentIdAsync(ClaimsPrincipal user)
        {
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return 0;

            return await _context.Students
                .Where(s => s.UserId == userId)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();
        }
    }
}
