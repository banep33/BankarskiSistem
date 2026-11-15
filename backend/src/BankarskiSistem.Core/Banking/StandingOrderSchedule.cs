using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Banking
{
    public static class StandingOrderSchedule
    {
        public static DateOnly Occurrence(DateOnly startDate, StandingOrderFrequency frequency, int index) => frequency switch
        {
            StandingOrderFrequency.Nedeljno => startDate.AddDays(7 * index),
            StandingOrderFrequency.Mesecno => startDate.AddMonths(index),
            StandingOrderFrequency.Kvartalno => startDate.AddMonths(3 * index),
            StandingOrderFrequency.Godisnje => startDate.AddYears(index),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };

        public static bool IsWithinLimits(DateOnly date, int index, DateOnly? endDate, int? maxExecutions) =>
            (endDate == null || date <= endDate.Value) && (maxExecutions == null || index < maxExecutions.Value);

        public static IReadOnlyList<DateOnly> Upcoming(
            DateOnly startDate, StandingOrderFrequency frequency, int fromIndex, DateOnly? endDate, int? maxExecutions, int count)
        {
            var result = new List<DateOnly>(count);
            for (var index = fromIndex; result.Count < count; index++)
            {
                var date = Occurrence(startDate, frequency, index);
                if (!IsWithinLimits(date, index, endDate, maxExecutions))
                    break;
                result.Add(date);
            }
            return result;
        }
    }
}
