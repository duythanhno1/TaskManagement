using System.Net;
using System.Text.Json;

namespace Managerment.MiddleWare
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var traceId = context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}, Path: {Path}", traceId, context.Request.Path);

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = _env.IsDevelopment()
                    ? new
                    {
                        Message = "Internal server error.",
                        TraceId = traceId,
                        Details = ex.ToString()
                    }
                    : (object)new
                    {
                        Message = "Internal server error.",
                        TraceId = traceId
                    };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}
