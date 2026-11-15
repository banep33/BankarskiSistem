using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class StandingOrder
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FromAccountId { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientAccountNumber { get; set; } = string.Empty;
        public string? RecipientAddress { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string PaymentCode { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string? ReferenceModel { get; set; }
        public string? ReferenceNumberOnSlip { get; set; }
        public StandingOrderFrequency Frequency { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? MaxExecutions { get; set; }
        public int OccurrenceIndex { get; set; }
        public DateOnly? NextExecutionDate { get; set; }
        public StandingOrderStatus Status { get; set; } = StandingOrderStatus.Aktivan;
        public int ExecutionsCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public string? LastFailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Account? FromAccount { get; set; }
        public ICollection<StandingOrderExecution> Executions { get; set; } = new List<StandingOrderExecution>();
    }

    public class StandingOrderExecution
    {
        public int Id { get; set; }
        public int StandingOrderId { get; set; }
        public DateOnly ScheduledFor { get; set; }
        public StandingOrderExecutionStatus Status { get; set; } = StandingOrderExecutionStatus.Pokrenut;
        public int? PaymentOrderId { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public PaymentOrder? PaymentOrder { get; set; }
    }
}
