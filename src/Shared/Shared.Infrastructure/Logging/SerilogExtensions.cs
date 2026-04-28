using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Hosting;

namespace Shared.Infrastructure.Logging
{
    public static class SerilogExtensions
    {
        public static IHostBuilder AddSharedSerilog(this IHostBuilder hostBuilder, string serviceName)
        {
            return hostBuilder.UseSerilog((context, services, loggerConfig) =>
            {
                loggerConfig
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithProperty("Service", serviceName)
                    .WriteTo.Console(
                        restrictedToMinimumLevel: LogEventLevel.Information,
                        outputTemplate: "[{Timestamp:HH:mm:ss}] [{Service}] {Message:lj}{NewLine}")
                    .WriteTo.File(
                        path: $"Logs/{serviceName}/log_.txt",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {Message:lj} {Properties:j}{NewLine}{Exception}");
            });
        }
    }
}
