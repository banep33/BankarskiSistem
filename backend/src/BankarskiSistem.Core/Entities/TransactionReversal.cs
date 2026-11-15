using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class TransactionReversal
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int OriginalTransactionId { get; set; }
        public int? ReversalTransactionId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ReversalStatus Status { get; set; } = ReversalStatus.Podnet;
        public int RequestedByUserId { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public Transaction? OriginalTransaction { get; set; }
        public User? RequestedBy { get; set; }
        public User? ReviewedBy { get; set; }
    }
}
