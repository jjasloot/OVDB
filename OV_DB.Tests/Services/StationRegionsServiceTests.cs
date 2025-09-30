using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Tests.Services
{
    public class StationRegionsServiceTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly StationRegionsService _service;
        private readonly GeometryFactory _geometryFactory;

        public StationRegionsServiceTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _service = new StationRegionsService(_context);
            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task AssignRegionsToStationAsync_WithIntersectingRegion_AssignsRegion()
        {
            // Arrange
            var region = new Region
            {
                Id = 1,
                Name = "Amsterdam Region",
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

            var station = new Station
            {
                Id = 1,
                Name = "Amsterdam Centraal",
                Longitude = 4.9,
                Lattitude = 52.3,
                Regions = new List<Region>()
            };

            // Act
            await _service.AssignRegionsToStationAsync(station);

            // Assert
            Assert.Single(station.Regions);
            Assert.Equal("Amsterdam Region", station.Regions.First().Name);
        }

        [Fact]
        public async Task AssignRegionsToStationAsync_WithNonIntersectingRegion_DoesNotAssignRegion()
        {
            // Arrange
            var region = new Region
            {
                Id = 1,
                Name = "Paris Region",
                Geometry = _geometryFactory.CreateMultiPolygon(new[] {
                    _geometryFactory.CreatePolygon(new Coordinate[]
                    {
                        new Coordinate(2.0, 48.5),
                        new Coordinate(2.5, 48.5),
                        new Coordinate(2.5, 49.0),
                        new Coordinate(2.0, 49.0),
                        new Coordinate(2.0, 48.5)
                    })
                })
            };
            _context.Regions.Add(region);
            await _context.SaveChangesAsync();

            var station = new Station
            {
                Id = 1,
                Name = "Amsterdam Centraal",
                Longitude = 4.9,
                Lattitude = 52.3,
                Regions = new List<Region>()
            };

            // Act
            await _service.AssignRegionsToStationAsync(station);

            // Assert
            Assert.Empty(station.Regions);
        }

        [Fact]
        public async Task AssignRegionsToStationCacheRegionsAsync_WithCachedRegions_AssignsCorrectly()
        {
            // Arrange
            var region = new Region
            {
                Id = 1,
                Name = "Netherlands",
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

            var station = new Station
            {
                Id = 1,
                Name = "Amsterdam Centraal",
                Longitude = 4.9,
                Lattitude = 52.3,
                Regions = new List<Region>()
            };

            // Act
            await _service.AssignRegionsToStationCacheRegionsAsync(station);

            // Assert
            Assert.Single(station.Regions);
            Assert.Equal("Netherlands", station.Regions.First().Name);
        }

        [Fact]
        public async Task AssignRegionsToStationAsync_ClearsExistingRegions()
        {
            // Arrange
            var oldRegion = new Region
            {
                Id = 1,
                Name = "Old Region",
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

            var station = new Station
            {
                Id = 1,
                Name = "Amsterdam Centraal",
                Longitude = 4.9,
                Lattitude = 52.3,
                Regions = new List<Region> { oldRegion }
            };

            // Act
            await _service.AssignRegionsToStationAsync(station);

            // Assert
            Assert.Single(station.Regions);
            Assert.Equal("New Region", station.Regions.First().Name);
        }
    }
}
