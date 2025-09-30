using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Controllers;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests.Controllers
{
    public class TripReportControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly TripReportController _controller;

        public TripReportControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _controller = new TripReportController(_context);
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
        public async Task GetTripReport_WithValidData_ReturnsExcelFile()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                NameNL = "Test Route NL",
                RouteTypeId = 1,
                RouteType = routeType,
                From = "Amsterdam",
                To = "Rotterdam",
                CalculatedDistance = 100
            };
            var routeMap = new RouteMap { MapId = 1, Map = map, RouteId = 1, Route = route };
            route.RouteMaps = new List<RouteMap> { routeMap };

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
            var result = await _controller.GetTripReport(new List<Guid> { map.MapGuid }, 2025, false);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
        }

        [Fact]
        public async Task GetTripReport_WithEnglish_ReturnsEnglishExcel()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetTripReport(new List<Guid> { map.MapGuid }, 2025, true);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.NotNull(fileResult.FileContents);
        }
    }
}
