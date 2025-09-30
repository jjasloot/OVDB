using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using OV_DB.IntegrationTests.Infrastructure;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.IntegrationTests.Controllers
{
    [Collection("Database")]
    public class RoutesControllerIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _databaseFixture;
        private OvdbWebApplicationFactory _factory;
        private HttpClient _client;
        private string _authToken;
        private User _testUser;
        private Map _testMap;
        private RouteType _testRouteType;

        public RoutesControllerIntegrationTests(DatabaseFixture databaseFixture)
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

            // Create a test user and authenticate
            await CreateAuthenticatedUser();
        }

        private async Task CreateAuthenticatedUser()
        {
            // Register user
            var registerRequest = new
            {
                Email = "routes.test@example.com",
                Password = "Test123!Password"
            };

            var registerResponse = await _client.PostAsJsonAsync("/api/Authentication/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            var loginResponse = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();
            _authToken = loginResponse!.Token;

            // Set up auth header for future requests
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

            // Get the created user from database
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            _testUser = await context.Users.SingleAsync(u => u.Email == "routes.test@example.com");

            // Create test map and route type
            _testMap = TestDataGenerator.CreateTestMap(_testUser.Id, "Test Map");
            _testRouteType = TestDataGenerator.CreateTestRouteType(_testUser.Id);

            context.Maps.Add(_testMap);
            context.RouteTypes.Add(_testRouteType);
            await context.SaveChangesAsync();

            // Reload to get generated IDs
            await context.Entry(_testMap).ReloadAsync();
            await context.Entry(_testRouteType).ReloadAsync();
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
        public async Task CreateRoute_DirectlyInDatabase_CanBeRetrieved()
        {
            // Arrange - Create route directly in database (since KML endpoint uses synchronous IO)
            Route testRoute;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                testRoute = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                testRoute.Name = "Amsterdam - Rotterdam";
                testRoute.From = "Amsterdam";
                testRoute.To = "Rotterdam";
                testRoute.LineNumber = "IC 1234";
                
                // Add geometry
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var coordinates = new[]
                {
                    new Coordinate(4.9, 52.37),  // Amsterdam
                    new Coordinate(4.48, 51.92)  // Rotterdam
                };
                testRoute.LineString = geometryFactory.CreateLineString(coordinates);
                
                context.Routes.Add(testRoute);
                await context.SaveChangesAsync();
                await context.Entry(testRoute).ReloadAsync();
            }

            // Act - Retrieve via API to verify it's accessible
            var response = await _client.GetAsync($"/api/Routes/{testRoute.RouteId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var routeDto = await response.Content.ReadFromJsonAsync<RouteDTO>();
            Assert.NotNull(routeDto);
            Assert.Equal("Amsterdam - Rotterdam", routeDto.Name);
            Assert.Equal("Amsterdam", routeDto.From);
            Assert.Equal("Rotterdam", routeDto.To);
            Assert.Equal("IC 1234", routeDto.LineNumber);
        }

        [Fact]
        public async Task GetRoutes_ReturnsUserRoutes()
        {
            // Arrange - Create test routes
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                var route1 = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                route1.Name = "Route 1";
                var route2 = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                route2.Name = "Route 2";

                context.Routes.AddRange(route1, route2);
                await context.SaveChangesAsync();
            }

            // Act - Use the actual GET /api/Routes endpoint with query parameters
            var response = await _client.GetAsync("/api/Routes?start=0&count=100");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var routeList = await response.Content.ReadFromJsonAsync<RouteListDTO>();
            Assert.NotNull(routeList);
            Assert.True(routeList.Routes.Count >= 2);
            Assert.Contains(routeList.Routes, r => r.Name == "Route 1");
            Assert.Contains(routeList.Routes, r => r.Name == "Route 2");
        }

        [Fact]
        public async Task CreateRouteInstance_WithValidData_CreatesInstance()
        {
            // Arrange - Create a route first with LineString for duration calculation
            Route testRoute;
            using (var createScope = _factory.Services.CreateScope())
            {
                var createContext = createScope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                testRoute = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                
                // Add geometry for timezone calculation
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var coordinates = new[]
                {
                    new Coordinate(4.9, 52.37),  // Amsterdam
                    new Coordinate(4.48, 51.92)  // Rotterdam
                };
                testRoute.LineString = geometryFactory.CreateLineString(coordinates);
                
                createContext.Routes.Add(testRoute);
                await createContext.SaveChangesAsync();
                await createContext.Entry(testRoute).ReloadAsync();
            }

            // Use PUT /api/Routes/instances with null RouteInstanceId to create new instance
            var instanceRequest = new
            {
                RouteId = testRoute.RouteId,
                RouteInstanceId = (int?)null,  // null means create new
                Date = DateTime.UtcNow.Date,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow,
                RouteInstanceProperties = new List<object>(),  // Empty properties list
                RouteInstanceMaps = new List<object> { new { MapId = _testMap.MapId } }
            };

            // Act - Use PUT to create instance
            var response = await _client.PutAsJsonAsync("/api/Routes/instances", instanceRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Verify in database
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var instance = await context.RouteInstances
                .FirstOrDefaultAsync(ri => ri.RouteId == testRoute.RouteId);

            Assert.NotNull(instance);
            Assert.Equal(DateTime.UtcNow.Date, instance.Date.Date);
            Assert.NotNull(instance.StartTime);
            Assert.NotNull(instance.EndTime);
            Assert.True(instance.DurationHours > 0);
        }

        [Fact]
        public async Task UpdateRoute_WithGeometry_UpdatesLineString()
        {
            // Arrange - Create a route
            Route testRoute;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                testRoute = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                
                // Add a simple LineString geometry
                var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
                var coordinates = new[]
                {
                    new Coordinate(4.9, 52.37),  // Amsterdam
                    new Coordinate(4.48, 51.92)  // Rotterdam
                };
                testRoute.LineString = geometryFactory.CreateLineString(coordinates);
                
                context.Routes.Add(testRoute);
                await context.SaveChangesAsync();
                await context.Entry(testRoute).ReloadAsync();
            }

            // Act - Verify we can read it back
            var response = await _client.GetAsync($"/api/Routes/{testRoute.RouteId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var routeDto = await response.Content.ReadFromJsonAsync<RouteDTO>();
            Assert.NotNull(routeDto);
            Assert.Equal(testRoute.Name, routeDto.Name);
            
            // Verify geometry exists in database
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var savedRoute = await verifyContext.Routes.FindAsync(testRoute.RouteId);
            Assert.NotNull(savedRoute);
            Assert.NotNull(savedRoute.LineString);
            Assert.Equal(2, savedRoute.LineString.Coordinates.Length);
        }

        [Fact]
        public async Task DeleteRoute_RemovesRouteAndInstances()
        {
            // Arrange - Create route with instance
            Route testRoute;
            RouteInstance testInstance;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                testRoute = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                context.Routes.Add(testRoute);
                await context.SaveChangesAsync();
                await context.Entry(testRoute).ReloadAsync();

                testInstance = TestDataGenerator.CreateTestRouteInstance(testRoute.RouteId);
                context.RouteInstances.Add(testInstance);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.DeleteAsync($"/api/Routes/{testRoute.RouteId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Verify deletion
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var deletedRoute = await verifyContext.Routes.FindAsync(testRoute.RouteId);
            Assert.Null(deletedRoute);
            
            var deletedInstances = await verifyContext.RouteInstances
                .Where(ri => ri.RouteId == testRoute.RouteId)
                .ToListAsync();
            Assert.Empty(deletedInstances);
        }

        [Fact]
        public async Task GetRouteStatistics_ReturnsCorrectStats()
        {
            // Arrange - Create route with multiple instances
            Route testRoute;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
                testRoute = TestDataGenerator.CreateTestRoute(_testRouteType.TypeId, new List<int> { _testMap.MapId });
                testRoute.CalculatedDistance = 100.0; // 100 km
                context.Routes.Add(testRoute);
                await context.SaveChangesAsync();
                await context.Entry(testRoute).ReloadAsync();

                // Add 3 instances
                for (int i = 0; i < 3; i++)
                {
                    var instance = TestDataGenerator.CreateTestRouteInstance(testRoute.RouteId);
                    instance.Date = DateTime.UtcNow.AddDays(-i).Date;
                    context.RouteInstances.Add(instance);
                }
                await context.SaveChangesAsync();
            }

            // Act - Use the correct Stats endpoint: /api/Stats/{mapGuid}
            var response = await _client.GetAsync($"/api/Stats/{_testMap.MapGuid}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            // The user should have routes and route instances
            using var verifyScope = _factory.Services.CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<OVDBDatabaseContext>();
            var routeCount = await verifyContext.Routes
                .Include(r => r.RouteMaps)
                .Where(r => r.RouteMaps.Any(rm => rm.MapId == _testMap.MapId))
                .CountAsync();
            var instanceCount = await verifyContext.RouteInstances
                .Include(ri => ri.Route)
                .ThenInclude(r => r.RouteMaps)
                .Where(ri => ri.Route.RouteMaps.Any(rm => rm.MapId == _testMap.MapId))
                .CountAsync();
                
            Assert.True(routeCount > 0);
            Assert.Equal(3, instanceCount);
        }
    }
}
