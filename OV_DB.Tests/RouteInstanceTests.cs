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
}
