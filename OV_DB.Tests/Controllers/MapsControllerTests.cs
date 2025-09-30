using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Controllers;
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
    public class MapsControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly MapsController _controller;

        public MapsControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _controller = new MapsController(_context);
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
        public async Task GetMaps_WithValidUser_ReturnsUserMaps()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var maps = new List<Map>
            {
                new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Map 1", UserId = 1, User = user, OrderNr = 1 },
                new Map { MapId = 2, MapGuid = Guid.NewGuid(), Name = "Map 2", UserId = 1, User = user, OrderNr = 2 }
            };

            _context.Users.Add(user);
            _context.Maps.AddRange(maps);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMaps();

            // Assert
            var mapList = Assert.IsAssignableFrom<IEnumerable<Map>>(result.Value);
            Assert.Equal(2, mapList.Count());
            Assert.All(mapList, m => Assert.Equal(1, m.UserId));
        }

        [Fact]
        public async Task GetMap_WithValidId_ReturnsMap()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMap(1);

            // Assert
            var mapValue = Assert.IsType<Map>(result.Value);
            Assert.Equal("Test Map", mapValue.Name);
        }

        [Fact]
        public async Task GetMap_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMap(999);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetMap_OtherUsersMap_ReturnsNotFound()
        {
            // Arrange
            var user1 = new User { Id = 1, Email = "user1@example.com", Guid = Guid.NewGuid() };
            var user2 = new User { Id = 2, Email = "user2@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "User2 Map", UserId = 2, User = user2 };

            _context.Users.AddRange(user1, user2);
            _context.Maps.Add(map);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMap(1);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
