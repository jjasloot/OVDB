using System;
using System.Collections.Generic;
using System.Linq;
using OV_DB.Services;

namespace OV_DB.Tests
{
    // Achievements are computed rather than stored, so "earned on" is derived from the trip that
    // first crossed each threshold. That derivation - and the progress-to-next-tier maths the UI
    // relies on - is what these tests pin down.
    public class AchievementServiceTests
    {
        private static AchievementService.ProgressPoint Point(int day, double value) =>
            new(new DateTime(2026, 1, day), value);

        private static List<AchievementService.ProgressPoint> Progression(params (int Day, double Value)[] points) =>
            points.Select(p => Point(p.Day, p.Value)).ToList();

        [Fact]
        public void BuildFamily_DatesEachTierToTheTripThatCrossedIt()
        {
            var family = AchievementService.BuildFamily("DISTANCE", "icon", "km", [10, 20, 30],
                Progression((1, 5), (2, 12), (3, 18), (4, 25)));

            Assert.Equal(2, family.EarnedTiers);
            Assert.Equal(3, family.TotalTiers);
            // CurrentTier is the highest earned one - 20, first reached on the 4th (18 was not enough
            // on the 3rd) - not the first tier crossed.
            Assert.Equal(20, family.CurrentTier.Threshold);
            Assert.Equal(new DateTime(2026, 1, 4), family.CurrentTier.EarnedOn);
            Assert.Equal(30, family.NextTier.Threshold);
            Assert.Null(family.NextTier.EarnedOn);
        }

        [Fact]
        public void BuildFamily_MeasuresProgressFromThePreviousThreshold()
        {
            // 25 of the way from 20 to 30 is halfway, not 25/30.
            var family = AchievementService.BuildFamily("DISTANCE", "icon", "km", [10, 20, 30],
                Progression((1, 25)));

            Assert.Equal(30, family.NextTier.Threshold);
            Assert.Equal(0.5, family.ProgressToNext);
        }

        [Fact]
        public void BuildFamily_ReportsFullProgressWhenEveryTierIsEarned()
        {
            var family = AchievementService.BuildFamily("TRIPS", "icon", "count", [1, 2],
                Progression((1, 5)));

            Assert.Null(family.NextTier);
            Assert.Equal(2, family.EarnedTiers);
            Assert.Equal(1, family.ProgressToNext);
        }

        [Fact]
        public void BuildFamily_HandlesNothingEarnedYet()
        {
            var family = AchievementService.BuildFamily("TRIPS", "icon", "count", [10, 20],
                Progression((1, 4)));

            Assert.Null(family.CurrentTier);
            Assert.Equal(0, family.EarnedTiers);
            Assert.Equal(10, family.NextTier.Threshold);
            Assert.Equal(0.4, family.ProgressToNext);
        }

        [Fact]
        public void BuildFamily_HandlesAnEmptyProgression()
        {
            var family = AchievementService.BuildFamily("TRIPS", "icon", "count", [10], []);

            Assert.Equal(0, family.CurrentValue);
            Assert.Equal(0, family.EarnedTiers);
            Assert.Null(family.CurrentTier);
            Assert.Equal(0, family.ProgressToNext);
        }

        [Fact]
        public void BuildFamily_TreatsAThresholdReachedExactlyAsEarned()
        {
            var family = AchievementService.BuildFamily("DISTANCE", "icon", "km", [1000],
                Progression((1, 1000)));

            Assert.Equal(1, family.EarnedTiers);
            Assert.Equal(new DateTime(2026, 1, 1), family.CurrentTier.EarnedOn);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SplitOperators_IgnoresBlankValues(string input)
        {
            Assert.Empty(AchievementService.SplitOperators(input));
        }

        [Fact]
        public void SplitOperators_SplitsAndTrimsMultiOperatorRoutes()
        {
            Assert.Equal(["NS", "Arriva", "Blauwnet"], AchievementService.SplitOperators("NS, Arriva ,Blauwnet"));
        }
    }
}
