using BankarskiSistem.API.Contracts;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class SetBudgetRequestValidator : AbstractValidator<SetBudgetRequest>
    {
        public SetBudgetRequestValidator()
        {
            RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Izaberite kategoriju.");
            RuleFor(x => x.MonthlyLimit).GreaterThan(0).WithMessage("Mesečni iznos mora biti veći od 0.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Iznos može imati najviše dve decimale.");
        }
    }
}
