using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace QdratNew.Helpers
{
    public static class ImageWebpHelper
    {
        public static async Task<string> SaveAsWebpAsync(
            IFormFile file,
            string uploadPath,
            int maxWidth = 1600,
            int quality = 75)
        {
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var fileName = $"{Guid.NewGuid():N}.webp";
            var fullPath = Path.Combine(uploadPath, fileName);

            using var image = await Image.LoadAsync(file.OpenReadStream());

            if (image.Width > maxWidth)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxWidth, 0)
                }));
            }

            await image.SaveAsync(fullPath, new WebpEncoder
            {
                Quality = quality
            });

            return fileName;
        }
    }
}
