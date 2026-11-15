using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Models
{
    public record TransactionFilter
    {
        public int? OwnerUserId { get; init; }
        public int? AccountId { get; init; }
        public TransactionType? Type { get; init; }
        public TransactionStatus? Status { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public string? Search { get; init; }
    }

    public record PaymentOrderFilter
    {
        public PaymentOrderStatus? Status { get; init; }
        public int? AccountId { get; init; }
    }

    public record UserFilter
    {
        public UserRole? Role { get; init; }
        public string? Search { get; init; }
        public bool? IsActive { get; init; }
    }

    public record AuditLogFilter
    {
        public int? UserId { get; init; }
        public bool? OnlyFailures { get; init; }
        public string? Search { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
    }
}
