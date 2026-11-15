using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record CreateReversalRequest(string Reason);

    public record ReviewReversalRequest(string? Note);

    public record TransactionReversalResponse(
        int Id,
        string ReferenceNumber,
        int OriginalTransactionId,
        string? OriginalReference,
        TransactionType? OriginalType,
        decimal? Amount,
        string? Currency,
        string? FromAccountNumber,
        string? ToAccountNumber,
        DateTime? OriginalCreatedAt,
        string Reason,
        ReversalStatus Status,
        string? RequestedBy,
        string? ReviewedBy,
        string? ReviewNote,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        int? ReversalTransactionId);

    public static class ReversalMappings
    {
        public static TransactionReversalResponse ToResponse(this TransactionReversal reversal) =>
            new(reversal.Id, reversal.ReferenceNumber, reversal.OriginalTransactionId, reversal.OriginalTransaction?.ReferenceNumber,
                reversal.OriginalTransaction?.Type, reversal.OriginalTransaction?.Amount, reversal.OriginalTransaction?.Currency,
                reversal.OriginalTransaction?.FromAccount?.AccountNumber, reversal.OriginalTransaction?.ToAccount?.AccountNumber,
                reversal.OriginalTransaction?.CreatedAt, reversal.Reason, reversal.Status, reversal.RequestedBy?.FullName,
                reversal.ReviewedBy?.FullName, reversal.ReviewNote, reversal.CreatedAt, reversal.ReviewedAt, reversal.ReversalTransactionId);
    }
}
