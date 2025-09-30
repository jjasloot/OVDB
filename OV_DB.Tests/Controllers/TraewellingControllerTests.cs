using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OV_DB.Controllers;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests.Controllers
{
    public class TraewellingControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<ITrawellingService> _trawellingServiceMock;
        private readonly Mock<ILogger<TraewellingController>> _loggerMock;
        private readonly TraewellingController _controller;

        public TraewellingControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _trawellingServiceMock = new Mock<ITrawellingService>();
            _loggerMock = new Mock<ILogger<TraewellingController>>();

            _controller = new TraewellingController(
                _trawellingServiceMock.Object,
                _context,
                _loggerMock.Object);
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
        public void GetConnectUrl_WithValidUser_ReturnsAuthUrl()
        {
            // Arrange
            SetupControllerWithUser(1);

            _trawellingServiceMock
                .Setup(s => s.GenerateAndStoreState(1))
                .Returns("test-state");

            _trawellingServiceMock
                .Setup(s => s.GetAuthorizationUrl(1, "test-state"))
                .Returns("https://traewelling.de/oauth/authorize?state=test-state");

            // Act
            var result = _controller.GetConnectUrl();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Disconnect_WithValidUser_DisconnectsAccount()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                TrawellingAccessToken = "token",
                TrawellingRefreshToken = "refresh"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.DisconnectAccount();

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var updatedUser = await _context.Users.FindAsync(1);
            Assert.NotNull(updatedUser);
            Assert.Null(updatedUser.TrawellingAccessToken);
            Assert.Null(updatedUser.TrawellingRefreshToken);
        }

        [Fact]
        public async Task GetConnectionStatus_WithConnectedUser_ReturnsStatus()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                TrawellingAccessToken = "token"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            _trawellingServiceMock
                .Setup(s => s.HasValidTokens(It.IsAny<User>()))
                .Returns(true);

            // Act
            var result = await _controller.GetConnectionStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
