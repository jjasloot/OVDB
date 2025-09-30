using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using OV_DB.Controllers;
using OV_DB.Mappings;
using OVDB_database.Database;
using OVDB_database.Models;
using System.Security.Claims;

namespace OV_DB.Tests.Controllers
{
    public class RouteTypesControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly RouteTypesController _controller;

        public RouteTypesControllerTests()
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
            
            _controller = new RouteTypesController(_context);
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
        public async Task GetRouteTypes_WithValidUser_ReturnsRouteTypes()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var routeType1 = new RouteType { TypeId = 1, Name = "Train", NameNL = "Trein", Colour = "#FF0000", UserId = 1 };
            var routeType2 = new RouteType { TypeId = 2, Name = "Bus", NameNL = "Bus", Colour = "#00FF00", UserId = 1 };

            _context.Users.Add(user);
            _context.RouteTypes.AddRange(routeType1, routeType2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetRouteTypes();

            // Assert
            var routeTypes = Assert.IsAssignableFrom<IEnumerable<RouteType>>(result.Value);
            Assert.Equal(2, routeTypes.Count());
        }
    }
}
