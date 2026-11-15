using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class Transaction
    {
        public int Id { get; set; }
        public int? FromAccountId { get; set; }
        public int? ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public decimal FeeAmount { get; set; }
        public string Currency { get; set; } = "RSD";
        public decimal? FromBalanceAfter { get; set; }
        public decimal? ToBalanceAfter { get; set; }
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.NaCekanju;
        public string? Description { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int? PerformedByUserId { get; set; }
        public string? IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Account? FromAccount { get; set; }
        public Account? ToAccount { get; set; }
        public User? PerformedBy { get; set; }
        public PaymentOrder? PaymentOrder { get; set; }
    }
}
