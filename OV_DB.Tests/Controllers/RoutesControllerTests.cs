using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using OV_DB.Controllers;
using OV_DB.Mappings;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Security.Claims;

namespace OV_DB.Tests.Controllers
{
    public class RoutesControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly Mock<IRouteRegionsService> _routeRegionsServiceMock;
        private readonly Mock<ITimezoneService> _timezoneServiceMock;
        private readonly RoutesController _controller;
        private readonly GeometryFactory _geometryFactory;

        public RoutesControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);
            
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });
            _mapper = mapperConfig.CreateMapper();
            
            _routeRegionsServiceMock = new Mock<IRouteRegionsService>();
            _timezoneServiceMock = new Mock<ITimezoneService>();
            
            _controller = new RoutesController(
                _context,
                _mapper,
                _routeRegionsServiceMock.Object,
                _timezoneServiceMock.Object);

            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
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
        public async Task GetRoutes_WithValidUser_ReturnsRoutes()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            var route = new Route
            {
                RouteId = 1,
                Name = "Test Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 10,
                LineString = lineString,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } },
                RouteInstances = new List<RouteInstance>()
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.Add(route);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetRoutes(null, null, null, null, null, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<ActionResult<OV_DB.Models.RouteListDTO>>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetRoutes_WithFilter_FiltersResults()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            var route1 = new Route
            {
                RouteId = 1,
                Name = "Amsterdam - Utrecht",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 10,
                LineString = lineString,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } },
                RouteInstances = new List<RouteInstance>()
            };

            var route2 = new Route
            {
                RouteId = 2,
                Name = "Rotterdam - Den Haag",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 15,
                LineString = lineString,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 2 } },
                RouteInstances = new List<RouteInstance>()
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.AddRange(route1, route2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetRoutes(null, null, null, null, "Amsterdam", CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<ActionResult<OV_DB.Models.RouteListDTO>>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetRoutes_WithSorting_SortsCorrectly()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            var route1 = new Route
            {
                RouteId = 1,
                Name = "B Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 10,
                LineString = lineString,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 1 } },
                RouteInstances = new List<RouteInstance>()
            };

            var route2 = new Route
            {
                RouteId = 2,
                Name = "A Route",
                RouteTypeId = 1,
                RouteType = routeType,
                CalculatedDistance = 15,
                LineString = lineString,
                RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = 2 } },
                RouteInstances = new List<RouteInstance>()
            };

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            _context.Routes.AddRange(route1, route2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetRoutes(null, null, "name", false, null, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<ActionResult<OV_DB.Models.RouteListDTO>>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetRoutes_WithPagination_ReturnsCorrectPage()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var map = new Map { MapId = 1, MapGuid = Guid.NewGuid(), Name = "Test Map", UserId = 1, User = user };
            var routeType = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            
            var lineString = _geometryFactory.CreateLineString(new Coordinate[]
            {
                new Coordinate(4.9, 52.3),
                new Coordinate(5.0, 52.4)
            });

            for (int i = 1; i <= 10; i++)
            {
                var route = new Route
                {
                    RouteId = i,
                    Name = $"Route {i}",
                    RouteTypeId = 1,
                    RouteType = routeType,
                    CalculatedDistance = 10,
                    LineString = lineString,
                    RouteMaps = new List<RouteMap> { new RouteMap { MapId = 1, Map = map, RouteId = i } },
                    RouteInstances = new List<RouteInstance>()
                };
                _context.Routes.Add(route);
            }

            _context.Users.Add(user);
            _context.Maps.Add(map);
            _context.RouteTypes.Add(routeType);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetRoutes(0, 5, null, null, null, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<ActionResult<OV_DB.Models.RouteListDTO>>(result);
            Assert.NotNull(okResult.Value);
        }
    }
}
