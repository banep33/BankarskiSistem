using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BankarskiSistem.Infrastructure.Data
{
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(value => ToStorage(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }

        internal static DateTime ToStorage(DateTime value) => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }

    public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(value => value.HasValue ? UtcDateTimeConverter.ToStorage(value.Value) : value,
                   value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value)
        {
        }
    }
}
