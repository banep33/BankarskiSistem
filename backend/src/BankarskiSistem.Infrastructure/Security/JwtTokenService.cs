using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BankarskiSistem.Infrastructure.Security
{
    public class JwtTokenService : ITokenService
    {
        public const string TokenVersionClaim = "tv";
        public const string SessionIdClaim = "sid";
        public const string PurposeClaim = "purpose";
        public const string TwoFactorPurpose = "dvostepena-prijava";

        private readonly JwtSettings _settings;
        private readonly TimeProvider _timeProvider;

        public JwtTokenService(IOptions<JwtSettings> settings, TimeProvider timeProvider)
        {
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        public DateTime NextExpiry() => _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_settings.ExpirationMinutes);

        public DateTime NextSessionExpiry() => _timeProvider.GetUtcNow().UtcDateTime.AddHours(_settings.SessionHours);

        public string CreateTwoFactorChallenge(User user, DateTime expiresAt)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: TwoFactorAudience,
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(TokenVersionClaim, user.TokenVersion.ToString()),
                    new Claim(PurposeClaim, TwoFactorPurpose),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                ],
                notBefore: now,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(CreateSigningKey(_settings), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public (int UserId, int TokenVersion)? ReadTwoFactorChallenge(string token)
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            try
            {
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _settings.Issuer,
                    ValidAudience = TwoFactorAudience,
                    IssuerSigningKey = CreateSigningKey(_settings),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    LifetimeValidator = (_, expires, _, _) =>
                        expires.HasValue && expires.Value > _timeProvider.GetUtcNow().UtcDateTime
                }, out _);

                if (principal.FindFirst(PurposeClaim)?.Value != TwoFactorPurpose)
                    return null;
                if (!int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
                    return null;
                if (!int.TryParse(principal.FindFirst(TokenVersionClaim)?.Value, out var tokenVersion))
                    return null;
                return (userId, tokenVersion);
            }
            catch (SecurityTokenException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private string TwoFactorAudience => $"{_settings.Audience}:{TwoFactorPurpose}";

        public string CreateToken(User user, Guid sessionId, DateTime expiresAt, bool mfaVerified = false)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName),
                new Claim(TokenVersionClaim, user.TokenVersion.ToString()),
                new Claim(SessionIdClaim, sessionId.ToString()),
                new Claim(AuthMethods.ClaimType, AuthMethods.For(mfaVerified)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var credentials = new SigningCredentials(CreateSigningKey(_settings), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static SymmetricSecurityKey CreateSigningKey(JwtSettings settings)
        {
            var keyBytes = Encoding.UTF8.GetBytes(settings.SecretKey ?? string.Empty);
            if (keyBytes.Length < JwtSettings.MinimumKeyBytes)
            {
                throw new InvalidOperationException(
                    $"Jwt:SecretKey mora imati najmanje {JwtSettings.MinimumKeyBytes} bajta. " +
                    "Podesite ga kroz user-secrets ili promenljivu okruzenja Jwt__SecretKey.");
            }
            return new SymmetricSecurityKey(keyBytes);
        }
    }
}
