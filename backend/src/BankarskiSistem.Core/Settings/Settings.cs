namespace BankarskiSistem.Core.Settings
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";
        public const int MinimumKeyBytes = 32;

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationMinutes { get; set; } = 15;
        public int SessionHours { get; set; } = 8;
    }

    public class SecuritySettings
    {
        public const string SectionName = "SecuritySettings";

        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 30;
        public int MaxTransactionsPerDay { get; set; } = 20;
        public decimal DefaultDailyLimit { get; set; } = 100_000m;
        public Dictionary<string, decimal> DailyLimitsByCurrency { get; set; } = new();
        public string BusinessTimeZone { get; set; } = "Europe/Belgrade";
        public bool RequireTwoFactorForStaff { get; set; } = true;

        public decimal GetDailyLimit(string currency) =>
            DailyLimitsByCurrency.TryGetValue(currency, out var limit) ? limit : DefaultDailyLimit;
    }

    public class BankingSettings
    {
        public const string SectionName = "Banking";

        public string BankCode { get; set; } = "205";
        public string[] SupportedCurrencies { get; set; } = ["RSD", "EUR", "USD"];
    }

    public class CustomerProtectionSettings
    {
        public const string SectionName = "CustomerProtection";

        public int ComplaintResponseDays { get; set; } = 15;
        public int ComplaintExtensionDays { get; set; } = 15;
        public int PrivacyRequestResponseDays { get; set; } = 30;
        public int ReauthenticationMinutes { get; set; } = 5;
        public int SessionTouchIntervalSeconds { get; set; } = 60;
        public int SessionCacheSeconds { get; set; } = 15;
        public Dictionary<string, decimal> StrongConfirmationThresholds { get; set; } = new();

        public decimal? GetStrongConfirmationThreshold(string currency) =>
            StrongConfirmationThresholds.TryGetValue(currency, out var threshold) ? threshold : null;
    }

    public class ComplianceSettings
    {
        public const string SectionName = "Compliance";

        public Dictionary<string, decimal> CashReportingThresholds { get; set; } = new();
        public Dictionary<string, decimal> LargePaymentThresholds { get; set; } = new();
        public int RapidPaymentCount { get; set; } = 5;
        public int RapidPaymentWindowMinutes { get; set; } = 10;
        public int StructuringWindowHours { get; set; } = 24;
        public int StructuringMinimumOperations { get; set; } = 3;
    }

    public class RetentionSettings
    {
        public const string SectionName = "Retention";

        public int ReadNotificationsDays { get; set; } = 365;
        public int ExpiredSessionsDays { get; set; } = 90;
        public int FinancialRecordsYears { get; set; } = 10;
        public int AuditLogYears { get; set; } = 10;
    }
}
