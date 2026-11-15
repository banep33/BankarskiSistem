using System.Security.Claims;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Tests.Unit
{
    public class TwoFactorAssuranceTests
    {
        private static async Task<bool> EvaluateAsync(string? authMethod, bool required, string claimType = AuthMethods.ClaimType)
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, "Bankar") };
            if (authMethod != null)
                claims.Add(new Claim(claimType, authMethod));
            var requirement = new TwoFactorAssuranceRequirement();
            var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
            var handler = new TwoFactorAssuranceHandler(Options.Create(new SecuritySettings { RequireTwoFactorForStaff = required }));
            await handler.HandleAsync(context);
            return context.HasSucceeded;
        }

        [Theory]
        [InlineData(AuthMethods.MultiFactor, true, true)]
        [InlineData(AuthMethods.Password, true, false)]
        [InlineData(null, true, false)]
        [InlineData(AuthMethods.Password, false, true)]
        public async Task StaffPolicy_RequiresMultiFactorSessionWhenEnabled(string? authMethod, bool required, bool expected)
        {
            Assert.Equal(expected, await EvaluateAsync(authMethod, required));
        }

        [Fact]
        public async Task StaffPolicy_AcceptsClaimTypeMappedByJwtHandler()
        {
            Assert.True(await EvaluateAsync(AuthMethods.MultiFactor, true, AuthMethods.MappedClaimType));
        }
    }
}
