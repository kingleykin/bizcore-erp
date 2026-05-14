namespace Bizcore.BuildingBlocks.Storage
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, bool isPublic = false, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string fileName, bool isPublic = false, CancellationToken cancellationToken = default);
        Task DeleteAsync(string fileName, bool isPublic = false, CancellationToken cancellationToken = default);
        Task<string> GetFileUrlAsync(string fileName, bool isPublic = false, int expiryInSeconds = 3600, CancellationToken cancellationToken = default);
    }
}
