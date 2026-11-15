namespace BankarskiSistem.API.Security
{
    public static class Policies
    {
        public const string Administrator = "AdministratorOnly";
        public const string Banker = "BankarOnly";
        public const string Customer = "KorisnikOnly";
        public const string Staff = "BankarIliAdministrator";
    }

    public static class RateLimitPolicies
    {
        public const string Login = "login";
        public const string Payments = "payments";
        public const string Sensitive = "sensitive";
        public const string Submissions = "submissions";
    }
}
