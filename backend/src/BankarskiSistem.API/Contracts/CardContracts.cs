using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record IssueCardRequest(CardBrand Brand);

    public record CardSettingsRequest(bool OnlineEnabled, bool ContactlessEnabled, bool AbroadEnabled, decimal DailyPurchaseLimit, decimal DailyAtmLimit);

    public record BlockCardRequest(string? Reason);

    public record ReportLostCardRequest(bool Reissue);

    public record SimulateCardPaymentRequest(CardChannel Channel, string MerchantName, decimal Amount, bool IsAbroad, bool Contactless, string? CategoryCode);

    public record CardResponse(
        int Id,
        int AccountId,
        string? AccountNumber,
        string Currency,
        CardBrand Brand,
        string MaskedNumber,
        string Last4,
        string CardholderName,
        string Expiry,
        bool IsExpired,
        CardStatus Status,
        string? StatusReason,
        bool BlockedByBank,
        bool OnlineEnabled,
        bool ContactlessEnabled,
        bool AbroadEnabled,
        decimal DailyPurchaseLimit,
        decimal DailyAtmLimit,
        decimal MaxPurchaseLimit,
        decimal MaxAtmLimit,
        int? ReplacesCardId,
        DateTime CreatedAt,
        DateTime? CancelledAt);

    public record CardAuthorizationResponse(
        int Id,
        int CardId,
        CardChannel Channel,
        string MerchantName,
        string? CategoryCode,
        decimal Amount,
        string Currency,
        bool IsAbroad,
        CardAuthorizationStatus Status,
        string? DeclineReason,
        int? TransactionId,
        DateTime CreatedAt);

    public static class CardMappings
    {
        public static CardResponse ToResponse(this Card card, DateOnly today, decimal maxPurchase, decimal maxAtm) => new(
            card.Id, card.AccountId, card.Account?.AccountNumber, card.Account?.Currency ?? "RSD", card.Brand,
            CardNumbers.Mask(card.Last4), card.Last4, card.CardholderName, $"{card.ExpiryMonth:00}/{card.ExpiryYear % 100:00}",
            card.IsExpiredAt(today), card.Status, card.StatusReason, card.BlockedByBank,
            card.OnlineEnabled, card.ContactlessEnabled, card.AbroadEnabled, card.DailyPurchaseLimit, card.DailyAtmLimit,
            maxPurchase, maxAtm, card.ReplacesCardId, card.CreatedAt, card.CancelledAt);

        public static CardAuthorizationResponse ToResponse(this CardAuthorization a) => new(
            a.Id, a.CardId, a.Channel, a.MerchantName, a.CategoryCode, a.Amount, a.Currency, a.IsAbroad, a.Status, a.DeclineReason, a.TransactionId, a.CreatedAt);
    }
}
