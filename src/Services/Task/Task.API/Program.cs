using System.Text;
using Auth.API.Protos;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Localization;
using Shared.Infrastructure.Logging;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Middleware;
using Task.API.BackgroundJobs;
using Task.API.Consumers;
using Task.API.Data;
using Task.API.Hubs;
using Task.API.Services;

Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
    .WriteTo.File("Logs/task/log_.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    builder.Host.AddSharedSerilog("TaskService");
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddHttpContextAccessor();

    // DI
    builder.Services.AddScoped<JwtMiddleware>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddSingleton<ILocalizer, JsonLocalizer>();

    // Database
    builder.Services.AddDbContext<TaskDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

    // gRPC Client to Auth Service
    builder.Services.AddGrpcClient<AuthGrpc.AuthGrpcClient>(o =>
    {
        o.Address = new Uri(config["AuthService:GrpcUrl"] ?? "http://auth-service:5011");
    });

    // MassTransit + RabbitMQ
    builder.Services.AddSharedMassTransit(config, bus =>
    {
        bus.AddConsumer<UserUpdatedConsumer>();
    });

    // SignalR + Redis
    var signalRBuilder = builder.Services.AddSignalR();
    var redisConn = config.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConn))
    {
        builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = redisConn; o.InstanceName = "Task_"; });
        signalRBuilder.AddStackExchangeRedis(redisConn, o => { o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Task"); });
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }

    // Swagger
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo { Title = "Task Service API", Version = "v1" });
        o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { In = ParameterLocation.Header, Name = "Authorization", Type = SecuritySchemeType.Http, BearerFormat = "JWT", Scheme = "Bearer" });
        o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
    });

    // JWT
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:SecretKey"]!))
        };
    });

    builder.Services.AddHealthChecks().AddDbContextCheck<TaskDbContext>("database", HealthStatus.Unhealthy);

    // Hangfire
    builder.Services.AddHangfire(hc => hc.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
        .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(15), UseRecommendedIsolationLevel = true, DisableGlobalLocks = true }));
    builder.Services.AddHangfireServer();
    builder.Services.AddTransient<TaskDeadlineReminderJob>();
    builder.Services.AddTransient<Task.API.BackgroundJobs.SoftDeleteCleanupJob>();

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpMetrics();

    if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

    app.UseMiddleware<JwtMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<TaskHub>("/taskhub");
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics");

    try
    {
        app.MapHangfireDashboard("/hangfire");
        RecurringJob.AddOrUpdate<TaskDeadlineReminderJob>("remind-task-deadline", j => j.Execute(), Cron.Hourly());
        RecurringJob.AddOrUpdate<Task.API.BackgroundJobs.SoftDeleteCleanupJob>("cleanup-soft-deleted-tasks", j => j.Execute(), Cron.Weekly(DayOfWeek.Sunday, 3));
    }
    catch (Exception ex) { Log.Warning(ex, "Hangfire init failed"); }

    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Task Service terminated"); }
finally { Log.CloseAndFlush(); }
