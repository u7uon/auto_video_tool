using AutoUpload_API.DTOs;
using AutoUpload_API.Models;

namespace AutoUpload_API.IServices
{
    // Video service interface
    public interface IVideoService
    {
        Task<IEnumerable<Video>> GetAllAsync(string status);

        Task<IEnumerable<Video>> GetUserVideosAsync(Guid userId, string status);
        Task UpdateVideo(UpdateVideoDTO dTO,Guid userId);
        Task<Video> CreateAsync(CreateVideoDTO video, Guid userId);
        Task ScheduleUploadAsync(Guid videoId, DateTime scheduleTime, Guid userId);
        Task UploadToYouTubeAsync(Guid videoId, Guid userId);

        Task UpdateVideo(Guid Id, IFormFile newVideo , Guid userId);
    }
}