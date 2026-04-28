using System.Text;
using Auth.API.BackgroundJobs;
using Auth.API.Data;
using Auth.API.GrpcServices;
using Auth.API.Services;
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

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(path: "Logs/auth/log_.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    builder.Host.AddSharedSerilog("AuthService");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddGrpc();

    // FluentValidation
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddHttpContextAccessor();

    // DI
    builder.Services.AddScoped<JwtMiddleware>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddSingleton<ILocalizer, JsonLocalizer>();

    // Database
    builder.Services.AddDbContext<AuthDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

    // MassTransit + RabbitMQ
    builder.Services.AddSharedMassTransit(config);

    // Swagger
    builder.Services.AddSwaggerGen(option =>
    {
        option.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth Service API", Version = "v1" });
        option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "JWT Token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        option.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // JWT Authentication
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(config["JWT:SecretKey"]!))
            };
        });

    // Health Check
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AuthDbContext>("database", HealthStatus.Unhealthy);

    // Hangfire
    builder.Services.AddHangfire(hangfireConfig => hangfireConfig
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
    builder.Services.AddHangfireServer();
    builder.Services.AddTransient<TokenCleanupJob>();

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpMetrics();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<JwtMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGrpcService<AuthGrpcService>();
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics");

    try
    {
        app.MapHangfireDashboard("/hangfire");
        RecurringJob.AddOrUpdate<TokenCleanupJob>("cleanup-expired-tokens", job => job.Execute(), Cron.Daily(2));
        Log.Information("Auth Service — Hangfire jobs registered");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Hangfire initialization failed");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Auth Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
