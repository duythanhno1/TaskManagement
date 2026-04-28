using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Notification.API.Consumers;
using Notification.API.Data;
using Notification.API.Hubs;
using Notification.API.Services;
using Prometheus;
using Serilog;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure.Auth;
using Shared.Infrastructure.Localization;
using Shared.Infrastructure.Logging;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Middleware;

Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File("Logs/notification/log_.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;
    builder.Host.AddSharedSerilog("NotificationService");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<JwtMiddleware>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddSingleton<ILocalizer, JsonLocalizer>();

    builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection")));

    builder.Services.AddSharedMassTransit(config, bus =>
    {
        bus.AddConsumer<TaskCreatedConsumer>();
        bus.AddConsumer<TaskAssignedConsumer>();
        bus.AddConsumer<TaskDeadlineConsumer>();
        bus.AddConsumer<MessageSentConsumer>();
        bus.AddConsumer<GroupCreatedConsumer>();
        bus.AddConsumer<ReactionAddedConsumer>();
    });

    var signalRBuilder = builder.Services.AddSignalR();
    var redisConn = config.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConn))
        signalRBuilder.AddStackExchangeRedis(redisConn, o => { o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Notif"); });

    builder.Services.AddSwaggerGen(o => { o.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" }); o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { In = ParameterLocation.Header, Name = "Authorization", Type = SecuritySchemeType.Http, BearerFormat = "JWT", Scheme = "Bearer" }); o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } }); });
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.RequireHttpsMetadata = false; o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:SecretKey"]!)) }; });
    builder.Services.AddHealthChecks().AddDbContextCheck<NotificationDbContext>("database", HealthStatus.Unhealthy);

    var app = builder.Build();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpMetrics();
    if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
    app.UseMiddleware<JwtMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<NotificationHub>("/notificationhub");
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Notification Service terminated"); }
finally { Log.CloseAndFlush(); }
