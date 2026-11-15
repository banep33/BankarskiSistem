using System.Globalization;
using System.Text;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public static class TransactionCursor
    {
        public static string Encode(DateTime createdAt, int id)
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt.Ticks}:{id}"));
            return raw.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static bool TryDecode(string? cursor, out DateTime createdAt, out int id)
        {
            createdAt = default;
            id = 0;
            if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 64)
                return false;

            try
            {
                var base64 = cursor.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');

                if (parts.Length != 2
                    || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                    || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out id)
                    || ticks > DateTime.MaxValue.Ticks)
                    return false;

                createdAt = new DateTime(ticks);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
