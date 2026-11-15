using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record CreateStandingOrderRequest(
        int FromAccountId,
        string RecipientName,
        string RecipientAccountNumber,
        string? RecipientAddress,
        decimal Amount,
        string PaymentCode,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        StandingOrderFrequency Frequency,
        DateOnly StartDate,
        DateOnly? EndDate,
        int? MaxExecutions);

    public record UpdateStandingOrderRequest(
        decimal Amount,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        DateOnly? EndDate,
        int? MaxExecutions);

    public record StandingOrderResponse(
        int Id,
        string ReferenceNumber,
        int FromAccountId,
        string? FromAccountNumber,
        string RecipientName,
        string RecipientAccountNumber,
        string? RecipientAddress,
        decimal Amount,
        string Currency,
        string PaymentCode,
        string Purpose,
        string? ReferenceModel,
        string? PaymentReferenceNumber,
        StandingOrderFrequency Frequency,
        DateOnly StartDate,
        DateOnly? EndDate,
        int? MaxExecutions,
        DateOnly? NextExecutionDate,
        IReadOnlyList<DateOnly> UpcomingDates,
        StandingOrderStatus Status,
        int ExecutionsCount,
        int ConsecutiveFailures,
        DateTime? LastExecutedAt,
        string? LastFailureReason,
        DateTime CreatedAt);

    public record StandingOrderExecutionResponse(
        int Id,
        DateOnly ScheduledFor,
        StandingOrderExecutionStatus Status,
        int? PaymentOrderId,
        string? TransactionReference,
        decimal? FeeAmount,
        string? FailureReason,
        DateTime? CompletedAt);

    public record StandingOrderDetailResponse(StandingOrderResponse Order, IReadOnlyList<StandingOrderExecutionResponse> Executions);

    public static class StandingOrderMappings
    {
        public static StandingOrderResponse ToResponse(this StandingOrder order) =>
            new(order.Id, order.ReferenceNumber, order.FromAccountId, order.FromAccount?.AccountNumber, order.RecipientName,
                order.RecipientAccountNumber, order.RecipientAddress, order.Amount, order.Currency, order.PaymentCode, order.Purpose,
                order.ReferenceModel, order.ReferenceNumberOnSlip, order.Frequency, order.StartDate, order.EndDate, order.MaxExecutions,
                order.NextExecutionDate,
                order.Status is StandingOrderStatus.Aktivan or StandingOrderStatus.Pauziran
                    ? StandingOrderSchedule.Upcoming(order.StartDate, order.Frequency, order.OccurrenceIndex, order.EndDate, order.MaxExecutions, 3)
                        .Where(d => order.NextExecutionDate == null || d >= order.NextExecutionDate.Value).ToList()
                    : [],
                order.Status, order.ExecutionsCount, order.ConsecutiveFailures, order.LastExecutedAt, order.LastFailureReason,
                order.CreatedAt);

        public static StandingOrderDetailResponse ToDetailResponse(this StandingOrder order) =>
            new(order.ToResponse(), order.Executions
                .OrderByDescending(e => e.ScheduledFor)
                .Select(e => new StandingOrderExecutionResponse(e.Id, e.ScheduledFor, e.Status, e.PaymentOrderId,
                    e.PaymentOrder?.Transaction?.ReferenceNumber, e.PaymentOrder?.FeeAmount, e.FailureReason, e.CompletedAt))
                .ToList());
    }
}
