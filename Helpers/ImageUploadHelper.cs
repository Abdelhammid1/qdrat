using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using Microsoft.AspNetCore.Http;

namespace QdratNew.Helpers
{
    public static class ImageUploadHelper
    {
        public static async Task<string> SaveAsWebPAsync(
            IFormFile imageFile,
            string webRootPath,
            string folderName,
            int maxWidth = 1200
        )
        {
            var uploadsFolder = Path.Combine(webRootPath, "uploads", folderName);
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}.webp";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using var image = await Image.LoadAsync(imageFile.OpenReadStream());

            if (image.Width > maxWidth)
            {
                image.Mutate(x =>
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size(maxWidth, 0),
                        Mode = ResizeMode.Max
                    })
                );
            }

            var encoder = new WebpEncoder
            {
                Quality = 75
            };

            await image.SaveAsync(fullPath, encoder);

            return $"/uploads/{folderName}/{fileName}";
        }
    }
}
