using BankarskiSistem.API.Contracts;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class CreateSavingsGoalRequestValidator : AbstractValidator<CreateSavingsGoalRequest>
    {
        public CreateSavingsGoalRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Unesite naziv cilja.").MaximumLength(60);
            RuleFor(x => x.TargetAmount).GreaterThan(0).WithMessage("Ciljani iznos mora biti veći od 0.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Iznos može imati najviše dve decimale.");
            RuleFor(x => x.Currency).NotEmpty().Matches("^[A-Za-z]{3}$").WithMessage("Izaberite valutu.");
        }
    }

    public class UpdateSavingsGoalRequestValidator : AbstractValidator<UpdateSavingsGoalRequest>
    {
        public UpdateSavingsGoalRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Unesite naziv cilja.").MaximumLength(60);
            RuleFor(x => x.TargetAmount).GreaterThan(0).WithMessage("Ciljani iznos mora biti veći od 0.");
        }
    }

    public class SavingsMovementRequestValidator : AbstractValidator<SavingsMovementRequest>
    {
        public SavingsMovementRequestValidator()
        {
            RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("Izaberite račun.");
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Iznos mora biti veći od 0.")
                .Must(value => decimal.Round(value, 2) == value).WithMessage("Iznos može imati najviše dve decimale.");
        }
    }
}
