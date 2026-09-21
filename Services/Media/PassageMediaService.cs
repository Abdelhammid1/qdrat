using Microsoft.AspNetCore.Http;

namespace QdratNew.Services.Media
{
    public interface IPassageMediaService
    {
        Task<string?> SaveMediaAsync(IFormFile file);
        bool IsValidMedia(IFormFile file);
    }

    public class PassageMediaService : IPassageMediaService
    {
        private readonly IWebHostEnvironment _env;

        public PassageMediaService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public bool IsValidMedia(IFormFile file)
        {
            if (file == null) return false;

            var allowedExtensions = new[] { ".mp3", ".mp4", ".wav" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return false;

            // حد أقصى 20MB
            if (file.Length > 20 * 1024 * 1024)
                return false;

            return true;
        }

        public async Task<string?> SaveMediaAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "passages");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/uploads/passages/" + fileName;
        }
    }
}