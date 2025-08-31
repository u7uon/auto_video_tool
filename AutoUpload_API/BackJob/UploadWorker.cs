
using AutoUpload_API.Data;
using AutoUpload_API.Models;
using static AutoUpload_API.IServices.IYoutubeService;

namespace AutoUpload_API.BackJob
{
    public class UploadWorker 
    {
        private readonly AppDbContext _db;
        private readonly IYouTubeService _youtubeService;
        public UploadWorker(AppDbContext db, IYouTubeService youtubeService) 
        {
            _db = db;
            _youtubeService = youtubeService;
        }

        public async Task UploadVideoAsync(Guid videoId)
        {
            var video = await _db.Videos.FindAsync(videoId);
            if (video == null) return;
            try
            {
                var youtubeId = await _youtubeService.UploadVideoAsync(video.FilePath, video,video.UserId);
                video.YouTubeId = youtubeId;
                video.Status = "success"; 
            }
            catch (Exception ex)
            {
                video.Status = "failed";
                video.Message = ex.Message;
            }
            await _db.SaveChangesAsync();
        }


    }
}
