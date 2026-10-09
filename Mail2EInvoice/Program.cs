using Mail2EInvoice;
using Serilog;
using Serilog.Events;

namespace EMLWorker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();
            builder.Services.AddHostedService<CleanUpWorker>();

            builder.Services.AddWindowsService();

            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Error)
            .WriteTo.File(
                path: Path.Combine(AppContext.BaseDirectory, "Mail2EInvoice.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 20,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
            )
            .CreateLogger();

            builder.Logging.AddSerilog();
            builder.Logging.AddEventLog();

            var host = builder.Build();

            await host.RunAsync();
        }
    }
}
