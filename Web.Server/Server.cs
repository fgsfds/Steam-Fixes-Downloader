using Superheater.Web.Server.Providers;
using Superheater.Web.Server.Tasks;
using Telegram;
using Web.Server.Database;
using Web.Server.Helpers;

namespace Superheater.Web.Server
{
    public sealed class Server
    {
        public static void Main(string[] args)
        {
            var dbContext = new DatabaseContext();
            dbContext.Dispose();


            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers().AddJsonOptions(jsonOptions =>
            {
                jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddHostedService<AppReleasesTask>();
            builder.Services.AddHostedService<FileCheckerTask>();

            builder.Services.AddSingleton<FixesProvider>();
            builder.Services.AddSingleton<NewsProvider>();
            builder.Services.AddSingleton<AppReleasesProvider>();
            builder.Services.AddSingleton<HttpClient>(CreateHttpClient);
            builder.Services.AddSingleton<S3Client>();
            builder.Services.AddSingleton<DatabaseContextFactory>();
            builder.Services.AddSingleton<TelegramBot>();

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

            var bot = new TelegramBot();
            _ = bot.StartAsync();

            app.Run();
        }

        private static HttpClient CreateHttpClient(IServiceProvider provider)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Superheater");
            return httpClient;
        }
    }
}
