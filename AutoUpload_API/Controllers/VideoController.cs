using AutoUpload_API.DTOs;
using AutoUpload_API.IServices;
using AutoUpload_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AutoUpload_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Yêu cầu authentication cho tất cả endpoints
    public class VideosController : ControllerBase
    {
        private readonly IVideoService _videoService;

        public VideosController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }
            return userId;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingVideos()
        {
            var userId = GetCurrentUserId();
            return Ok(await _videoService.GetUserVideosAsync(userId, "pending"));
        }

        [HttpGet("success")]
        public async Task<IActionResult> GetSuccessVideos()
        {
            var userId = GetCurrentUserId();
            return Ok(await _videoService.GetUserVideosAsync(userId, "success"));
        }

        [HttpGet("failed")]
        public async Task<IActionResult> GetFailedVideos()
        {
            var userId = GetCurrentUserId();
            return Ok(await _videoService.GetUserVideosAsync(userId, "failed"));
        }


        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetVideo(Guid id)
        //{
        //    var userId = GetCurrentUserId();
        //    var video = await _videoService.GetUserVideoAsync(userId, id);

        //    if (video == null)
        //        return NotFound("Video not found or access denied");

        //    return Ok(video);
        //}

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] CreateVideoDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var created = await _videoService.CreateAsync(request, userId);
                return Ok(new { id = created.Id, message = "Video created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("info")]
        public async Task<IActionResult> Update(UpdateVideoDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _videoService.UpdateVideo(request, userId);
                return Ok(new { message = "Video updated successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to this video");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id}/file")]
        public async Task<IActionResult> UpdateVideoFile(Guid id, IFormFile newVideo)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _videoService.UpdateVideo(id, newVideo, userId);
                return Ok(new { message = "Video file updated successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to this video");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/schedule")]
        public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _videoService.ScheduleUploadAsync(id, request.ScheduleTime, userId);
                return Ok(new { message = "Video scheduled successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to this video");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/upload")]
        public async Task<IActionResult> Upload(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _videoService.UploadToYouTubeAsync(id, userId);
                return Ok(new { message = "Video upload initiated" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("Access denied to this video");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        //[HttpDelete("{id}")]
        //public async Task<IActionResult> Delete(Guid id)
        //{
        //    try
        //    {
        //        var userId = GetCurrentUserId();
        //        await _videoService.DeleteVideoAsync(id, userId);
        //        return Ok(new { message = "Video deleted successfully" });
        //    }
        //    catch (UnauthorizedAccessException)
        //    {
        //        return Forbid("Access denied to this video");
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { error = ex.Message });
        //    }
        //}

        public class ScheduleRequest
        {
            public DateTime ScheduleTime { get; set; }
        }
    }
}