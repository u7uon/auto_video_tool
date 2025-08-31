using AutoUpload_API.BackJob;
using AutoUpload_API.Data;
using AutoUpload_API.DTOs;
using AutoUpload_API.IServices;
using AutoUpload_API.Models;
using Azure.Core;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System;
using static AutoUpload_API.IServices.IYoutubeService;

namespace AutoUpload_API.Service
{
    public class VideoService : IVideoService
    {
        private readonly AppDbContext _context;
        private readonly IYouTubeService _youtubeService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly UploadWorker _videoJob;
        private readonly IVideoStorageService storageService;
        public VideoService(AppDbContext context, IYouTubeService youtubeService, IBackgroundJobClient backgroundJobClient, UploadWorker videoJob, IVideoStorageService storageService)
        {
            _context = context;
            _youtubeService = youtubeService;
            _backgroundJobClient = backgroundJobClient;
            _videoJob = videoJob;
            this.storageService = storageService;
        }

        public async Task<IEnumerable<Video>> GetAllAsync(string status) => await _context.Videos.Where(x => x.Status == status).ToListAsync();

        public async Task<Models.Video> CreateAsync(CreateVideoDTO request , Guid userId)
        {

            Video video = new Video
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                Tags = request.Tags,
                Status = "pending",
                ScheduleAt = request.ScheduleAt,
                CreatedAt = DateTime.Now
            };

            var filename = await storageService.SaveVideo(request.File);

            video.FilePath = Path.Combine("wwwroot/videos" + filename ?? throw new ArgumentException("File upload failed"));

            _context.Videos.Add(video);
            await _context.SaveChangesAsync();

            var delay = video.ScheduleAt - DateTime.UtcNow;

            if (delay.TotalSeconds <= 0)
            {
                throw new ArgumentException("Schedule time must be in the future.");
            }

            var jobId = _backgroundJobClient.Schedule(() => _videoJob.UploadVideoAsync(video.Id), delay);

            video.JobId = jobId;
            _context.Videos.Update(video);
            await _context.SaveChangesAsync();

            return video;
        }

        public async Task ScheduleUploadAsync(Guid videoId, DateTime scheduleTime, Guid userId)
        {
            var video = await _context.Videos.FindAsync(videoId);
            if (video != null)
            {
                video.ScheduleAt = scheduleTime;
                video.Status = "pending";
                await _context.SaveChangesAsync();
            }
        }

        public async Task UploadToYouTubeAsync(Guid videoId, Guid userId)
        {
            var video = await _context.Videos.FindAsync(videoId);
            try
            {
                if (video != null)
                {
                    var youtubeId = await _youtubeService.UploadVideoAsync(video.FilePath, video,video.UserId);
                    video.YouTubeId = youtubeId;
                    video.Status = "success";
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                if (video != null)
                {
                    video.Status = "failed";
                    video.Message = ex.Message;
                    await _context.SaveChangesAsync();
                }
            }

        }

        public async Task UpdateVideo(UpdateVideoDTO dTO, Guid userId)
        {
            var video = _context.Videos.Find(dTO.Id);

            if (video != null)
            {
                video.Title = dTO.Title;
                video.Description = dTO.Description;
                video.Tags = dTO.Tags;
                video.ScheduleAt = dTO.ScheduleAt;
                _context.Videos.Update(video);
                await _context.SaveChangesAsync();


                var delay = video.ScheduleAt - DateTime.UtcNow;
                if (delay.TotalSeconds <= 0)
                {
                    throw new ArgumentException("Schedule time must be in the future.");
                }
                _backgroundJobClient.Delete(video.JobId);
                var jobId = _backgroundJobClient.Schedule(() => _videoJob.UploadVideoAsync(video.Id), delay);

                video.JobId = jobId;
                _context.Videos.Update(video);
                await _context.SaveChangesAsync();
            }

        }

        public async Task UpdateVideo(Guid Id, IFormFile newVideo, Guid userId)
        {
            var video = await _context.Videos.FindAsync(Id);
            if (video != null)
            {
                storageService.DeleteVideo(System.IO.Path.GetFileName(video.FilePath));
                storageService.SaveVideo(newVideo);
                video.FilePath = "wwwroot/videos/" + newVideo.FileName;
                video.Status = "pending";
                video.Message = null;
                _context.Videos.Update(video);
                await _context.SaveChangesAsync();
                var delay = video.ScheduleAt - DateTime.UtcNow;
                if (delay.TotalSeconds <= 0)
                {
                    throw new ArgumentException("Schedule time must be in the future.");
                }
                _backgroundJobClient.Delete(video.JobId);
                var jobId = _backgroundJobClient.Schedule(() => _videoJob.UploadVideoAsync(video.Id), delay);
                video.JobId = jobId;
                _context.Videos.Update(video);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Video>> GetUserVideosAsync(Guid userId, string status)
        {
            return await _context.Videos.Where(v => v.UserId == userId && v.Status == status).ToListAsync();
        }
    }
}