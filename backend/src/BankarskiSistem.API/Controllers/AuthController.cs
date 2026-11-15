using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Middleware;
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
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                var outcome = await authService.LoginAsync(request.Email, request.Password);
                if (outcome.Challenge != null)
                    return Ok(new TwoFactorChallengeResponse(true, outcome.Challenge.Token, outcome.Challenge.ExpiresAt));

                return Ok(await CompleteLoginAsync(outcome.Session!));
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

        [HttpPost("login/two-factor")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<ActionResult<LoginResponse>> LoginTwoFactor(TwoFactorLoginRequest request)
        {
            try
            {
                var result = await authService.CompleteTwoFactorAsync(request.ChallengeToken, request.Code);
                return Ok(await CompleteLoginAsync(result));
            }
            catch (AuthenticationFailedException exception)
            {
                await auditLog.LogAsync(new AuditEntry(
                    exception.UserId,
                    StatisticsService.FailedLoginAction,
                    "Auth",
                    exception.UserId,
                    NewValue: "Pogresan kod dvostepene prijave",
                    IsSuccess: false,
                    ErrorMessage: exception.GetType().Name));
                throw;
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Refresh()
        {
            if (!Request.Headers.ContainsKey(RefreshTokenCookie.CsrfHeader))
                throw new ForbiddenException("Zahtev nije dozvoljen.");

            try
            {
                var result = await authService.RefreshAsync(RefreshTokenCookie.Read(HttpContext) ?? string.Empty);
                if (result.RefreshToken != null)
                    RefreshTokenCookie.Append(HttpContext, result.RefreshToken);
                var pending = await legalService.GetPendingDocumentsAsync(result.User.Id);
                return Ok(result.ToResponse(pending));
            }
            catch (InvalidRefreshTokenException exception)
            {
                RefreshTokenCookie.Delete(HttpContext);
                var problem = ProblemWriter.Create(HttpContext, StatusCodes.Status401Unauthorized, "authentication-failed",
                    "Prijava nije uspela", exception.Message, []);
                return new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    ContentTypes = { ProblemWriter.ContentType }
                };
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
            RefreshTokenCookie.Delete(HttpContext);
            var sessionId = User.FindSessionId();
            if (sessionId.HasValue)
            {
                var userId = CurrentActor.UserId;
                await authService.LogoutAsync(userId, sessionId.Value);
                await auditLog.LogAsync(new AuditEntry(userId, "Odjava", "Auth", userId));
            }
            return NoContent();
        }

        private async Task<LoginResponse> CompleteLoginAsync(AuthResult result)
        {
            await auditLog.LogAsync(new AuditEntry(result.User.Id, "Uspesna prijava", "Auth", result.User.Id,
                result.User.TwoFactorEnabled ? "Dvostepena prijava" : null));
            if (result.RefreshToken != null)
                RefreshTokenCookie.Append(HttpContext, result.RefreshToken);
            var pending = await legalService.GetPendingDocumentsAsync(result.User.Id);
            return result.ToResponse(pending);
        }
    }
}
