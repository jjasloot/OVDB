using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests.Services
{
    public class TrawellingServiceTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ITimezoneService> _timezoneServiceMock;
        private readonly Mock<ILogger<TrawellingService>> _loggerMock;
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;

        public TrawellingServiceTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);

            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["Traewelling:BaseUrl"]).Returns("https://traewelling.de");
            _configurationMock.Setup(c => c["Traewelling:ClientId"]).Returns("test-client-id");
            _configurationMock.Setup(c => c["Traewelling:ClientSecret"]).Returns("test-secret");
            _configurationMock.Setup(c => c["Traewelling:RedirectUri"]).Returns("https://localhost/callback");
            _configurationMock.Setup(c => c["Traewelling:AuthorizeUrl"]).Returns("https://traewelling.de/oauth/authorize");
            _configurationMock.Setup(c => c["Traewelling:TokenUrl"]).Returns("https://traewelling.de/oauth/token");

            _timezoneServiceMock = new Mock<ITimezoneService>();
            _loggerMock = new Mock<ILogger<TrawellingService>>();
            _cacheMock = new Mock<IMemoryCache>();

            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _httpClient?.Dispose();
        }

        [Fact]
        public void GetAuthorizationUrl_ReturnsValidUrl()
        {
            // Arrange
            var service = new TrawellingService(_httpClient, _configurationMock.Object,
                _timezoneServiceMock.Object, _context, _loggerMock.Object, _cacheMock.Object);

            // Act
            var url = service.GetAuthorizationUrl(1, "test-state");

            // Assert
            Assert.Contains("https://traewelling.de/oauth/authorize", url);
            Assert.Contains("client_id=test-client-id", url);
            Assert.Contains("state=test-state", url);
            Assert.Contains("response_type=code", url);
        }

        [Fact]
        public void GenerateAndStoreState_ReturnsValidState()
        {
            // Arrange
            var service = new TrawellingService(_httpClient, _configurationMock.Object,
                _timezoneServiceMock.Object, _context, _loggerMock.Object, _cacheMock.Object);

            // Act
            var state = service.GenerateAndStoreState(1);

            // Assert
            Assert.NotNull(state);
            Assert.NotEmpty(state);
            Assert.True(Guid.TryParse(state, out _));
        }

        [Fact]
        public async Task ExchangeCodeForToken_WithValidCode_ReturnsToken()
        {
            // Arrange
            var responseJson = @"{
                ""access_token"": ""test-access-token"",
                ""refresh_token"": ""test-refresh-token"",
                ""expires_in"": 3600
            }";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });

            var service = new TrawellingService(_httpClient, _configurationMock.Object,
                _timezoneServiceMock.Object, _context, _loggerMock.Object, _cacheMock.Object);

            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var state = service.GenerateAndStoreState(1);

            // Act
            var result = await service.ExchangeCodeForTokensAsync("test-code", state, 1);

            // Assert
            Assert.True(result);
            var updatedUser = await _context.Users.FindAsync(1);
            Assert.NotNull(updatedUser);
            Assert.Equal("test-access-token", updatedUser.TrawellingAccessToken);
        }

        [Fact]
        public async Task RefreshToken_WithValidToken_UpdatesToken()
        {
            // Arrange
            var responseJson = @"{
                ""access_token"": ""new-access-token"",
                ""refresh_token"": ""new-refresh-token"",
                ""expires_in"": 3600
            }";

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });

            var service = new TrawellingService(_httpClient, _configurationMock.Object,
                _timezoneServiceMock.Object, _context, _loggerMock.Object, _cacheMock.Object);

            var user = new User
            {
                Id = 1,
                Email = "test@example.com",
                Guid = Guid.NewGuid(),
                TrawellingAccessToken = "old-token",
                TrawellingRefreshToken = "old-refresh-token"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await service.RefreshTokensAsync(user);

            // Assert
            Assert.True(result);
            var updatedUser = await _context.Users.FindAsync(1);
            Assert.NotNull(updatedUser);
            Assert.Equal("new-access-token", updatedUser.TrawellingAccessToken);
        }
    }
}
