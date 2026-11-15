using BankarskiSistem.Core.Interfaces;

namespace BankarskiSistem.API.Security
{
    public class HttpRequestContext : IRequestContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    }
}
