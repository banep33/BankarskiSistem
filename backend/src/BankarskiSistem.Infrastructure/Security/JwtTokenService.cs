using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

        private readonly JwtSettings _settings;
        private readonly TimeProvider _timeProvider;

        public JwtTokenService(IOptions<JwtSettings> settings, TimeProvider timeProvider)
        {
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        public DateTime NextExpiry() => _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_settings.ExpirationMinutes);

        public string CreateToken(User user, Guid sessionId, DateTime expiresAt)
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
