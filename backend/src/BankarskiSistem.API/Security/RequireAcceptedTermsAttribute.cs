using BankarskiSistem.API.Middleware;
using BankarskiSistem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BankarskiSistem.API.Security
{
    public sealed class RequireAcceptedTermsAttribute : TypeFilterAttribute
    {
        public RequireAcceptedTermsAttribute() : base(typeof(RequireAcceptedTermsFilter)) { }
    }

    public sealed class RequireAcceptedTermsFilter : IAsyncActionFilter
    {
        private readonly ILegalService _legalService;

        public RequireAcceptedTermsFilter(ILegalService legalService)
        {
            _legalService = legalService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindUserId();
            if (userId.HasValue && (await _legalService.GetPendingDocumentsAsync(userId.Value)).Count > 0)
            {
                var problem = ProblemWriter.Create(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "terms-not-accepted",
                    "Potrebno je prihvatiti uslove",
                    "Pre izvršenja platnih transakcija morate prihvatiti važeću politiku privatnosti i uslove korišćenja.",
                    []);
                context.Result = new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ContentTypes = { ProblemWriter.ContentType }
                };
                return;
            }

            await next();
        }
    }
}
