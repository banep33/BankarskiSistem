using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class Card
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public CardBrand Brand { get; set; }
        public string PanHash { get; set; } = string.Empty;
        public string Last4 { get; set; } = string.Empty;
        public string CardholderName { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public CardStatus Status { get; set; } = CardStatus.Aktivna;
        public bool OnlineEnabled { get; set; } = true;
        public bool ContactlessEnabled { get; set; } = true;
        public bool AbroadEnabled { get; set; }
        public decimal DailyPurchaseLimit { get; set; }
        public decimal DailyAtmLimit { get; set; }
        public string? StatusReason { get; set; }
        public bool BlockedByBank { get; set; }
        public int? ReplacesCardId { get; set; }
        public int? IssuedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CancelledAt { get; set; }

        public Account? Account { get; set; }

        public bool IsExpiredAt(DateOnly today) => new DateOnly(ExpiryYear, ExpiryMonth, 1).AddMonths(1) <= today;
    }

    public class CardAuthorization
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public CardChannel Channel { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string? CategoryCode { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RSD";
        public bool IsAbroad { get; set; }
        public CardAuthorizationStatus Status { get; set; }
        public string? DeclineReason { get; set; }
        public int? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
