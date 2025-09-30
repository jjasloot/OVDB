using OV_DB.Helpers;

namespace OV_DB.Tests.Helpers
{
    public class GeometryHelperTests
    {
        [Fact]
        public void Distance_WithSameCoordinates_ReturnsZero()
        {
            // Arrange
            double lat1 = 52.3676, lon1 = 4.9041; // Amsterdam

            // Act
            var distance = GeometryHelper.distance(lat1, lon1, lat1, lon1, 'K');

            // Assert
            Assert.Equal(0, distance);
        }

        [Fact]
        public void Distance_AmsterdamToParis_ReturnsCorrectKilometers()
        {
            // Arrange
            double lat1 = 52.3676, lon1 = 4.9041; // Amsterdam
            double lat2 = 48.8566, lon2 = 2.3522; // Paris

            // Act
            var distance = GeometryHelper.distance(lat1, lon1, lat2, lon2, 'K');

            // Assert
            Assert.True(distance > 400 && distance < 500); // Approximately 430 km
        }

        [Fact]
        public void Distance_WithNauticalMiles_ReturnsCorrectValue()
        {
            // Arrange
            double lat1 = 52.3676, lon1 = 4.9041; // Amsterdam
            double lat2 = 48.8566, lon2 = 2.3522; // Paris

            // Act
            var distanceNautical = GeometryHelper.distance(lat1, lon1, lat2, lon2, 'N');

            // Assert
            Assert.True(distanceNautical > 0);
            Assert.True(distanceNautical < 500); // Should be less than kilometers
        }

        [Fact]
        public void Distance_WithMiles_ReturnsCorrectValue()
        {
            // Arrange
            double lat1 = 52.3676, lon1 = 4.9041; // Amsterdam
            double lat2 = 48.8566, lon2 = 2.3522; // Paris

            // Act
            var distanceMiles = GeometryHelper.distance(lat1, lon1, lat2, lon2, 'M');

            // Assert
            Assert.True(distanceMiles > 250 && distanceMiles < 300); // Approximately 267 miles
        }

        [Fact]
        public void Distance_NewYorkToLosAngeles_ReturnsCorrectDistance()
        {
            // Arrange
            double lat1 = 40.7128, lon1 = -74.0060; // New York
            double lat2 = 34.0522, lon2 = -118.2437; // Los Angeles

            // Act
            var distance = GeometryHelper.distance(lat1, lon1, lat2, lon2, 'K');

            // Assert
            Assert.True(distance > 3900 && distance < 4000); // Approximately 3944 km
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 'K', 0)] // Same point
        [InlineData(51.5074, -0.1278, 48.8566, 2.3522, 'K', 344)] // London to Paris (approx)
        [InlineData(35.6762, 139.6503, 37.7749, -122.4194, 'K', 8280)] // Tokyo to San Francisco (approx)
        public void Distance_VariousLocations_ReturnsExpectedRange(double lat1, double lon1, double lat2, double lon2, char unit, double expected)
        {
            // Act
            var distance = GeometryHelper.distance(lat1, lon1, lat2, lon2, unit);

            // Assert
            if (expected == 0)
            {
                Assert.Equal(0, distance);
            }
            else
            {
                // Allow 10% tolerance
                Assert.InRange(distance, expected * 0.9, expected * 1.1);
            }
        }
    }
}
