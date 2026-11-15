using BankarskiSistem.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace BankarskiSistem.API.Security
{
    public class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
    {
        private readonly IDataProtector _protector = provider.CreateProtector("BankarskiSistem.Totp.v1");

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
    }

    public static class RefreshTokenCookie
    {
        public const string Name = "bs_refresh";
        public const string Path = "/api/v1/auth";
        public const string CsrfHeader = "X-BS-Refresh";

        public static void Append(HttpContext context, string token) =>
            context.Response.Cookies.Append(Name, token, Options(context));

        public static void Delete(HttpContext context) =>
            context.Response.Cookies.Delete(Name, Options(context));

        public static string? Read(HttpContext context) =>
            context.Request.Cookies.TryGetValue(Name, out var value) ? value : null;

        private static CookieOptions Options(HttpContext context) => new()
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            IsEssential = true
        };
    }
}
