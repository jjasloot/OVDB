using Microsoft.Extensions.DependencyInjection;
using OV_DB.IntegrationTests.Infrastructure;
using OV_DB.Models;
using OVDB_database.Database;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.IntegrationTests.Controllers
{
    [Collection("Database")]
    public class AuthenticationControllerIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _databaseFixture;
        private OvdbWebApplicationFactory _factory;
        private HttpClient _client;

        public AuthenticationControllerIntegrationTests(DatabaseFixture databaseFixture)
        {
            _databaseFixture = databaseFixture;
        }

        public async Task InitializeAsync()
        {
            _factory = new OvdbWebApplicationFactory
            {
                ConnectionString = _databaseFixture.ConnectionString
            };

            _client = _factory.CreateClient();

            // Clean database before each test
            await _databaseFixture.ResetDatabaseAsync();
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }
        }

        [Fact]
        public async Task CreateAccount_WithValidData_CreatesUserAndReturnsToken()
        {
            // Arrange
            var createAccountRequest = new
            {
                Email = "integration.test@example.com",
                Password = "Test123!Password"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Authentication/register", createAccountRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotEmpty(result.Token);

            // Verify user was created in database
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var user = context.Users.FirstOrDefault(u => u.Email == "integration.test@example.com");
            Assert.NotNull(user);
            Assert.Equal("integration.test@example.com", user.Email);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsToken()
        {
            // Arrange - Create a user first
            var createRequest = new
            {
                Email = "login.test@example.com",
                Password = "Test123!Password"
            };
            await _client.PostAsJsonAsync("/api/Authentication/register", createRequest);

            var loginRequest = new
            {
                Email = "login.test@example.com",
                Password = "Test123!Password"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Authentication/Login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotEmpty(result.Token);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Authentication/Login", loginRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateAccount_WithDuplicateEmail_ReturnsBadRequest()
        {
            // Arrange - Create first user
            var firstRequest = new
            {
                Email = "duplicate@example.com",
                Password = "Test123!Password"
            };
            await _client.PostAsJsonAsync("/api/Authentication/register", firstRequest);

            // Try to create duplicate
            var duplicateRequest = new
            {
                Email = "duplicate@example.com",
                Password = "DifferentPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/Authentication/register", duplicateRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
