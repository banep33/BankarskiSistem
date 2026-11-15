using BankarskiSistem.Core.Interfaces;

namespace BankarskiSistem.API.Configuration
{
    public static class AdministratorSeeder
    {
        public static async Task SeedAdministratorAsync(this WebApplication app)
        {
            var section = app.Configuration.GetSection("Seed:Administrator");
            var email = section["Email"];
            var password = section["Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return;

            await using var scope = app.Services.CreateAsyncScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            await userService.EnsureAdministratorExistsAsync(
                email,
                password,
                section["FirstName"] ?? "Sistem",
                section["LastName"] ?? "Administrator");
        }
    }
}
