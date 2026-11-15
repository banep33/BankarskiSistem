using BankarskiSistem.API.Contracts;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class CurrencyExchangeRequestValidator : AbstractValidator<CurrencyExchangeRequest>
    {
        public CurrencyExchangeRequestValidator()
        {
            RuleFor(x => x.FromAccountId).GreaterThan(0).WithMessage("Izaberite račun sa kog menjate.");
            RuleFor(x => x.ToAccountId).GreaterThan(0).WithMessage("Izaberite račun na koji menjate.")
                .NotEqual(x => x.FromAccountId).WithMessage("Izaberite dva različita računa.");
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.")
                .Must(amount => decimal.Round(amount, 2) == amount).WithMessage("Iznos može imati najviše dve decimale.");
        }
    }

    public class PublishExchangeRatesRequestValidator : AbstractValidator<PublishExchangeRatesRequest>
    {
        public PublishExchangeRatesRequestValidator()
        {
            RuleFor(x => x.Rates).NotEmpty().WithMessage("Unesite bar jednu valutu.");
            RuleForEach(x => x.Rates).ChildRules(rate =>
            {
                rate.RuleFor(r => r.Currency).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Oznaka valute ima tri slova.");
                rate.RuleFor(r => r.BuyRate).GreaterThan(0).WithMessage("Kupovni kurs mora biti veći od 0.");
                rate.RuleFor(r => r.MiddleRate).GreaterThan(r => r.BuyRate).WithMessage("Srednji kurs mora biti veći od kupovnog.");
                rate.RuleFor(r => r.SellRate).GreaterThan(r => r.MiddleRate).WithMessage("Prodajni kurs mora biti veći od srednjeg.");
            });
        }
    }
}
