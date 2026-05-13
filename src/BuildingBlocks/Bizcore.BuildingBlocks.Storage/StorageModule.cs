using Bizcore.BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Bizcore.BuildingBlocks.Storage
{
    public class StorageModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            var minioOptions = new MinioOptions();
            builder.Configuration.GetSection(MinioOptions.SectionName).Bind(minioOptions);
            services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));

            services.AddMinio(config =>
            {
                config.WithEndpoint(minioOptions.Endpoint)
                      .WithCredentials(minioOptions.AccessKey, minioOptions.SecretKey)
                      .WithSSL(minioOptions.UseSSL);
            });

            services.AddScoped<IStorageService, MinioStorageService>();
        }
    }
}
