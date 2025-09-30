using OV_DB.Services;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace OV_DB.Tests.Services
{
    public class TimezoneServiceAdvancedTests
    {
        private readonly TimezoneService _service;
        private readonly GeometryFactory _geometryFactory;

        public TimezoneServiceAdvancedTests()
        {
            _service = new TimezoneService();
            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        }

        [Fact]
        public async Task ConvertUtcToLocalTimeAsync_WithValidCoordinates_ReturnsLocalTime()
        {
            // Arrange
            var utcDateTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            double latitude = 52.3676; // Amsterdam
            double longitude = 4.9041;

            // Act
            var localTime = await _service.ConvertUtcToLocalTimeAsync(utcDateTime, latitude, longitude);

            // Assert
            Assert.NotEqual(default(DateTime), localTime);
            // In summer, Amsterdam is UTC+2
            Assert.True(localTime >= utcDateTime);
        }

        [Fact]
        public async Task ConvertUtcToLocalTimeAsync_WithInvalidCoordinates_ReturnsSafeValue()
        {
            // Arrange
            var utcDateTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            double latitude = 999; // Invalid
            double longitude = 999; // Invalid

            // Act
            var localTime = await _service.ConvertUtcToLocalTimeAsync(utcDateTime, latitude, longitude);

            // Assert - Should return the UTC time when conversion fails
            Assert.Equal(utcDateTime, localTime);
        }

        [Fact]
        public void CalculateDurationInHours_WithSingleTimezone_ReturnsCorrectDuration()
        {
            // Arrange
            var startTime = new DateTime(2025, 1, 1, 10, 0, 0);
            var endTime = new DateTime(2025, 1, 1, 15, 30, 0);
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3), // Amsterdam
                new Coordinate(4.48, 51.92) // Rotterdam
            });

            // Act
            var duration = _service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            Assert.Equal(5.5, duration, precision: 1); // 5.5 hours
        }

        [Fact]
        public void CalculateDurationInHours_AcrossMidnight_HandlesCorrectly()
        {
            // Arrange
            var startTime = new DateTime(2025, 1, 1, 23, 0, 0);
            var endTime = new DateTime(2025, 1, 2, 2, 0, 0);
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            // Act
            var duration = _service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            Assert.Equal(3.0, duration, precision: 1); // 3 hours
        }

        [Theory]
        [InlineData(2025, 1, 15, 10, 0, 2025, 1, 15, 12, 0, 2.0)] // 2 hours
        [InlineData(2025, 6, 15, 8, 30, 2025, 6, 15, 17, 45, 9.25)] // 9.25 hours
        [InlineData(2025, 3, 20, 6, 15, 2025, 3, 20, 18, 45, 12.5)] // 12.5 hours
        public void CalculateDurationInHours_VariousTimeRanges_ReturnsCorrectDuration(
            int startYear, int startMonth, int startDay, int startHour, int startMinute,
            int endYear, int endMonth, int endDay, int endHour, int endMinute,
            double expectedHours)
        {
            // Arrange
            var startTime = new DateTime(startYear, startMonth, startDay, startHour, startMinute, 0);
            var endTime = new DateTime(endYear, endMonth, endDay, endHour, endMinute, 0);
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            // Act
            var duration = _service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            Assert.Equal(expectedHours, duration, precision: 2);
        }
    }
}
