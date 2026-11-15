using BankarskiSistem.API.Middleware;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.API.Security
{
    public class TwoFactorAssuranceRequirement : IAuthorizationRequirement
    {
    }

    public class TwoFactorAssuranceHandler(IOptions<SecuritySettings> settings) : AuthorizationHandler<TwoFactorAssuranceRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TwoFactorAssuranceRequirement requirement)
        {
            if (!settings.Value.RequireTwoFactorForStaff || AuthMethods.IsMultiFactor(context.User))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }

    public class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _fallback = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (!authorizeResult.Forbidden)
            {
                await _fallback.HandleAsync(next, context, policy, authorizeResult);
                return;
            }

            var missingSecondFactor = authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<TwoFactorAssuranceRequirement>().Any() == true
                && (context.User.IsInRole("Bankar") || context.User.IsInRole("Administrator"));

            if (missingSecondFactor)
                await ProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, "two-factor-required", "Potrebna je dvostepena prijava",
                    "Zaposleni banke moraju da koriste dvostepenu prijavu. Uključite je u odeljku Bezbednost, a zatim nastavite sa radom.", []);
            else
                await ProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, "forbidden", "Pristup odbijen",
                    "Nemate dozvolu za ovu radnju.", []);
        }
    }
}
