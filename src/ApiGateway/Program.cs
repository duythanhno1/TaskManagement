using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss}] [Gateway] {Message:lj}{NewLine}")
    .WriteTo.File("Logs/gateway/log_.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
    builder.Services.AddOcelot(builder.Configuration);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(_ => true).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        });
    });

    var app = builder.Build();

    app.UseCors("AllowAll");
    app.UseSerilogRequestLogging();

    app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "API Gateway" }));

    await app.UseOcelot();

    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "API Gateway terminated"); }
finally { Log.CloseAndFlush(); }
