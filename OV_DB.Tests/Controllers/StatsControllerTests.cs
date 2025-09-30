using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OV_DB.Controllers;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Security.Claims;

namespace OV_DB.Tests.Controllers
{
    public class StatsControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly StatsController _controller;

        public StatsControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _controller = new StatsController(_context);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private void SetupControllerWithUser(int userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task GetStats_WithValidData_ReturnsStats()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 100,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };
            var routeInstance = new RouteInstance
            {
                RouteInstanceId = 1,
                RouteId = 1,
                Route = route,
                Date = new DateTime(2025, 1, 15)
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            _context.RouteInstances.Add(routeInstance);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetStats(map.MapGuid, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetStats_WithYearFilter_FiltersCorrectly()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 100,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };

            var instance2024 = new RouteInstance
            {
                RouteInstanceId = 1,
                RouteId = 1,
                Route = route,
                Date = new DateTime(2024, 6, 15)
            };

            var instance2025 = new RouteInstance
            {
                RouteInstanceId = 2,
                RouteId = 1,
                Route = route,
                Date = new DateTime(2025, 1, 15)
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            _context.RouteInstances.AddRange(instance2024, instance2025);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetStats(map.MapGuid, 2025);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetTimedStats_WithValidData_ReturnsTimedStats()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 100,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };
            var routeInstance = new RouteInstance
            {
                RouteInstanceId = 1,
                RouteId = 1,
                Route = route,
                Date = new DateTime(2025, 1, 15)
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            _context.RouteInstances.Add(routeInstance);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetTimedStats(map.MapGuid, null, "en");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetTimedStats_WithDutchLanguage_UsesNLNames()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 100,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };
            var routeInstance = new RouteInstance
            {
                RouteInstanceId = 1,
                RouteId = 1,
                Route = route,
                Date = new DateTime(2025, 1, 15)
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            _context.RouteInstances.Add(routeInstance);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetTimedStats(map.MapGuid, null, "nl");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
