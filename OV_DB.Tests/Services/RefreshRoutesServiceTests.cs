using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OV_DB.Hubs;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests.Services
{
    public class RefreshRoutesServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<IHubContext<MapGenerationHub>> _hubContextMock;
        private readonly Mock<IRouteRegionsService> _routeRegionsServiceMock;
        private readonly GeometryFactory _geometryFactory;

        public RefreshRoutesServiceTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _hubContextMock = new Mock<IHubContext<MapGenerationHub>>();
            _routeRegionsServiceMock = new Mock<IRouteRegionsService>();
            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

            // Setup DI container
            var services = new ServiceCollection();
            services.AddSingleton(_context);
            services.AddSingleton(_routeRegionsServiceMock.Object);
            _serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
        }

        [Fact]
        public async Task StartAsync_InitializesService()
        {
            // Arrange
            var service = new RefreshRoutesService(_serviceProvider, _hubContextMock.Object);

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert - Service should start without throwing
            Assert.NotNull(service);
        }

        [Fact]
        public async Task StopAsync_StopsService()
        {
            // Arrange
            var service = new RefreshRoutesService(_serviceProvider, _hubContextMock.Object);
            await service.StartAsync(CancellationToken.None);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert - Service should stop without throwing
            Assert.NotNull(service);
        }

        [Fact]
        public async Task RefreshRoutesAsync_UpdatesRoutes()
        {
            // Arrange
            var polygon = _geometryFactory.CreatePolygon(new Coordinate[]
            {
                new Coordinate(4.0, 52.0),
                new Coordinate(5.0, 52.0),
                new Coordinate(5.0, 53.0),
                new Coordinate(4.0, 53.0),
                new Coordinate(4.0, 52.0)
            });

            var region = new Region
            {
                Id = 1,
                Name = "Test Region",
                OsmRelationId = 100,
                Geometry = _geometryFactory.CreateMultiPolygon(new[] { polygon })
            };

            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.5, 52.5),
                new Coordinate(4.6, 52.6)
            });

            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                LineString = lineString,
                Regions = new[] { region }.ToList()
            };

            _context.Regions.Add(region);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            _routeRegionsServiceMock.Setup(s => s.AssignRegionsToRouteAsync(It.IsAny<Route>()))
                .ReturnsAsync(true);

            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            _hubContextMock.Setup(h => h.Clients).Returns(mockClients.Object);
            mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);

            var service = new RefreshRoutesService(_serviceProvider, _hubContextMock.Object);

            // Act
            await service.RefreshRoutesAsync(1);

            // Assert
            _routeRegionsServiceMock.Verify(s => s.AssignRegionsToRouteAsync(It.IsAny<Route>()), Times.Once);
        }
    }
}
