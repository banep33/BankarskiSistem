using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class BusinessCalendar : IBusinessCalendar
    {
        private const string WindowsFallbackTimeZone = "Central Europe Standard Time";

        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _timeZone;

        public BusinessCalendar(TimeProvider timeProvider, IOptions<SecuritySettings> settings)
        {
            _timeProvider = timeProvider;
            _timeZone = ResolveTimeZone(settings.Value.BusinessTimeZone);
        }

        public DateTime StartOfTodayUtc()
        {
            var localNow = ToBusinessTime(_timeProvider.GetUtcNow().UtcDateTime);
            return ToUtc(localNow.Date);
        }

        public DateOnly Today() => DateOnly.FromDateTime(ToBusinessTime(_timeProvider.GetUtcNow().UtcDateTime));

        public DateTime ToBusinessTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);

        public DateTime ToUtc(DateTime businessTime) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(businessTime, DateTimeKind.Unspecified), _timeZone);

        private static TimeZoneInfo ResolveTimeZone(string id)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone))
                return timeZone;
            if (TimeZoneInfo.TryFindSystemTimeZoneById(WindowsFallbackTimeZone, out timeZone))
                return timeZone;
            return TimeZoneInfo.Utc;
        }
    }
}
