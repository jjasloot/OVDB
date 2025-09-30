using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Controllers;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests.Controllers
{
    public class MapFilterControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly MapFilterController _controller;

        public MapFilterControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _controller = new MapFilterController(_context);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task GetYearsAsync_WithValidMap_ReturnsYears()
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
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };
            var instance1 = new RouteInstance { RouteInstanceId = 1, RouteId = 1, Route = route, Date = new DateTime(2023, 1, 1) };
            var instance2 = new RouteInstance { RouteInstanceId = 2, RouteId = 1, Route = route, Date = new DateTime(2024, 1, 1) };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            _context.RouteInstances.AddRange(instance1, instance2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetYearsAsync(map.MapGuid.ToString());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var years = Assert.IsAssignableFrom<List<int>>(okResult.Value);
            Assert.Contains(2023, years);
            Assert.Contains(2024, years);
        }

        [Fact]
        public async Task GetYearsAsync_WithInvalidMap_ReturnsNotFound()
        {
            // Arrange
            var invalidGuid = Guid.NewGuid();

            // Act
            var result = await _controller.GetYearsAsync(invalidGuid.ToString());

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetTypesAsync_WithValidMap_ReturnsRouteTypes()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1, OrderNr = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } }
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetTypesAsync(map.MapGuid.ToString());

            // Assert
            var types = Assert.IsAssignableFrom<List<RouteType>>(result.Value);
            Assert.Single(types);
            Assert.Equal("Train", types[0].Name);
        }

        [Fact]
        public async Task GetTypesAsync_WithInvalidMap_ReturnsNotFound()
        {
            // Arrange
            var invalidGuid = Guid.NewGuid();

            // Act
            var result = await _controller.GetTypesAsync(invalidGuid.ToString());

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
