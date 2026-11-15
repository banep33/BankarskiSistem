using System.Text.Json;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class TimestampIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public TimestampIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        [MySqlFact]
        public async Task TimestampsReadFromDatabase_AreUtc_AndSerializeWithZoneDesignator()
        {
            var createdAt = new DateTime(2026, 11, 15, 19, 14, 25, DateTimeKind.Utc);
            int userId;
            await using (var context = _database.CreateContext())
            {
                var user = new User
                {
                    FirstName = "Vreme",
                    LastName = "Test",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = "x",
                    Role = UserRole.Korisnik,
                    CreatedAt = createdAt,
                    LockedUntil = createdAt.AddMinutes(30)
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();
                userId = user.Id;
            }

            await using var reader = _database.CreateContext();
            var loaded = await reader.Users.AsNoTracking().SingleAsync(u => u.Id == userId);

            Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
            Assert.Equal(createdAt, loaded.CreatedAt);
            Assert.Equal(DateTimeKind.Utc, loaded.LockedUntil!.Value.Kind);
            Assert.Equal("\"2026-11-15T19:14:25Z\"", JsonSerializer.Serialize(loaded.CreatedAt));
        }
    }
}
