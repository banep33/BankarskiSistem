using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Security;
using BankarskiSistem.Tests.Fakes;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Tests.Unit
{
    public class SessionValidationCacheTests
    {
        private readonly FakeTimeProvider _time = new();
        private readonly SessionValidationCache _cache;
        private readonly Guid _session = Guid.NewGuid();

        public SessionValidationCacheTests()
        {
            _cache = new SessionValidationCache(Options.Create(new CustomerProtectionSettings { SessionCacheSeconds = 15 }), _time);
        }

        [Fact]
        public void RememberedSession_IsValidOnlyForSameUserVersionAndRole()
        {
            _cache.Remember(_session, 4, 2, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));

            Assert.True(_cache.IsKnownValid(_session, 4, 2, "Korisnik"));
            Assert.False(_cache.IsKnownValid(_session, 4, 3, "Korisnik"));
            Assert.False(_cache.IsKnownValid(Guid.NewGuid(), 4, 2, "Korisnik"));
        }

        [Fact]
        public void Entry_ExpiresAfterTimeToLive()
        {
            _cache.Remember(_session, 4, 0, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));
            _time.Advance(TimeSpan.FromSeconds(16));

            Assert.False(_cache.IsKnownValid(_session, 4, 0, "Korisnik"));
        }

        [Fact]
        public void InvalidateUser_ImmediatelyDropsAllSessionsOfThatUser()
        {
            var other = Guid.NewGuid();
            _cache.Remember(_session, 4, 0, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));
            _cache.Remember(other, 10, 0, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));

            _cache.InvalidateUser(4);

            Assert.False(_cache.IsKnownValid(_session, 4, 0, "Korisnik"));
            Assert.True(_cache.IsKnownValid(other, 10, 0, "Korisnik"));
        }

        [Fact]
        public void InvalidateSession_DropsOnlyThatSession()
        {
            var laptop = Guid.NewGuid();
            _cache.Remember(_session, 4, 0, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));
            _cache.Remember(laptop, 4, 0, "Korisnik", _time.GetUtcNow().UtcDateTime.AddMinutes(30));

            _cache.InvalidateSession(_session);

            Assert.False(_cache.IsKnownValid(_session, 4, 0, "Korisnik"));
            Assert.True(_cache.IsKnownValid(laptop, 4, 0, "Korisnik"));
        }
    }
}
