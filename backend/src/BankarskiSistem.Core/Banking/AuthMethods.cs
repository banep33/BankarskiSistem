using System.Security.Claims;

namespace BankarskiSistem.Core.Banking
{
    public static class AuthMethods
    {
        public const string ClaimType = "amr";
        public const string MappedClaimType = "http://schemas.microsoft.com/claims/authnmethodsreferences";
        public const string Password = "pwd";
        public const string MultiFactor = "mfa";

        public static string For(bool multiFactorVerified) => multiFactorVerified ? MultiFactor : Password;

        public static bool IsMultiFactor(ClaimsPrincipal principal) =>
            principal.Claims.Any(c => (c.Type == ClaimType || c.Type == MappedClaimType) && c.Value == MultiFactor);
    }
}
