using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Models
{
    public record Actor(int UserId, UserRole Role)
    {
        public bool IsAdministrator => Role == UserRole.Administrator;
        public bool IsBanker => Role == UserRole.Bankar;
        public bool IsCustomer => Role == UserRole.Korisnik;
        public bool IsStaff => IsAdministrator || IsBanker;
    }

    public record CreateUserCommand(string FirstName, string LastName, string Email, string Password, UserRole Role);

    public record UpdateUserCommand(string FirstName, string LastName, string Email, UserRole Role);

    public record UpdateProfileCommand(string FirstName, string LastName);

    public record DepositCommand(int AccountId, decimal Amount, string? Description, string? IdempotencyKey);

    public record WithdrawCommand(int AccountId, decimal Amount, string? Description, string? IdempotencyKey);

    public record CreatePaymentOrderCommand(
        int FromAccountId,
        string RecipientName,
        string RecipientAccountNumber,
        string? RecipientAddress,
        decimal Amount,
        string PaymentCode,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        DateOnly? ScheduledFor,
        string? SaveAsBeneficiary,
        string? IdempotencyKey,
        bool StrongConfirmationProvided = false);

    public record CardSettings(bool OnlineEnabled, bool ContactlessEnabled, bool AbroadEnabled, decimal DailyPurchaseLimit, decimal DailyAtmLimit);

    public record CardAuthorizationRequest(CardChannel Channel, string MerchantName, decimal Amount, bool IsAbroad, bool Contactless, string? CategoryCode);

    public record CurrencyExchangeCommand(int FromAccountId, int ToAccountId, decimal Amount, bool AmountIsTarget, int? ExpectedRateId, string? IdempotencyKey);

    public record ExchangeRateInput(string Currency, decimal BuyRate, decimal MiddleRate, decimal SellRate);

    public record CreateStandingOrderCommand(
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

    public record UpdateStandingOrderCommand(
        decimal Amount,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        DateOnly? EndDate,
        int? MaxExecutions);

    public record BeneficiaryCommand(
        string Nickname,
        string RecipientName,
        string AccountNumber,
        string? RecipientAddress,
        string? PaymentCode,
        string? Purpose,
        string? ReferenceModel,
        string? ReferenceNumber);

    public record PublishLegalDocumentCommand(LegalDocumentType DocumentType, string Version, string Title, string Content);

    public record AuditEntry(
        int? UserId,
        string Action,
        string? EntityType = null,
        int? EntityId = null,
        string? NewValue = null,
        string? OldValue = null,
        bool IsSuccess = true,
        string? ErrorMessage = null);
}
