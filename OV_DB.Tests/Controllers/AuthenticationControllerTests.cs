using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OV_DB.Controllers;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Security.Claims;

namespace OV_DB.Tests.Controllers
{
    public class AuthenticationControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AuthenticationController _controller;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthenticationControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            _configurationMock = new Mock<IConfiguration>();
            
            // Setup JWT configuration
            _configurationMock.Setup(c => c["JWTSigningKey"]).Returns("ThisIsAVeryLongSecretKeyForJWTTokenGenerationWithAtLeast256Bits");
            _configurationMock.Setup(c => c["Tokens:ValidityInMinutes"]).Returns("60");
            _configurationMock.Setup(c => c["Tokens:Issuer"]).Returns("OVDB");
            
            _controller = new AuthenticationController(_configurationMock.Object, _context);
            _passwordHasher = new PasswordHasher<User>();
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsToken()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                IsAdmin = false
            };
            user.Password = _passwordHasher.HashPassword(user, "Password123!");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            // Act
            var result = await _controller.LoginAsync(loginRequest);

            // Assert
            var okResult = Assert.IsType<ActionResult<LoginResponse>>(result);
            Assert.NotNull(okResult.Value);
            Assert.NotNull(okResult.Value.Token);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ReturnsForbid()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                IsAdmin = false
            };
            user.Password = _passwordHasher.HashPassword(user, "Password123!");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            // Act
            var result = await _controller.LoginAsync(loginRequest);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task LoginAsync_WithNonExistentUser_ReturnsForbid()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "Password123!"
            };

            // Act
            var result = await _controller.LoginAsync(loginRequest);

            // Assert
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyEmail_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "",
                Password = "Password123!"
            };

            // Act
            var result = await _controller.LoginAsync(loginRequest);

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyPassword_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = ""
            };

            // Act
            var result = await _controller.LoginAsync(loginRequest);

            // Assert
            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task LoginAsync_UpdatesLastLogin()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                IsAdmin = false,
                LastLogin = DateTime.UtcNow.AddDays(-10)
            };
            user.Password = _passwordHasher.HashPassword(user, "Password123!");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var oldLastLogin = user.LastLogin;

            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            // Act
            await _controller.LoginAsync(loginRequest);

            // Assert
            var updatedUser = await _context.Users.FindAsync(1);
            Assert.NotNull(updatedUser);
            Assert.True(updatedUser.LastLogin > oldLastLogin);
        }

        [Fact]
        public async Task RegisterUserAsync_WithValidData_CreatesUser()
        {
            // Arrange
            var createAccount = new CreateAccount
            {
                Email = "newuser@example.com",
                Password = "ValidPassword123!"
            };

            // Act
            var result = await _controller.RegisterUserAsync(createAccount);

            // Assert
            var okResult = Assert.IsType<ActionResult<LoginResponse>>(result);
            Assert.NotNull(okResult.Value);
            Assert.NotNull(okResult.Value.Token);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@example.com");
            Assert.NotNull(user);
            Assert.NotEmpty(user.Maps); // Should have default map
        }

        [Fact]
        public async Task RegisterUserAsync_WithShortPassword_ReturnsBadRequest()
        {
            // Arrange
            var createAccount = new CreateAccount
            {
                Email = "newuser@example.com",
                Password = "Short1!"
            };

            // Act
            var result = await _controller.RegisterUserAsync(createAccount);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Wachtwoord te kort", badRequestResult.Value);
        }

        [Fact]
        public async Task RegisterUserAsync_WithExistingEmail_ReturnsBadRequest()
        {
            // Arrange
            var existingUser = new User
            {
                Email = "existing@example.com",
                Guid = Guid.NewGuid()
            };
            existingUser.Password = _passwordHasher.HashPassword(existingUser, "Password123!");
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var createAccount = new CreateAccount
            {
                Email = "existing@example.com",
                Password = "NewPassword123!"
            };

            // Act
            var result = await _controller.RegisterUserAsync(createAccount);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Mailadres wordt al gebruikt", badRequestResult.Value);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithValidUser_ReturnsNewToken()
        {
            // Arrange
            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                IsAdmin = false,
                LastLogin = DateTime.UtcNow.AddHours(-1)
            };
            user.Password = _passwordHasher.HashPassword(user, "Password123!");
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim("email", "test@example.com"),
                new Claim("admin", "false")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            var request = new RefreshTokenRequest { RefreshToken = "old-token" };

            // Act
            var result = await _controller.RefreshTokenAsync(request);

            // Assert
            var okResult = Assert.IsType<ActionResult<LoginResponse>>(result);
            Assert.NotNull(okResult.Value);
            Assert.NotNull(okResult.Value.Token);
        }
    }
}
