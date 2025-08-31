namespace AutoUpload_API.Models
{
    public class Video
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Tags { get; set; }
        public string FilePath { get; set; }
        public string Status { get; set; } = "Pending"; 
        public DateTime ScheduleAt { get; set; }
        public string?   YouTubeId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Message { get; set; } 

        public string? JobId { get; set; }

       
        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
