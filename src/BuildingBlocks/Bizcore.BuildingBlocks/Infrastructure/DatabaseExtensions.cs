using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Đảm bảo Database tồn tại (tạo mới nếu chưa có) bằng cách kết nối qua 'master'.
        /// Giải quyết vấn đề các service như Hangfire/HealthChecks kết nối tới DB trước khi EF Migrate chạy.
        /// </summary>
        public static void PreCreateDatabase(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = builder.InitialCatalog;
                
                // Nếu không có tên DB thì bỏ qua
                if (string.IsNullOrEmpty(databaseName)) return;

                builder.InitialCatalog = "master";
                using var connection = new SqlConnection(builder.ConnectionString);
                connection.Open();
                
                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{databaseName}') 
                    BEGIN
                        CREATE DATABASE [{databaseName}];
                    END";
                command.ExecuteNonQuery();
            }
            catch
            {
                // Swallow exception - EF Migrate sẽ thử lại sau đó nếu bước này thất bại
            }
        }

        /// <summary>
        /// Thực hiện Migration tự động cho DbContext.
        /// </summary>
        public static async Task MigrateDatabaseAsync<TContext>(this IServiceProvider serviceProvider) 
            where TContext : DbContext
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var dbName = context.Database.GetDbConnection().Database;
            
            try 
            {
                await context.Database.MigrateAsync();
            }
            catch (SqlException ex)
            {
                // Error 2714: There is already an object named 'X' in the database.
                if (ex.Number == 2714)
                {
                    throw new InvalidOperationException(
                        $"Migration failed for '{dbName}' because an object already exists. " +
                        "This usually happens when multiple services share the same database or " +
                        "the database was created without migration history. " +
                        "Please ensure each service has a unique database name in appsettings.json.", ex);
                }
                throw;
            }
        }
    }
}
