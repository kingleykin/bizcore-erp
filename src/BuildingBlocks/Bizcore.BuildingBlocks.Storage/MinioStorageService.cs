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

        public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(fileName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);
            
            return fileName;
        }

        public async Task<Stream> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task DeleteAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
        }

        public async Task<string> GetPresignedUrlAsync(string fileName, int expiryInSeconds = 3600, CancellationToken cancellationToken = default)
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(fileName)
                .WithExpiry(expiryInSeconds);

            // If we have an external endpoint, we need to sign the URL using that endpoint
            // so the Host header in the signature matches what the browser will send.
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
