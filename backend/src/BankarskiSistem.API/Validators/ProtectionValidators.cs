using BankarskiSistem.API.Contracts;
using BankarskiSistem.Core.Enums;
using FluentValidation;

namespace BankarskiSistem.API.Validators
{
    public class ReauthenticateRequestValidator : AbstractValidator<ReauthenticateRequest>
    {
        public ReauthenticateRequestValidator()
        {
            RuleFor(x => x.Password).NotEmpty().WithMessage("Unesite lozinku.").MaximumLength(ValidationRules.PasswordMaxLength);
        }
    }

    public class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
    {
        public UpdatePreferencesRequestValidator()
        {
            RuleFor(x => x.ExtraConfirmationThreshold)
                .GreaterThan(0).When(x => x.ExtraConfirmationThreshold.HasValue)
                .WithMessage("Prag za dodatnu potvrdu mora biti veći od nule.");
        }
    }

    public class SubmitComplaintRequestValidator : AbstractValidator<SubmitComplaintRequest>
    {
        public SubmitComplaintRequestValidator()
        {
            RuleFor(x => x.Category).IsInEnum().WithMessage("Izaberite kategoriju prigovora.");
            RuleFor(x => x.Subject).NotEmpty().WithMessage("Naslov prigovora je obavezan.")
                .MaximumLength(150).WithMessage("Naslov ne sme biti duži od 150 karaktera.");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Opišite problem.")
                .MinimumLength(20).WithMessage("Opis mora imati najmanje 20 karaktera kako bismo razumeli problem.")
                .MaximumLength(5000).WithMessage("Opis ne sme biti duži od 5000 karaktera.");
        }
    }

    public class ComplaintMessageRequestValidator : AbstractValidator<ComplaintMessageRequest>
    {
        public ComplaintMessageRequestValidator()
        {
            RuleFor(x => x.Message).NotEmpty().WithMessage("Poruka ne može biti prazna.")
                .MaximumLength(5000).WithMessage("Poruka ne sme biti duža od 5000 karaktera.");
        }
    }

    public class ChangeComplaintStatusRequestValidator : AbstractValidator<ChangeComplaintStatusRequest>
    {
        public ChangeComplaintStatusRequestValidator()
        {
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.Note).MaximumLength(5000);
        }
    }

    public class ExtendComplaintDeadlineRequestValidator : AbstractValidator<ExtendComplaintDeadlineRequest>
    {
        public ExtendComplaintDeadlineRequestValidator()
        {
            RuleFor(x => x.Reason).NotEmpty().WithMessage("Razlog produženja je obavezan.").MaximumLength(500);
        }
    }

    public class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
    {
        public SubmitFeedbackRequestValidator()
        {
            RuleFor(x => x.SurveyType).IsInEnum();
            RuleFor(x => x.Score)
                .InclusiveBetween(0, 10).When(x => x.SurveyType == SurveyType.NPS).WithMessage("NPS ocena mora biti od 0 do 10.")
                .InclusiveBetween(1, 5).When(x => x.SurveyType == SurveyType.CSAT).WithMessage("CSAT ocena mora biti od 1 do 5.")
                .InclusiveBetween(1, 7).When(x => x.SurveyType == SurveyType.CES).WithMessage("CES ocena mora biti od 1 do 7.");
            RuleFor(x => x.Comment).MaximumLength(1000);
            RuleFor(x => x.Context).NotEmpty().Matches("^[a-z0-9:_-]{2,60}$").WithMessage("Neispravan kontekst ocene.");
        }
    }

    public class FaqArticleRequestValidator : AbstractValidator<FaqArticleRequest>
    {
        public FaqArticleRequestValidator()
        {
            RuleFor(x => x.Category).NotEmpty().MaximumLength(60);
            RuleFor(x => x.Question).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Answer).NotEmpty().MaximumLength(10000);
        }
    }

    public class SubmitPrivacyRequestValidator : AbstractValidator<SubmitPrivacyRequest>
    {
        public SubmitPrivacyRequestValidator()
        {
            RuleFor(x => x.RequestType).IsInEnum().WithMessage("Izaberite vrstu zahteva.");
            RuleFor(x => x.Details).NotEmpty().WithMessage("Opišite zahtev.")
                .MinimumLength(10).WithMessage("Opis zahteva mora imati najmanje 10 karaktera.")
                .MaximumLength(5000);
        }
    }

    public class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
    {
        public TwoFactorLoginRequestValidator()
        {
            RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2000);
            RuleFor(x => x.Code).NotEmpty().WithMessage("Unesite kod.").MaximumLength(20);
        }
    }

    public class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
    {
        public TwoFactorCodeRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().WithMessage("Unesite kod.").MaximumLength(20);
        }
    }

    public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
    {
        public ForgotPasswordRequestValidator()
        {
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.NewPassword).StrongPassword();
            RuleFor(x => x.ConfirmNewPassword).Equal(x => x.NewPassword).WithMessage("Lozinke se ne poklapaju.");
        }
    }

    public class ReviewPasswordResetRequestValidator : AbstractValidator<ReviewPasswordResetRequest>
    {
        public ReviewPasswordResetRequestValidator()
        {
            RuleFor(x => x.Note).MaximumLength(500).WithMessage("Napomena ne sme biti duža od 500 karaktera.");
        }
    }

    public class RespondPrivacyRequestValidator : AbstractValidator<RespondPrivacyRequest>
    {
        public RespondPrivacyRequestValidator()
        {
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.Response).MaximumLength(10000);
        }
    }

    public class ReviewComplianceAlertRequestValidator : AbstractValidator<ReviewComplianceAlertRequest>
    {
        public ReviewComplianceAlertRequestValidator()
        {
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.Note).NotEmpty().WithMessage("Obrazloženje je obavezno.").MaximumLength(1000);
        }
    }

    public class UpdateKycRequestValidator : AbstractValidator<UpdateKycRequest>
    {
        public UpdateKycRequestValidator()
        {
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.RiskRating).IsInEnum();
        }
    }
}
