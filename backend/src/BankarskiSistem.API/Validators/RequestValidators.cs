using BankarskiSistem.API.Contracts;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Infrastructure.Services.Payments;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    internal static class ValidationRules
    {
        public const int NameMaxLength = 100;
        public const int EmailMaxLength = 255;
        public const int DescriptionMaxLength = 255;
        public const int PurposeMaxLength = 105;
        public const int AddressMaxLength = 150;
        public const int NicknameMaxLength = 60;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 72;
        public const string PersonNamePattern = @"^[\p{L}][\p{L}\p{M}' .-]*$";
        public const string RecipientNamePattern = @"^[\p{L}\p{N}][\p{L}\p{M}\p{N}' .,&()/-]*$";

        public static IRuleBuilderOptions<T, string> ValidPersonName<T>(this IRuleBuilder<T, string> rule, string fieldName) =>
            rule.NotEmpty().WithMessage($"{fieldName} je obavezno.")
                .MaximumLength(NameMaxLength).WithMessage($"{fieldName} ne sme biti duže od {NameMaxLength} karaktera.")
                .Matches(PersonNamePattern).WithMessage($"{fieldName} sadrži nedozvoljene karaktere.");

        public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
            rule.NotEmpty().WithMessage("Email je obavezan.")
                .MaximumLength(EmailMaxLength).WithMessage($"Email ne sme biti duži od {EmailMaxLength} karaktera.")
                .EmailAddress().WithMessage("Neispravan format email adrese.");

        public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
            rule.NotEmpty().WithMessage("Lozinka je obavezna.")
                .MinimumLength(PasswordMinLength).WithMessage($"Lozinka mora imati najmanje {PasswordMinLength} karaktera.")
                .MaximumLength(PasswordMaxLength).WithMessage($"Lozinka ne sme imati više od {PasswordMaxLength} karaktera.")
                .Matches("[A-Z]").WithMessage("Lozinka mora sadržati najmanje jedno veliko slovo.")
                .Matches("[a-z]").WithMessage("Lozinka mora sadržati najmanje jedno malo slovo.")
                .Matches("[0-9]").WithMessage("Lozinka mora sadržati najmanje jedan broj.");

        public static IRuleBuilderOptions<T, decimal> ValidMoneyAmount<T>(this IRuleBuilder<T, decimal> rule) =>
            rule.GreaterThan(0).WithMessage("Iznos mora biti veći od 0.")
                .LessThanOrEqualTo(MoneyRules.MaxSingleAmount)
                    .WithMessage($"Iznos ne sme biti veći od {MoneyRules.MaxSingleAmount:N0}.")
                .Must(amount => decimal.Round(amount, 2) == amount)
                    .WithMessage("Iznos može imati najviše dve decimale.");

        public static IRuleBuilderOptions<T, string> ValidAccountNumber<T>(this IRuleBuilder<T, string> rule) =>
            rule.NotEmpty().WithMessage("Broj računa primaoca je obavezan.")
                .Must(number => SerbianAccountNumber.TryParse(number, out _))
                    .WithMessage("Broj računa mora biti u formatu 205-0000001234567-89 (ili kraće 205-1234567-89).");

        public static IRuleBuilderOptions<T, string> ValidRecipientName<T>(this IRuleBuilder<T, string> rule) =>
            rule.NotEmpty().WithMessage("Naziv primaoca je obavezan.")
                .MaximumLength(NameMaxLength).WithMessage($"Naziv primaoca ne sme biti duži od {NameMaxLength} karaktera.")
                .Matches(RecipientNamePattern).WithMessage("Naziv primaoca sadrži nedozvoljene karaktere.");
    }

    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Password).NotEmpty().WithMessage("Lozinka je obavezna.")
                .MaximumLength(ValidationRules.PasswordMaxLength);
        }
    }

    public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
    {
        public ChangePasswordRequestValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Trenutna lozinka je obavezna.");
            RuleFor(x => x.NewPassword).StrongPassword()
                .NotEqual(x => x.CurrentPassword).WithMessage("Nova lozinka mora biti različita od trenutne.");
            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("Lozinke se ne poklapaju.");
        }
    }

    public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(x => x.FirstName).ValidPersonName("Ime");
            RuleFor(x => x.LastName).ValidPersonName("Prezime");
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Password).StrongPassword();
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Lozinke se ne poklapaju.");
            RuleFor(x => x.Role).IsInEnum().WithMessage("Neispravna uloga korisnika.");
        }
    }

    public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserRequestValidator()
        {
            RuleFor(x => x.FirstName).ValidPersonName("Ime");
            RuleFor(x => x.LastName).ValidPersonName("Prezime");
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Role).IsInEnum().WithMessage("Neispravna uloga korisnika.");
        }
    }

    public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
    {
        public UpdateProfileRequestValidator()
        {
            RuleFor(x => x.FirstName).ValidPersonName("Ime");
            RuleFor(x => x.LastName).ValidPersonName("Prezime");
        }
    }

    public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
    {
        public CreateAccountRequestValidator()
        {
            RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Neispravan ID korisnika.");
            RuleFor(x => x.Currency).NotEmpty().WithMessage("Valuta je obavezna.")
                .Matches("^[A-Za-z]{3}$").WithMessage("Valuta mora biti troslovni ISO kod (npr. RSD, EUR).");
        }
    }

    public class ChangeAccountStatusRequestValidator : AbstractValidator<ChangeAccountStatusRequest>
    {
        public ChangeAccountStatusRequestValidator()
        {
            RuleFor(x => x.Status).IsInEnum().WithMessage("Status mora biti Aktivan, Neaktivan ili Blokiran.");
        }
    }

    public class MoneyRequestValidator : AbstractValidator<MoneyRequest>
    {
        public MoneyRequestValidator()
        {
            RuleFor(x => x.Amount).ValidMoneyAmount();
            RuleFor(x => x.Description).MaximumLength(ValidationRules.DescriptionMaxLength)
                .WithMessage($"Opis ne sme biti duži od {ValidationRules.DescriptionMaxLength} karaktera.");
        }
    }

    public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
    {
        public CreatePaymentRequestValidator()
        {
            RuleFor(x => x.FromAccountId).GreaterThan(0).WithMessage("Izaberite račun sa kog plaćate.");
            RuleFor(x => x.RecipientName).ValidRecipientName();
            RuleFor(x => x.RecipientAccountNumber).ValidAccountNumber();
            RuleFor(x => x.RecipientAddress).MaximumLength(ValidationRules.AddressMaxLength)
                .WithMessage($"Adresa ne sme biti duža od {ValidationRules.AddressMaxLength} karaktera.");
            RuleFor(x => x.Amount).ValidMoneyAmount();
            RuleFor(x => x.PaymentCode).Must(PaymentCodes.IsValidFormat)
                .WithMessage("Šifra plaćanja mora biti trocifren broj koji počinje sa 1 ili 2 (npr. 289).");
            RuleFor(x => x.Purpose).NotEmpty().WithMessage("Svrha plaćanja je obavezna.")
                .MaximumLength(ValidationRules.PurposeMaxLength)
                    .WithMessage($"Svrha plaćanja ne sme biti duža od {ValidationRules.PurposeMaxLength} karaktera.");
            RuleFor(x => x)
                .Must(x => PaymentReference.Validate(Blank(x.ReferenceModel), Blank(x.ReferenceNumber)) == null)
                .WithMessage(x => PaymentReference.Validate(Blank(x.ReferenceModel), Blank(x.ReferenceNumber)) ?? string.Empty)
                .WithName("Poziv na broj");
            RuleFor(x => x.SaveAsBeneficiary).MaximumLength(ValidationRules.NicknameMaxLength)
                .WithMessage($"Naziv primaoca za čuvanje ne sme biti duži od {ValidationRules.NicknameMaxLength} karaktera.");
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class BeneficiaryRequestValidator : AbstractValidator<BeneficiaryRequest>
    {
        public BeneficiaryRequestValidator()
        {
            RuleFor(x => x.Nickname).NotEmpty().WithMessage("Naziv za prepoznavanje je obavezan.")
                .MaximumLength(ValidationRules.NicknameMaxLength)
                    .WithMessage($"Naziv ne sme biti duži od {ValidationRules.NicknameMaxLength} karaktera.");
            RuleFor(x => x.RecipientName).ValidRecipientName();
            RuleFor(x => x.AccountNumber).ValidAccountNumber();
            RuleFor(x => x.RecipientAddress).MaximumLength(ValidationRules.AddressMaxLength);
            RuleFor(x => x.Purpose).MaximumLength(ValidationRules.PurposeMaxLength);
            RuleFor(x => x.PaymentCode).Must(code => string.IsNullOrWhiteSpace(code) || PaymentCodes.IsValidFormat(code))
                .WithMessage("Šifra plaćanja mora biti trocifren broj koji počinje sa 1 ili 2.");
        }
    }

    public class AcceptDocumentsRequestValidator : AbstractValidator<AcceptDocumentsRequest>
    {
        public AcceptDocumentsRequestValidator()
        {
            RuleFor(x => x.DocumentIds).NotEmpty().WithMessage("Izaberite dokumente koje prihvatate.");
        }
    }

    public class PublishLegalDocumentRequestValidator : AbstractValidator<PublishLegalDocumentRequest>
    {
        public PublishLegalDocumentRequestValidator()
        {
            RuleFor(x => x.DocumentType).IsInEnum();
            RuleFor(x => x.Version).NotEmpty().MaximumLength(20).Matches(@"^[0-9A-Za-z.\-]+$")
                .WithMessage("Verzija sme sadržati samo slova, cifre, tačku i crticu.");
            RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Content).NotEmpty().MaximumLength(200_000);
        }
    }
}
