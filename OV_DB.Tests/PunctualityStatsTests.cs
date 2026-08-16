using System.Collections.Generic;
using System.Linq;
using OV_DB.Controllers;

namespace OV_DB.Tests
{
    // The delay buckets and the "punctual" threshold define what the punctuality page reports,
    // so pin the boundaries: a silent shift here would rewrite historical statistics.
    public class PunctualityStatsTests
    {
        private static int CountFor(IEnumerable<double> delays, string key) =>
            StatsController.BucketDelays(delays).Single(b => b.Key == key).Count;

        [Fact]
        public void BucketDelays_ReturnsAllBucketsInOrder_EvenWhenEmpty()
        {
            var buckets = StatsController.BucketDelays([]);

            Assert.Equal(
                ["EARLY", "ONTIME", "D5_15", "D15_30", "D30_60", "D60PLUS"],
                buckets.Select(b => b.Key));
            Assert.All(buckets, b => Assert.Equal(0, b.Count));
        }

        [Theory]
        [InlineData(-10, "EARLY")]
        [InlineData(-1.5, "EARLY")]
        [InlineData(-1, "ONTIME")]   // exactly one minute early still counts as on time
        [InlineData(0, "ONTIME")]
        [InlineData(4.9, "ONTIME")]
        [InlineData(5, "D5_15")]     // the punctuality threshold is exclusive
        [InlineData(14.9, "D5_15")]
        [InlineData(15, "D15_30")]
        [InlineData(29.9, "D15_30")]
        [InlineData(30, "D30_60")]
        [InlineData(59.9, "D30_60")]
        [InlineData(60, "D60PLUS")]
        [InlineData(600, "D60PLUS")]
        public void BucketDelays_PlacesDelayInExpectedBucket(double delay, string expectedKey)
        {
            Assert.Equal(1, CountFor([delay], expectedKey));
            Assert.Equal(1, StatsController.BucketDelays([delay]).Sum(b => b.Count));
        }

        [Fact]
        public void Median_ReturnsNullForEmptyInput()
        {
            Assert.Null(StatsController.Median([]));
        }

        [Fact]
        public void Median_UsesMiddleValueForOddCount()
        {
            Assert.Equal(4, StatsController.Median([9, 1, 4]));
        }

        [Fact]
        public void Median_AveragesTheTwoMiddleValuesForEvenCount()
        {
            Assert.Equal(3.5, StatsController.Median([1, 2, 5, 9]));
        }

        [Fact]
        public void Median_IsNotSkewedByASingleExtremeDelay()
        {
            // The mean of these is 51.4; the median is what makes the page readable.
            var delays = new List<double> { 0, 1, 2, 3, 251 };

            Assert.Equal(2, StatsController.Median(delays));
        }
    }
}
