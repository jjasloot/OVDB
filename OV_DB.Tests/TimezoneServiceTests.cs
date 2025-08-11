using OV_DB.Services;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace OV_DB.Tests
{
    public class TimezoneServiceTests
    {
        [Fact]
        public void CalculateDurationInHours_WithNullLineString_ReturnsSimpleDuration()
        {
            // Arrange
            var service = new TimezoneService();
            var startTime = new DateTime(2025, 1, 1, 10, 0, 0);
            var endTime = new DateTime(2025, 1, 1, 12, 30, 0);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, null);

            // Assert
            Assert.Equal(2.5, duration); // 2.5 hours difference
        }

        [Fact]
        public void CalculateDurationInHours_WithValidLineString_HandlesTimezones()
        {
            // Arrange
            var service = new TimezoneService();
            var startTime = new DateTime(2025, 1, 1, 10, 0, 0);
            var endTime = new DateTime(2025, 1, 1, 12, 0, 0);
            
            // Create a simple LineString (Amsterdam to Paris coordinates)
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676), // Amsterdam
                new Coordinate(2.3522, 48.8566)  // Paris
            };
            var lineString = geometryFactory.CreateLineString(coords);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            Assert.Equal(2.0, duration); // 2 hours difference - same timezone
        }

        [Fact]
        public void CalculateDurationInHours_CrossTimezone_AmsterdamToLondon()
        {
            // Arrange
            var service = new TimezoneService();
            var startTime = new DateTime(2025, 6, 15, 14, 0, 0); // Summer time for more realistic test
            var endTime = new DateTime(2025, 6, 15, 16, 0, 0);
            
            // Create LineString from Amsterdam to London (different timezones)
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676), // Amsterdam (UTC+2 in summer)
                new Coordinate(-0.1276, 51.5074) // London (UTC+1 in summer)
            };
            var lineString = geometryFactory.CreateLineString(coords);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            // Expected: Start at 14:00 Amsterdam time (UTC+2), End at 16:00 London time (UTC+1)
            // Amsterdam 14:00 UTC+2 = 12:00 UTC
            // London 16:00 UTC+1 = 15:00 UTC  
            // Duration should be 3 hours
            Assert.Equal(3.0, duration);
        }

        [Fact]
        public void CalculateDurationInHours_CrossTimezone_NewYorkToLosAngeles()
        {
            // Arrange
            var service = new TimezoneService();
            var startTime = new DateTime(2025, 3, 15, 10, 0, 0); // 10 AM Eastern
            var endTime = new DateTime(2025, 3, 15, 9, 0, 0); // 9 AM Pacific
            
            // Create LineString from New York to Los Angeles
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(-74.0060, 40.7128), // New York (UTC-4 in summer)
                new Coordinate(-118.2437, 34.0522) // Los Angeles (UTC-7 in summer)
            };
            var lineString = geometryFactory.CreateLineString(coords);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            // Expected: Start at 10:00 NY time, End at 9:00 LA time
            // NY 10:00 UTC-4 = 14:00 UTC
            // LA 9:00 UTC-7 = 16:00 UTC
            // Duration should be 2 hours
            Assert.Equal(2.0, duration);
        }
    }
}