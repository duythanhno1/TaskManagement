using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
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
using WebAPI.Utils.Middlewares;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// DI Registration
builder.Services.AddScoped<JWTAuthenticationMiddleware>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

// Swagger
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Managerment API",
        Version = "v1"
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

// SignalR
builder.Services.AddSignalR();

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

// CORS — đọc allowed origins từ config
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
            // Fallback: cho phép tất cả nếu chưa config
            policy.SetIsOriginAllowed(origin => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

// Memory Cache
builder.Services.AddMemoryCache();

// Response Compression (gzip + brotli)
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

// Rate Limiting — đọc config từ appsettings.json
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

    // Auth: configurable (default 5/30s)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = config.GetValue("RateLimit:Auth:PermitLimit", 5);
        opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Auth:WindowSeconds", 30));
        opt.QueueLimit = 0;
    });

    // Chat: configurable (default 30/10s)
    options.AddFixedWindowLimiter("chat", opt =>
    {
        opt.PermitLimit = config.GetValue("RateLimit:Chat:PermitLimit", 30);
        opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Chat:WindowSeconds", 10));
        opt.QueueLimit = 0;
    });

    // General: configurable (default 100/60s)
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = config.GetValue("RateLimit:General:PermitLimit", 100);
        opt.Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:General:WindowSeconds", 60));
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Middleware pipeline (thứ tự quan trọng!)

// 1. Global Exception Handler — đầu tiên để bắt mọi exception
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. Response Compression
app.UseResponseCompression();

// 3. Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 4. CORS
app.UseCors("AllowConfigured");

// 5. Rate Limiter
app.UseRateLimiter();

// 6. Auth
app.UseMiddleware<JWTAuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// 7. Endpoints
app.MapControllers();
app.MapHub<TaskHub>("/taskhub");
app.MapHub<ChatHub>("/chathub");
app.MapHealthChecks("/health");

app.Run();