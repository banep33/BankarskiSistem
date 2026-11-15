using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Tests.Unit
{
    public class StandingOrderScheduleTests
    {
        [Fact]
        public void Monthly_FromEndOfMonth_ClampsToShortMonths_WithoutDrifting()
        {
            var start = new DateOnly(2027, 1, 31);

            Assert.Equal(new DateOnly(2027, 2, 28), StandingOrderSchedule.Occurrence(start, StandingOrderFrequency.Mesecno, 1));
            Assert.Equal(new DateOnly(2027, 3, 31), StandingOrderSchedule.Occurrence(start, StandingOrderFrequency.Mesecno, 2));
            Assert.Equal(new DateOnly(2027, 4, 30), StandingOrderSchedule.Occurrence(start, StandingOrderFrequency.Mesecno, 3));
        }

        [Theory]
        [InlineData(StandingOrderFrequency.Nedeljno, 2027, 1, 15)]
        [InlineData(StandingOrderFrequency.Mesecno, 2027, 3, 1)]
        [InlineData(StandingOrderFrequency.Kvartalno, 2027, 7, 1)]
        [InlineData(StandingOrderFrequency.Godisnje, 2029, 1, 1)]
        public void Occurrence_SecondStep_MatchesFrequency(StandingOrderFrequency frequency, int year, int month, int day)
        {
            var start = new DateOnly(2027, 1, 1);
            const int index = 2;

            Assert.Equal(new DateOnly(year, month, day), StandingOrderSchedule.Occurrence(start, frequency, index));
        }

        [Fact]
        public void Upcoming_RespectsEndDateAndMaxExecutions()
        {
            var start = new DateOnly(2027, 5, 10);

            var byDate = StandingOrderSchedule.Upcoming(start, StandingOrderFrequency.Mesecno, 0, new DateOnly(2027, 7, 10), null, 10);
            var byCount = StandingOrderSchedule.Upcoming(start, StandingOrderFrequency.Mesecno, 1, null, 3, 10);

            Assert.Equal([new DateOnly(2027, 5, 10), new DateOnly(2027, 6, 10), new DateOnly(2027, 7, 10)], byDate);
            Assert.Equal([new DateOnly(2027, 6, 10), new DateOnly(2027, 7, 10)], byCount);
        }
    }
}
