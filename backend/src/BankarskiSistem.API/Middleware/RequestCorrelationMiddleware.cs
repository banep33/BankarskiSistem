using System.Text.RegularExpressions;

namespace BankarskiSistem.API.Middleware
{
    public partial class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
    {
        public const string HeaderName = "X-Request-Id";

        [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
        private static partial Regex AllowedRequestId();

        public async Task InvokeAsync(HttpContext context)
        {
            var incoming = context.Request.Headers[HeaderName].ToString();
            var requestId = AllowedRequestId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
            context.TraceIdentifier = requestId;
            context.Response.Headers[HeaderName] = requestId;

            using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
                await next(context);
        }
    }
}
