using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace File.API
{
    public class FileModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // Register Storage Building Block
            services.AddBizcoreModule<StorageModule>(builder);
            
            // Add controllers
            services.AddControllers();
            
            // Add Swagger
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
        }
    }
}
