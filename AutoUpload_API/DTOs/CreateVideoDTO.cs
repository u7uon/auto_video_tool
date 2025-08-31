namespace AutoUpload_API.DTOs
{
    public class CreateVideoDTO
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Tags { get; set; }
        public IFormFile File { get; set; }
        public DateTime ScheduleAt { get; set; }    
    }
}
