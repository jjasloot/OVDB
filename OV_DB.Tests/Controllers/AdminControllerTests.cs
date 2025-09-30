using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
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
    public class AdminControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _configurationMock = new Mock<IConfiguration>();

            _controller = new AdminController(_context, _configurationMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private void SetupControllerWithUser(int userId, bool isAdmin = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("admin", isAdmin.ToString().ToLower())
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Fact]
        public async Task GetAdministratorUsers_AsAdmin_ReturnsUserList()
        {
            // Arrange
            var user1 = new User { Id = 1, Email = "user1@example.com", Guid = Guid.NewGuid(), IsAdmin = true };
            var user2 = new User { Id = 2, Email = "user2@example.com", Guid = Guid.NewGuid(), IsAdmin = false };

            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1, isAdmin: true);

            // Act
            var result = await _controller.GetAdministratorUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<List<AdminUser>>(okResult.Value);
            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task GetAdministratorUsers_AsNonAdmin_ReturnsForbid()
        {
            // Arrange
            SetupControllerWithUser(1, isAdmin: false);

            // Act
            var result = await _controller.GetAdministratorUsers();

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task AddMissingGuidsForRoutes_AsAdmin_AddsGuids()
        {
            // Arrange
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                Share = Guid.Empty
            };

            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1, isAdmin: true);

            // Act
            var result = await _controller.AddMissingGuidsForRoutes();

            // Assert
            Assert.IsType<OkResult>(result);

            var updatedRoute = await _context.Routes.FindAsync(1);
            Assert.NotEqual(Guid.Empty, updatedRoute.Share);
        }

        [Fact]
        public async Task AddMissingGuidsForRoutes_AsNonAdmin_ReturnsForbid()
        {
            // Arrange
            SetupControllerWithUser(1, isAdmin: false);

            // Act
            var result = await _controller.AddMissingGuidsForRoutes();

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
    }
}
