using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    public class AuthController(
        IAuthService authService,
        ILegalService legalService,
        IPasswordResetService passwordResetService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            try
            {
                var result = await authService.LoginAsync(request.Email, request.Password);
                await auditLog.LogAsync(new AuditEntry(result.User.Id, "Uspesna prijava", "Auth", result.User.Id));
                var pending = await legalService.GetPendingDocumentsAsync(result.User.Id);
                return Ok(result.ToResponse(pending));
            }
            catch (AuthenticationFailedException exception)
            {
                await auditLog.LogAsync(new AuditEntry(
                    exception.UserId,
                    StatisticsService.FailedLoginAction,
                    "Auth",
                    exception.UserId,
                    NewValue: $"Email: {EmailNormalizer.Normalize(request.Email)}",
                    IsSuccess: false,
                    ErrorMessage: exception.GetType().Name));
                throw;
            }
        }

        [HttpPost("password-reset")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Submissions)]
        public async Task<IActionResult> RequestPasswordReset(ForgotPasswordRequest request)
        {
            var created = await passwordResetService.SubmitAsync(request.Email, request.NewPassword);
            if (created != null)
                await auditLog.LogAsync(new AuditEntry(created.UserId, "Zahtev za novu lozinku", "PasswordReset", created.Id, created.ReferenceNumber));

            return Accepted(new
            {
                message = "Ako nalog sa tom adresom postoji, zahtev je prosleđen banci. Nova lozinka važi tek kada je banka odobri, nakon provere identiteta."
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var sessionId = User.FindSessionId();
            if (sessionId.HasValue)
            {
                var userId = CurrentActor.UserId;
                await authService.LogoutAsync(userId, sessionId.Value);
                await auditLog.LogAsync(new AuditEntry(userId, "Odjava", "Auth", userId));
            }
            return NoContent();
        }
    }
}
