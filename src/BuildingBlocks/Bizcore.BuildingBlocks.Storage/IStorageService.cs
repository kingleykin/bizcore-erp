namespace Bizcore.BuildingBlocks.Storage
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
        Task<Stream> DownloadAsync(string fileName, CancellationToken cancellationToken = default);
        Task DeleteAsync(string fileName, CancellationToken cancellationToken = default);
        Task<string> GetPresignedUrlAsync(string fileName, int expiryInSeconds = 3600, CancellationToken cancellationToken = default);
    }
}
