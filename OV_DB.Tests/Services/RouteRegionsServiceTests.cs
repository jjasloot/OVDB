using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests.Services
{
    public class RouteRegionsServiceTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly RouteRegionsService _service;
        private readonly GeometryFactory _geometryFactory;

        public RouteRegionsServiceTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _service = new RouteRegionsService(_context);
            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task AssignRegionsToRouteAsync_WithIntersectingRegion_AssignsRegion()
        {
            // Arrange
            var region = new Region
            {
                Id = 1,
                Name = "Netherlands",
                ParentRegionId = null,
                Geometry = _geometryFactory.CreateMultiPolygon(new[] {
                    _geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(4.5, 52.0),
                        new Coordinate(5.5, 52.0),
                        new Coordinate(5.5, 53.0),
                        new Coordinate(4.5, 53.0),
                        new Coordinate(4.5, 52.0)
                    })
                })
            };
            _context.Regions.Add(region);
            await _context.SaveChangesAsync();

            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = _geometryFactory.CreateLineString(new Coordinate[]
                {
                    new Coordinate(4.9, 52.3),
                    new Coordinate(5.0, 52.4)
                }),
                Regions = new List<Region>()
            };

            // Act
            var result = await _service.AssignRegionsToRouteAsync(route);

            // Assert
            Assert.True(result); // Changed
            Assert.Single(route.Regions);
            Assert.Equal("Netherlands", route.Regions.First().Name);
        }

        [Fact]
        public async Task AssignRegionsToRouteAsync_WithNonIntersectingRegion_DoesNotAssignRegion()
        {
            // Arrange
            var region = new Region
            {
                Id = 1,
                Name = "Spain",
                ParentRegionId = null,
                Geometry = _geometryFactory.CreateMultiPolygon(new[] {
                    _geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(-4, 40),
                        new Coordinate(-3, 40),
                        new Coordinate(-3, 41),
                        new Coordinate(-4, 41),
                        new Coordinate(-4, 40)
                    })
                })
            };
            _context.Regions.Add(region);
            await _context.SaveChangesAsync();

            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = _geometryFactory.CreateLineString(new Coordinate[]
                {
                    new Coordinate(4.9, 52.3),
                    new Coordinate(5.0, 52.4)
                }),
                Regions = new List<Region>()
            };

            // Act
            var result = await _service.AssignRegionsToRouteAsync(route);

            // Assert
            Assert.False(result); // No changes
            Assert.Empty(route.Regions);
        }

        [Fact]
        public async Task AssignRegionsToRouteAsync_WithExistingRegions_UpdatesCorrectly()
        {
            // Arrange
            var oldRegion = new Region
            {
                Id = 1,
                Name = "Old Region",
                ParentRegionId = null,
                Geometry = _geometryFactory.CreateMultiPolygon(new[] {
                    _geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(10, 10),
                        new Coordinate(11, 10),
                        new Coordinate(11, 11),
                        new Coordinate(10, 11),
                        new Coordinate(10, 10)
                    })
                })
            };

            var newRegion = new Region
            {
                Id = 2,
                Name = "New Region",
                ParentRegionId = null,
                Geometry = _geometryFactory.CreateMultiPolygon(new[] {
                    _geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(4.5, 52.0),
                        new Coordinate(5.5, 52.0),
                        new Coordinate(5.5, 53.0),
                        new Coordinate(4.5, 53.0),
                        new Coordinate(4.5, 52.0)
                    })
                })
            };

            _context.Regions.AddRange(oldRegion, newRegion);
            await _context.SaveChangesAsync();

            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                LineString = _geometryFactory.CreateLineString(new Coordinate[]
                {
                    new Coordinate(4.9, 52.3),
                    new Coordinate(5.0, 52.4)
                }),
                Regions = new List<Region> { oldRegion }
            };

            // Act
            var result = await _service.AssignRegionsToRouteAsync(route);

            // Assert
            Assert.True(result); // Changed
            Assert.Single(route.Regions);
            Assert.Equal("New Region", route.Regions.First().Name);
        }
    }
}
