namespace AutoUpload_API.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔹 Thông tin cơ bản
        public string Email { get; set; }          // Email Google
        public string Name { get; set; }           // Tên hiển thị
        public string AvatarUrl { get; set; }      // Ảnh đại diện Google

        // 🔹 Token Google OAuth
        public string GoogleId { get; set; }       // ID người dùng Google
        public string GoogleAccessToken { get; set; }  // Access Token hiện tại
        public string GoogleRefreshToken { get; set; } // Refresh Token (rất quan trọng)
        public DateTime? AccessTokenExpiry { get; set; } // Thời gian hết hạn Access Token

        // 🔹 YouTube Channel Info
        public string ChannelId { get; set; }      // ID kênh YouTube
        public string ChannelTitle { get; set; }   // Tên kênh YouTube

        // 🔹 Thông tin quản lý
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        public ICollection<Video> Videos { get; set; }
    }

}
