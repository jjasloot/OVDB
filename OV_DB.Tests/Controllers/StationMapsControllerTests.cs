using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Controllers;
using OV_DB.Mappings;
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
    public class StationMapsControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly StationMapsController _controller;

        public StationMapsControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);

            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            _controller = new StationMapsController(_context, _mapper);
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
        public async Task GetMaps_WithValidUser_ReturnsStationMaps()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var stationMap1 = new StationGrouping
            {
                Id = 1,
                Name = "Map 1",
                NameNL = "Kaart 1",
                UserId = 1,
                User = user,
                OrderNr = 1
            };
            var stationMap2 = new StationGrouping
            {
                Id = 2,
                Name = "Map 2",
                NameNL = "Kaart 2",
                UserId = 1,
                User = user,
                OrderNr = 2
            };

            _context.Users.Add(user);
            _context.StationGroupings.AddRange(stationMap1, stationMap2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMaps();

            // Assert
            var stationMaps = Assert.IsAssignableFrom<List<StationMapDTO>>(result.Value);
            Assert.Equal(2, stationMaps.Count);
        }

        [Fact]
        public async Task GetMap_WithValidId_ReturnsStationMap()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var stationMap = new StationGrouping
            {
                Id = 1,
                Name = "Test Map",
                NameNL = "Test Kaart",
                UserId = 1,
                User = user
            };

            _context.Users.Add(user);
            _context.StationGroupings.Add(stationMap);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMap(1);

            // Assert
            var mapDto = Assert.IsType<StationMapDTO>(result.Value);
            Assert.Equal("Test Map", mapDto.Name);
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
            var stationMap = new StationGrouping
            {
                Id = 1,
                Name = "User2 Map",
                UserId = 2,
                User = user2
            };

            _context.Users.AddRange(user1, user2);
            _context.StationGroupings.Add(stationMap);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetMap(1);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
