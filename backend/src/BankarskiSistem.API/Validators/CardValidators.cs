using BankarskiSistem.API.Contracts;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class IssueCardRequestValidator : AbstractValidator<IssueCardRequest>
    {
        public IssueCardRequestValidator()
        {
            RuleFor(x => x.Brand).IsInEnum().WithMessage("Izaberite vrstu kartice.");
        }
    }

    public class CardSettingsRequestValidator : AbstractValidator<CardSettingsRequest>
    {
        public CardSettingsRequestValidator()
        {
            RuleFor(x => x.DailyPurchaseLimit).GreaterThanOrEqualTo(0).WithMessage("Limit ne može biti negativan.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Limit može imati najviše dve decimale.");
            RuleFor(x => x.DailyAtmLimit).GreaterThanOrEqualTo(0).WithMessage("Limit ne može biti negativan.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Limit može imati najviše dve decimale.");
        }
    }

    public class BlockCardRequestValidator : AbstractValidator<BlockCardRequest>
    {
        public BlockCardRequestValidator()
        {
            RuleFor(x => x.Reason).MaximumLength(200);
        }
    }

    public class SimulateCardPaymentRequestValidator : AbstractValidator<SimulateCardPaymentRequest>
    {
        public SimulateCardPaymentRequestValidator()
        {
            RuleFor(x => x.Channel).IsInEnum().WithMessage("Izaberite način plaćanja.");
            RuleFor(x => x.MerchantName).NotEmpty().WithMessage("Unesite naziv trgovca.").MaximumLength(80);
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Iznos može imati najviše dve decimale.");
            RuleFor(x => x.CategoryCode).MaximumLength(30);
        }
    }
}
