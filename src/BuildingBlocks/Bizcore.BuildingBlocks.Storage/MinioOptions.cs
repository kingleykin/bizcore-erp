namespace Bizcore.BuildingBlocks.Storage
{
    public class MinioOptions
    {
        public const string SectionName = "Minio";
        public string Endpoint { get; set; } = string.Empty;
        public string ExternalEndpoint { get; set; } = string.Empty; // URL for browser access
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public string PublicBucketName { get; set; } = string.Empty; // Bucket with read-only policy for public access
        public bool UseSSL { get; set; } = false;
    }
}
