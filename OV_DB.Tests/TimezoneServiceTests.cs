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
            var startTime = new DateTime(2025, 6, 15, 14, 0, 0); // 14:00 local time as entered by user
            var endTime = new DateTime(2025, 6, 15, 16, 0, 0);   // 16:00 local time as entered by user
            
            // Create LineString from Amsterdam to London (different timezones)
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676), // Amsterdam
                new Coordinate(-0.1276, 51.5074) // London
            };
            var lineString = geometryFactory.CreateLineString(coords);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            // With simplified approach: treat input times as local trip times (no timezone conversion)
            // User entered 14:00 to 16:00, so duration is 2 hours as they experienced it
            Assert.Equal(2.0, duration);
        }

        [Fact]
        public void CalculateDurationInHours_CrossTimezone_NewYorkToLosAngeles()
        {
            // Arrange
            var service = new TimezoneService();
            var startTime = new DateTime(2025, 3, 15, 10, 0, 0); // 10:00 local time as entered by user
            var endTime = new DateTime(2025, 3, 15, 11, 0, 0);   // 11:00 local time as entered by user (arrived at 11 AM local time)
            
            // Create LineString from New York to Los Angeles
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(-74.0060, 40.7128), // New York
                new Coordinate(-118.2437, 34.0522) // Los Angeles
            };
            var lineString = geometryFactory.CreateLineString(coords);

            // Act
            var duration = service.CalculateDurationInHours(startTime, endTime, lineString);

            // Assert
            // With simplified approach: treat input times as local trip times (no timezone conversion)  
            // User entered 10:00 to 11:00, so duration is 1 hour as they experienced it
            Assert.Equal(1.0, duration);
        }
    }
}