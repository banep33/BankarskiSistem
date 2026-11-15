using System.Security.Claims;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.API.Security
{
    public static class ClaimsPrincipalExtensions
    {
        public static Actor ToActor(this ClaimsPrincipal principal)
        {
            var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleClaim = principal.FindFirstValue(ClaimTypes.Role);

            if (!int.TryParse(idClaim, out var userId) || !Enum.TryParse<UserRole>(roleClaim, out var role))
                throw new UnauthorizedAccessException("Neispravan korisnički token.");

            return new Actor(userId, role);
        }

        public static Guid? FindSessionId(this ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(BankarskiSistem.Infrastructure.Security.JwtTokenService.SessionIdClaim), out var sessionId)
                ? sessionId
                : null;

        public static int? FindUserId(this ClaimsPrincipal principal) =>
            int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
    }
}
