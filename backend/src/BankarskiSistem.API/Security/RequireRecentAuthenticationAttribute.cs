using BankarskiSistem.API.Middleware;
using BankarskiSistem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BankarskiSistem.API.Security
{
    public sealed class RequireRecentAuthenticationAttribute : TypeFilterAttribute
    {
        public RequireRecentAuthenticationAttribute() : base(typeof(RequireRecentAuthenticationFilter)) { }
    }

    public sealed class RequireRecentAuthenticationFilter : IAsyncActionFilter
    {
        private readonly ISessionService _sessionService;

        public RequireRecentAuthenticationFilter(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var sessionId = context.HttpContext.User.FindSessionId();
            if (sessionId.HasValue && await _sessionService.IsRecentlyReauthenticatedAsync(sessionId.Value))
            {
                await next();
                return;
            }

            context.Result = ReauthenticationRequired(context.HttpContext,
                "Za ovu radnju potrebno je da ponovo potvrdite lozinku.");
        }

        public static ObjectResult ReauthenticationRequired(HttpContext httpContext, string detail)
        {
            var problem = ProblemWriter.Create(
                httpContext, StatusCodes.Status403Forbidden, "reauthentication-required", "Potrebna je potvrda lozinkom", detail, []);
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status403Forbidden,
                ContentTypes = { ProblemWriter.ContentType }
            };
        }
    }
}
