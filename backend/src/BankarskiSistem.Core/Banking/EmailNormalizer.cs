namespace BankarskiSistem.Core.Banking
{
    public static class EmailNormalizer
    {
        public static string Normalize(string email) => email.Trim().ToLowerInvariant();
    }
}
