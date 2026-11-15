using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class PaymentOrder
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int FromAccountId { get; set; }
        public int? ToAccountId { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientAccountNumber { get; set; } = string.Empty;
        public string? RecipientAddress { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RSD";
        public decimal FeeAmount { get; set; }
        public int? FeeScheduleId { get; set; }
        public string PaymentCode { get; set; } = "289";
        public string Purpose { get; set; } = string.Empty;
        public string? ReferenceModel { get; set; }
        public string? ReferenceNumber { get; set; }
        public PaymentOrderStatus Status { get; set; }
        public DateOnly? ScheduledFor { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public string? RejectionReason { get; set; }
        public int? TransactionId { get; set; }
        public string? IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Account? FromAccount { get; set; }
        public Account? ToAccount { get; set; }
        public Transaction? Transaction { get; set; }

        public bool IsInternal => ToAccountId.HasValue;
        public bool CanBeCancelled => Status == PaymentOrderStatus.Zakazan;
    }

    public class Beneficiary
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? RecipientAddress { get; set; }
        public string? PaymentCode { get; set; }
        public string? Purpose { get; set; }
        public string? ReferenceModel { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }

    public class LegalDocument
    {
        public int Id { get; set; }
        public LegalDocumentType DocumentType { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
        public int? PublishedByUserId { get; set; }
    }

    public class UserConsent
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int LegalDocumentId { get; set; }
        public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }

        public LegalDocument? LegalDocument { get; set; }
    }
}
