using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class Account
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "RSD";
        public AccountKind Kind { get; set; } = AccountKind.Tekuci;
        public AccountStatus Status { get; set; } = AccountStatus.Aktivan;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public bool IsActive => Status == AccountStatus.Aktivan;
    }
}
