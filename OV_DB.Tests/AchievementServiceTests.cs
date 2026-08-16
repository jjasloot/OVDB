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

        [Theory]
        [InlineData("DB", true)]
        [InlineData("db", true)]
        [InlineData("Deutsche Bahn", true)]
        [InlineData("DB Regio AG Nord", true)]
        [InlineData("DBUS", false)]      // must not match on a bare prefix
        [InlineData("Blauwnet", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsDeutscheBahnName_MatchesOnlyTheRealThing(string name, bool expected)
        {
            Assert.Equal(expected, AchievementService.IsDeutscheBahnName(name));
        }

        [Fact]
        public void IsDeutscheBahn_PrefersTheMappedOperatorsOverFreeText()
        {
            var dbOperators = new HashSet<int> { 7 };

            // Mapped to a non-DB operator: the free text is not consulted at all.
            Assert.False(AchievementService.IsDeutscheBahn([3], "Deutsche Bahn", dbOperators));
            Assert.True(AchievementService.IsDeutscheBahn([3, 7], "Arriva", dbOperators));
        }

        [Fact]
        public void IsDeutscheBahn_FallsBackToFreeTextWhenNoOperatorsAreMapped()
        {
            var dbOperators = new HashSet<int> { 7 };

            Assert.True(AchievementService.IsDeutscheBahn([], "DB Regio", dbOperators));
            Assert.False(AchievementService.IsDeutscheBahn(null, "NS", dbOperators));
        }

        [Fact]
        public void BuildPerDayProgressions_TracksTheBiggestDayAndLongestStreak()
        {
            var trips = new List<(DateTime, double, List<string>)>
            {
                (new DateTime(2026, 1, 1), 100, ["NS"]),
                (new DateTime(2026, 1, 2), 300, ["NS", "Arriva"]),
                (new DateTime(2026, 1, 3), 50, ["NS"]),
                // Gap: the streak restarts here.
                (new DateTime(2026, 1, 10), 20, ["NS"]),
            };

            var (marathon, bingo, streak) = AchievementService.BuildPerDayProgressions(trips);

            Assert.Equal(300, marathon[^1].Value);
            Assert.Equal(2, bingo[^1].Value);
            Assert.Equal(3, streak[^1].Value);
        }

        [Theory]
        // Thresholds scale to the country, so a 3-region country and a 26-canton one both work.
        [InlineData(12, new double[] { 3, 6, 9, 12 })]
        [InlineData(26, new double[] { 7, 13, 20, 26 })]
        [InlineData(3, new double[] { 1, 2, 3 })]
        [InlineData(1, new double[] { 1 })]
        public void SubdivisionThresholds_ScaleToTheCountrySize(int total, double[] expected)
        {
            Assert.Equal(expected, AchievementService.SubdivisionThresholds(total));
        }

        [Fact]
        public void BuildSubdivisionFamilies_OnlyIncludesCountriesTheUserHasBeenTo()
        {
            // Regions 10/11 belong to country 1, region 20 to country 2 (never visited).
            var subdivisionParent = new Dictionary<int, int> { [10] = 1, [11] = 1, [20] = 2 };
            var countryInfo = new Dictionary<int, (string, string, int)>
            {
                [1] = ("Netherlands", "Nederland", 4),
                [2] = ("Germany", "Duitsland", 16),
            };
            var trips = new List<(DateTime, int)>
            {
                (new DateTime(2026, 1, 1), 100),
                (new DateTime(2026, 1, 5), 200),
            };
            IReadOnlyList<int> RegionsFor(int routeId) => routeId == 100 ? [10] : [11];

            var families = AchievementService.BuildSubdivisionFamilies(trips, RegionsFor, subdivisionParent, countryInfo);

            var family = Assert.Single(families);
            Assert.Equal("SUBDIVISIONS_1", family.Key);
            Assert.Equal("Netherlands", family.Name);
            Assert.Equal("Nederland", family.NameNL);
            Assert.Equal("SUBDIVISIONS_DESC", family.DescriptionKey);
            Assert.Equal(2, family.CurrentValue);
            // Second region was reached on the 5th, which is when the "half" tier was earned.
            Assert.Equal(new DateTime(2026, 1, 5), family.CurrentTier.EarnedOn);
        }

        [Fact]
        public void BuildPerDayProgressions_SumsSeveralTripsOnTheSameDay()
        {
            var trips = new List<(DateTime, double, List<string>)>
            {
                (new DateTime(2026, 1, 1), 200, ["NS"]),
                (new DateTime(2026, 1, 1), 400, ["Arriva"]),
            };

            var (marathon, bingo, streak) = AchievementService.BuildPerDayProgressions(trips);

            Assert.Equal(600, marathon[^1].Value);
            Assert.Equal(2, bingo[^1].Value);
            Assert.Equal(1, streak[^1].Value);
        }
    }
}
