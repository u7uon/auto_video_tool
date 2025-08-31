namespace AutoUpload_API.IServices
{
    public interface IVideoStorageService
    {
        public Task<string> SaveVideo(IFormFile video); 

        public void DeleteVideo(string fileName);

        public Task GetVideo(string fileName);
    }
}
