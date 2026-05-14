using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Bizcore.BuildingBlocks.Storage
{
    public class MinioStorageService : IStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly MinioOptions _options;

        public MinioStorageService(IMinioClient minioClient, IOptions<MinioOptions> options)
        {
            _minioClient = minioClient;
            _options = options.Value;
        }

        private string GetBucket(bool isPublic) 
        {
            return isPublic && !string.IsNullOrEmpty(_options.PublicBucketName) 
                ? _options.PublicBucketName 
                : _options.BucketName;
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, bool isPublic = false, CancellationToken cancellationToken = default)
        {
            var bucket = GetBucket(isPublic);
            
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(fileName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);
            
            return fileName;
        }

        public async Task<Stream> DownloadAsync(string fileName, bool isPublic = false, CancellationToken cancellationToken = default)
        {
            var bucket = GetBucket(isPublic);
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task DeleteAsync(string fileName, bool isPublic = false, CancellationToken cancellationToken = default)
        {
            var bucket = GetBucket(isPublic);
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
        }

        public async Task<string> GetFileUrlAsync(string fileName, bool isPublic = false, int expiryInSeconds = 3600, CancellationToken cancellationToken = default)
        {
            var bucket = GetBucket(isPublic);

            // If it's a public file, return the direct URL
            if (isPublic && !string.IsNullOrEmpty(_options.PublicBucketName))
            {
                var endpoint = !string.IsNullOrEmpty(_options.ExternalEndpoint) 
                    ? _options.ExternalEndpoint 
                    : _options.Endpoint;
                
                // Ensure endpoint doesn't end with slash
                endpoint = endpoint.TrimEnd('/');
                return $"{endpoint}/{bucket}/{fileName}";
            }

            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(fileName)
                .WithExpiry(expiryInSeconds);

            if (!string.IsNullOrEmpty(_options.ExternalEndpoint))
            {
                var uri = new Uri(_options.ExternalEndpoint);
                var signingClient = new MinioClient()
                    .WithEndpoint(uri.Host, uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80))
                    .WithCredentials(_options.AccessKey, _options.SecretKey)
                    .WithSSL(uri.Scheme == "https")
                    .Build();
                
                return await signingClient.PresignedGetObjectAsync(presignedGetObjectArgs);
            }

            return await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
        }
    }
}
