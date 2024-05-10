using Common;
using Superheater.Web.Server.Providers;
using Superheater.Web.Server.Tasks;
using Web.Server.Helpers;

namespace Superheater.Web.Server
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers().AddJsonOptions(jsonOptions =>
            {
                jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddHostedService<FixesListUpdateTask>();
            builder.Services.AddHostedService<AppReleasesTask>();
            builder.Services.AddHostedService<NewsListUpdateTask>();

            builder.Services.AddSingleton<FixesProvider>();
            builder.Services.AddSingleton<NewsProvider>();
            builder.Services.AddSingleton<AppReleasesProvider>();
            builder.Services.AddSingleton<HttpClientInstance>();
            builder.Services.AddSingleton<S3Client>();

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
