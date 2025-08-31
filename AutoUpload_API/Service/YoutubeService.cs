using AutoUpload_API.Data;
using AutoUpload_API.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using static AutoUpload_API.IServices.IYoutubeService;

namespace AutoUpload_API.Service
{
    public class YouTubeService : IYouTubeService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly ILogger<YouTubeService> _logger;

        public YouTubeService(IConfiguration config, AppDbContext context, ILogger<YouTubeService> logger)
        {
            _config = config;
            _context = context;
            _logger = logger;
        }

        public async Task<string> UploadVideoAsync(string filePath, Models.Video video, Guid userId)
        {
            // Get user and their tokens
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("User not found");

            if (string.IsNullOrEmpty(user.GoogleAccessToken))
                throw new ArgumentException("User not authenticated with Google");

            // Check if token is expired and refresh if needed
            if (user.AccessTokenExpiry <= DateTime.UtcNow)
            {
                await RefreshUserTokenAsync(user);
            }

            // Create credential with user's access token
            var credential = GoogleCredential.FromAccessToken(user.GoogleAccessToken);

            var youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "VideoSchedulerApp"
            });

            var videoResource = new Google.Apis.YouTube.v3.Data.Video
            {
                Snippet = new VideoSnippet
                {
                    Title = video.Title,
                    Description = video.Description,
                    Tags = video.Tags?.Split(',').Select(t => t.Trim()).ToList(),
                    CategoryId = "22" // People & Blogs
                },
                Status = new VideoStatus { PrivacyStatus = "public" }
            };

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Video file not found: {filePath}");

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open);
                var videosInsertRequest = youtubeService.Videos.Insert(videoResource, "snippet,status", fileStream, "video/*");

                _logger.LogInformation($"Starting YouTube upload for video {video.Id}, file size: {fileStream.Length} bytes");

                var uploadProgress = await videosInsertRequest.UploadAsync();

                if (uploadProgress.Status == UploadStatus.Completed)
                {
                    _logger.LogInformation($"YouTube upload completed successfully for video {video.Id}");
                    return videosInsertRequest.ResponseBody.Id;
                }
                else if (uploadProgress.Status == UploadStatus.Failed)
                {
                    var errorMessage = uploadProgress.Exception?.Message ?? "Unknown upload error";
                    _logger.LogError($"YouTube upload failed for video {video.Id}: {errorMessage}");
                    throw new Exception($"YouTube upload failed: {errorMessage}");
                }
                else
                {
                    _logger.LogWarning($"YouTube upload incomplete for video {video.Id}, status: {uploadProgress.Status}");
                    throw new Exception($"YouTube upload incomplete: {uploadProgress.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during YouTube upload for video {video.Id}");
                throw;
            }
        }

        private async Task RefreshUserTokenAsync(User user)
        {
            if (string.IsNullOrEmpty(user.GoogleRefreshToken))
                throw new ArgumentException("No refresh token available. User needs to re-authenticate.");

            try
            {
                using var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    { "client_id", _config["GoogleOAuth:ClientId"] },
                    { "client_secret", _config["GoogleOAuth:ClientSecret"] },
                    { "refresh_token", user.GoogleRefreshToken },
                    { "grant_type", "refresh_token" }
                };

                var response = await client.PostAsync("https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(values));

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to refresh token for user {user.Id}: {errorContent}");
                    throw new Exception("Failed to refresh access token. User needs to re-authenticate.");
                }

                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<RefreshTokenResponse>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Update user tokens
                user.GoogleAccessToken = tokenResponse.AccessToken;
                user.AccessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully refreshed token for user {user.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing token for user {user.Id}");
                throw;
            }
        }

        private class RefreshTokenResponse
        {
            public string AccessToken { get; set; }
            public int ExpiresIn { get; set; }
            public string TokenType { get; set; }
        }
    }
}