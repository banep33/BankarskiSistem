using BankarskiSistem.API.Contracts;
using BankarskiSistem.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace BankarskiSistem.API.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private const string UnexpectedErrorMessage = "Došlo je do neočekivane greške. Pokušajte ponovo kasnije.";

        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, slug, title, detail) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "not-found", "Resurs nije pronađen", exception.Message),
                ReauthenticationRequiredException => (StatusCodes.Status403Forbidden, "reauthentication-required", "Potrebna je potvrda lozinkom", exception.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, "forbidden", "Pristup odbijen", exception.Message),
                DuplicateEntryException => (StatusCodes.Status409Conflict, "duplicate", "Zapis već postoji", exception.Message),
                ConflictException => (StatusCodes.Status409Conflict, "conflict", "Konflikt", exception.Message),
                AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "authentication-failed", "Prijava nije uspela", exception.Message),
                BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "business-rule", "Operacija nije dozvoljena", exception.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "unauthorized", "Niste prijavljeni", "Prijavite se ponovo."),
                BadHttpRequestException => (StatusCodes.Status400BadRequest, "bad-request", "Neispravan zahtev", "Zahtev nije u ispravnom formatu."),
                _ => (StatusCodes.Status500InternalServerError, "server-error", "Greška na serveru", UnexpectedErrorMessage)
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(exception, "Neobrađena greška na {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            else
                _logger.LogInformation("Zahtev {Method} {Path} odbijen ({Status}): {Detail}",
                    httpContext.Request.Method, httpContext.Request.Path, status, detail);

            await ProblemWriter.WriteAsync(httpContext, status, slug, title, detail, [], cancellationToken);
            return true;
        }
    }

    public static class ProblemWriter
    {
        public const string ContentType = "application/problem+json";
        public const string TypePrefix = "https://bankarski-sistem.local/problems/";

        public static ApiProblem Create(HttpContext context, int status, string slug, string title, string detail, IReadOnlyList<string> errors) =>
            new(TypePrefix + slug, title, status, detail, context.Request.Path, context.TraceIdentifier, errors);

        public static Task WriteAsync(
            HttpContext context, int status, string slug, string title, string detail,
            IReadOnlyList<string> errors, CancellationToken cancellationToken = default)
        {
            context.Response.StatusCode = status;
            return context.Response.WriteAsJsonAsync(
                Create(context, status, slug, title, detail, errors),
                options: null,
                contentType: ContentType,
                cancellationToken: cancellationToken);
        }
    }
}
