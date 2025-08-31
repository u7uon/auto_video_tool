using AutoUpload_API.IServices;

namespace AutoUpload_API.Service
{
    public class VideoStorageService : IVideoStorageService
    {
        private readonly IWebHostEnvironment _env;

        public VideoStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveVideo(IFormFile video)
        {
            if (video == null || video.Length == 0) throw new ArgumentException("Video file is invalid!");

            // Validate type & size (example max 200MB)
            if (!video.ContentType.StartsWith("video/")) throw new ArgumentException("Only video files allowed");
            const long maxBytes = 200L * 1024 * 1024;
            if (video.Length > maxBytes) throw new ArgumentException("Video too large");

            var uploadPath = Path.Combine(_env.WebRootPath ?? ".", "videos");
            Directory.CreateDirectory(uploadPath);

            var ext = Path.GetExtension(video.FileName);
            var safeFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadPath, safeFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await video.CopyToAsync(stream);

            return safeFileName; // caller combines with webroot if needed
        }

        public void DeleteVideo(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name is required!");

            var uploadPath = Path.Combine(_env.WebRootPath, "videos");
            var filePath = Path.Combine(uploadPath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }


        }



        public Task GetVideo(string fileName)
        {
            throw new NotImplementedException();
        }
    }
}
