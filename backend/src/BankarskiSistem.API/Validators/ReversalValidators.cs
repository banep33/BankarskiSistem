using BankarskiSistem.API.Contracts;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class CreateReversalRequestValidator : AbstractValidator<CreateReversalRequest>
    {
        public CreateReversalRequestValidator()
        {
            RuleFor(x => x.Reason).NotEmpty().WithMessage("Razlog storniranja je obavezan.")
                .MinimumLength(10).WithMessage("Razlog mora imati najmanje 10 karaktera.")
                .MaximumLength(500);
        }
    }

    public class ReviewReversalRequestValidator : AbstractValidator<ReviewReversalRequest>
    {
        public ReviewReversalRequestValidator()
        {
            RuleFor(x => x.Note).MaximumLength(500);
        }
    }
}
