using AutoUpload_API.Models;

namespace AutoUpload_API.IServices
{
    public interface IYoutubeService
    {
        public interface IYouTubeService
        {
            Task<string> UploadVideoAsync(string filePath, Models.Video video, Guid userId);
        }
    }
}