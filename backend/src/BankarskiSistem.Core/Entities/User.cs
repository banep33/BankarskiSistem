using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsLocked { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }
        public int TokenVersion { get; set; }
        public string? TotpSecretProtected { get; set; }
        public DateTime? TotpEnabledAt { get; set; }
        public long? TotpLastUsedStep { get; set; }
        public KycStatus KycStatus { get; set; } = KycStatus.NijeVerifikovan;
        public DateTime? KycVerifiedAt { get; set; }
        public int? KycVerifiedByUserId { get; set; }
        public RiskLevel RiskRating { get; set; } = RiskLevel.Nizak;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Account> Accounts { get; set; } = new List<Account>();

        public string FullName => $"{FirstName} {LastName}";

        public bool TwoFactorEnabled => TotpEnabledAt != null;

        public void InvalidateTokens() => TokenVersion++;
    }
}
