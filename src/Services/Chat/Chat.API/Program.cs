using System.Text;
using Auth.API.Protos;
using Chat.API.Data;
using Chat.API.Hubs;
using Chat.API.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
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

Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File("Logs/chat/log_.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;
    builder.Host.AddSharedSerilog("ChatService");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<JwtMiddleware>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddSingleton<ILocalizer, JsonLocalizer>();

    builder.Services.AddDbContext<ChatDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection")));
    builder.Services.AddGrpcClient<AuthGrpc.AuthGrpcClient>(o => { o.Address = new Uri(config["AuthService:GrpcUrl"] ?? "http://auth-service:5011"); });
    builder.Services.AddSharedMassTransit(config);

    var signalRBuilder = builder.Services.AddSignalR();
    var redisConn = config.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConn))
        signalRBuilder.AddStackExchangeRedis(redisConn, o => { o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Chat"); });

    builder.Services.AddSwaggerGen(o => { o.SwaggerDoc("v1", new OpenApiInfo { Title = "Chat Service API", Version = "v1" }); o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { In = ParameterLocation.Header, Name = "Authorization", Type = SecuritySchemeType.Http, BearerFormat = "JWT", Scheme = "Bearer" }); o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } }); });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.RequireHttpsMetadata = false; o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT:SecretKey"]!)) }; });
    builder.Services.AddHealthChecks().AddDbContextCheck<ChatDbContext>("database", HealthStatus.Unhealthy);

    var app = builder.Build();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpMetrics();
    if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
    app.UseMiddleware<JwtMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<ChatHub>("/chathub");
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Chat Service terminated"); }
finally { Log.CloseAndFlush(); }
