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
    public class RefreshRoutesWithoutRegionsServiceTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<IHubContext<MapGenerationHub>> _hubContextMock;
        private readonly Mock<IRouteRegionsService> _routeRegionsServiceMock;
        private readonly GeometryFactory _geometryFactory;

        public RefreshRoutesWithoutRegionsServiceTests()
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
            var service = new RefreshRoutesWithoutRegionsService(_serviceProvider, _hubContextMock.Object);

            // Act
            await service.StartAsync(CancellationToken.None);

            // Assert - Service should start without throwing
            Assert.NotNull(service);
        }

        [Fact]
        public async Task StopAsync_StopsService()
        {
            // Arrange
            var service = new RefreshRoutesWithoutRegionsService(_serviceProvider, _hubContextMock.Object);
            await service.StartAsync(CancellationToken.None);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert - Service should stop without throwing
            Assert.NotNull(service);
        }
    }
}
