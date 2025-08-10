using OVDB_database.Models;
using OV_DB.Services;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace OV_DB.Tests
{
    public class RouteInstanceTests
    {
        [Fact]
        public void GetAverageSpeedKmh_WithValidData_ReturnsCorrectSpeed()
        {
            // Arrange
            var route = new Route
            {
                CalculatedDistance = 100, // 100 km
                OverrideDistance = null
            };

            var instance = new RouteInstance
            {
                Route = route,
                DurationHours = 2.0, // 2 hours
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(2)
            };

            // Act
            var averageSpeed = instance.GetAverageSpeedKmh();

            // Assert
            Assert.Equal(50.0, averageSpeed); // 100 km / 2 hours = 50 km/h
        }

        [Fact]
        public void GetAverageSpeedKmh_WithNullDuration_ReturnsNull()
        {
            // Arrange
            var route = new Route { CalculatedDistance = 100 };
            var instance = new RouteInstance
            {
                Route = route,
                DurationHours = null
            };

            // Act
            var averageSpeed = instance.GetAverageSpeedKmh();

            // Assert
            Assert.Null(averageSpeed);
        }

        [Fact]
        public void GetAverageSpeedKmh_WithOverrideDistance_UsesOverride()
        {
            // Arrange
            var route = new Route
            {
                CalculatedDistance = 100,
                OverrideDistance = 80 // Should use this instead
            };

            var instance = new RouteInstance
            {
                Route = route,
                DurationHours = 2.0
            };

            // Act
            var averageSpeed = instance.GetAverageSpeedKmh();

            // Assert
            Assert.Equal(40.0, averageSpeed); // 80 km / 2 hours = 40 km/h
        }
    }

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
            Assert.True(duration > 0);
            Assert.True(duration <= 24); // Should be reasonable
        }
    }
}
