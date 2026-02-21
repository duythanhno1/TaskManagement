using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Managerment.ApplicationContext;
using Managerment.Hubs;
using Managerment.Interfaces;
using Managerment.MiddleWare;
using Managerment.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using WebAPI.Utils.Middlewares;
using Hangfire;
using Hangfire.SqlServer;
using Managerment.BackgroundJobs;
using Prometheus;

// Serilog bootstrap logger — file only
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: $"Logs/log_{DateTime.Now:dd_MM_yyyy}.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Task Management API...");

    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    // Serilog — file only, daily rolling, 30 ngày
    builder.Host.UseSerilog((context, services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "TaskManagement")
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}")
            .WriteTo.File(
                path: "Logs/log_.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // FluentValidation — tự động validate request
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // HttpContextAccessor — cần cho Audit Trail
    builder.Services.AddHttpContextAccessor();

    // DI Registration
    builder.Services.AddScoped<JWTAuthenticationMiddleware>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddSingleton<ILocalizer, JsonLocalizer>();

    // Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

    // Swagger
    builder.Services.AddSwaggerGen(option =>
    {
        option.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Task Managerment API",
            Version = "v1",
            Description = "Task Management & Chat API with real-time features"
        });
        option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Valid Token is needed",
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
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] { }
            }
        });
    });

    // SignalR (basic — backplane added below if Redis is configured)
    var signalRBuilder = builder.Services.AddSignalR();

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

    // CORS — configurable origins
    var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowConfigured", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
            else
            {
                policy.SetIsOriginAllowed(origin => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        });
    });

    // Redis Distributed Cache — fallback to in-memory if no Redis config
    var redisConnection = config.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = config.GetValue("Redis:InstanceName", "TaskMgmt_");
        });

        // SignalR Redis Backplane — enables multi-instance scaling
        signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("TaskMgmt");
        });

        Log.Information("Using Redis distributed cache + SignalR backplane: {RedisConnection}", redisConnection);
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
        Log.Information("Using in-memory distributed cache (Redis not configured)");
    }

    // Response Compression
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/json",
            "text/plain"
        });
    });

    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        options.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        options.Level = CompressionLevel.SmallestSize);

    // Health Check
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database", HealthStatus.Unhealthy);

    // Rate Limiting — configurable
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                ? (int)retryAfterValue.TotalSeconds
                : 10;
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                Message = "Too many requests. Please try again later.",
                RetryAfter = retryAfter
            }, cancellationToken);
        };

        options.AddFixedWindowLimiter("auth", opt =>
        {
            opt.PermitLimit = config.GetValue("RateLimit:Auth:PermitLimit", 5);
            opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Auth:WindowSeconds", 30));
            opt.QueueLimit = 0;
        });

        options.AddFixedWindowLimiter("chat", opt =>
        {
            opt.PermitLimit = config.GetValue("RateLimit:Chat:PermitLimit", 30);
            opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Chat:WindowSeconds", 10));
            opt.QueueLimit = 0;
        });

        options.AddFixedWindowLimiter("general", opt =>
        {
            opt.PermitLimit = config.GetValue("RateLimit:General:PermitLimit", 100);
            opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:General:WindowSeconds", 60));
            opt.QueueLimit = 0;
        });
    });

    // Hangfire — Background Jobs
    builder.Services.AddHangfire(hangfireConfig => hangfireConfig
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
    builder.Services.AddHangfireServer();

    // Register job classes
    builder.Services.AddTransient<TokenCleanupJob>();
    builder.Services.AddTransient<SoftDeleteCleanupJob>();
    builder.Services.AddTransient<TaskDeadlineReminderJob>();

    var app = builder.Build();

    // Middleware pipeline

    // 1. Global Exception Handler
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // 2. Serilog request logging — log mỗi HTTP request với duration
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
            ex != null ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : elapsed > 5000 ? LogEventLevel.Warning
            : LogEventLevel.Information;
    });

    // 3. Response Compression
    app.UseResponseCompression();

    // 3.5 Prometheus HTTP metrics
    app.UseHttpMetrics();

    // 4. Swagger
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // 5. CORS
    app.UseCors("AllowConfigured");

    // 6. Rate Limiter
    app.UseRateLimiter();

    // 7. Auth
    app.UseMiddleware<JWTAuthenticationMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    // 8. Endpoints
    app.MapControllers();
    app.MapHub<TaskHub>("/taskhub");
    app.MapHub<ChatHub>("/chathub");
    app.MapHealthChecks("/health");
    app.MapMetrics("/metrics"); // Prometheus metrics endpoint

    // 9. Hangfire Dashboard + Recurring Jobs
    try
    {
        app.MapHangfireDashboard("/hangfire");

        RecurringJob.AddOrUpdate<TokenCleanupJob>(
            "cleanup-expired-tokens",
            job => job.Execute(),
            Cron.Daily(2));

        RecurringJob.AddOrUpdate<SoftDeleteCleanupJob>(
            "cleanup-soft-deleted",
            job => job.Execute(),
            Cron.Weekly(DayOfWeek.Sunday, 3));

        RecurringJob.AddOrUpdate<TaskDeadlineReminderJob>(
            "remind-task-deadline",
            job => job.Execute(),
            Cron.Hourly());

        Log.Information("Hangfire recurring jobs registered successfully");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Hangfire initialization failed — background jobs disabled. Ensure database is available.");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}