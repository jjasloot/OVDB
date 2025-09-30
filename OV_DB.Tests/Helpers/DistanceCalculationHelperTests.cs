using OV_DB.Helpers;
using OVDB_database.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace OV_DB.Tests.Helpers
{
    public class DistanceCalculationHelperTests
    {
        [Fact]
        public void ComputeDistance_WithSimpleLine_CalculatesCorrectDistance()
        {
            // Arrange
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676), // Amsterdam
                new Coordinate(2.3522, 48.8566)  // Paris - approximately 430 km
            };
            var lineString = geometryFactory.CreateLineString(coords);
            
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = lineString
            };

            // Act
            DistanceCalculationHelper.ComputeDistance(route);

            // Assert
            Assert.True(route.CalculatedDistance > 0);
            Assert.True(route.CalculatedDistance > 400 && route.CalculatedDistance < 500); // Approximately 430 km
        }

        [Fact]
        public void ComputeDistance_WithMultipleSegments_CalculatesCorrectDistance()
        {
            // Arrange
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676), // Amsterdam
                new Coordinate(4.3517, 50.8503), // Brussels
                new Coordinate(2.3522, 48.8566)  // Paris
            };
            var lineString = geometryFactory.CreateLineString(coords);
            
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = lineString
            };

            // Act
            DistanceCalculationHelper.ComputeDistance(route);

            // Assert
            Assert.True(route.CalculatedDistance > 0);
            Assert.True(route.CalculatedDistance > 400); // Total distance should be over 400 km
        }

        [Fact]
        public void ComputeDistance_WithSameStartEnd_ReturnsZero()
        {
            // Arrange
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var coords = new Coordinate[]
            {
                new Coordinate(4.9041, 52.3676),
                new Coordinate(4.9041, 52.3676)
            };
            var lineString = geometryFactory.CreateLineString(coords);
            
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = lineString
            };

            // Act
            DistanceCalculationHelper.ComputeDistance(route);

            // Assert
            Assert.Equal(0, route.CalculatedDistance);
        }
    }
}
