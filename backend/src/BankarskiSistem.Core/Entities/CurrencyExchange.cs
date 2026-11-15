using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class ExchangeRate
    {
        public int Id { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal BuyRate { get; set; }
        public decimal MiddleRate { get; set; }
        public decimal SellRate { get; set; }
        public DateTime ValidFrom { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CurrencyExchange
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FromAccountId { get; set; }
        public int ToAccountId { get; set; }
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public decimal FromAmount { get; set; }
        public decimal ToAmount { get; set; }
        public decimal AppliedRate { get; set; }
        public ExchangeRateType RateType { get; set; }
        public decimal MiddleRate { get; set; }
        public decimal SpreadRsd { get; set; }
        public int ExchangeRateId { get; set; }
        public int SourceTransactionId { get; set; }
        public int TargetTransactionId { get; set; }
        public string? IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Account? FromAccount { get; set; }
        public Account? ToAccount { get; set; }
    }
}
