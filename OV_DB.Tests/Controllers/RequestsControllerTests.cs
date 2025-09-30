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
    public class RequestsControllerTests : IDisposable
    {
        private readonly OVDBDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly RequestsController _controller;

        public RequestsControllerTests()
        {
            var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new OVDBDatabaseContext(options);

            var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
            _mapper = config.CreateMapper();

            _controller = new RequestsController(_context, _mapper);
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
        public async Task GetUserRequests_WithValidUser_ReturnsRequests()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var request1 = new Request { Id = 1, UserId = 1, Message = "Request 1", Created = DateTime.Now, User = user };
            var request2 = new Request { Id = 2, UserId = 1, Message = "Request 2", Created = DateTime.Now, User = user };

            _context.Users.Add(user);
            _context.Requests.AddRange(request1, request2);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            // Act
            var result = await _controller.GetUserRequests();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var requests = Assert.IsAssignableFrom<List<RequestForUserDTO>>(okResult.Value);
            Assert.Equal(2, requests.Count);
        }

        [Fact]
        public async Task GetAdminRequests_AsAdmin_ReturnsAllRequests()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            var request = new Request { Id = 1, UserId = 1, Message = "Request", Created = DateTime.Now, User = user };

            _context.Users.Add(user);
            _context.Requests.Add(request);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1, isAdmin: true);

            // Act
            var result = await _controller.GetAdminRequests();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var requests = Assert.IsAssignableFrom<List<RequestForAdminDTO>>(okResult.Value);
            Assert.Single(requests);
        }

        [Fact]
        public async Task GetAdminRequests_AsNonAdmin_ReturnsForbid()
        {
            // Arrange
            SetupControllerWithUser(1, isAdmin: false);

            // Act
            var result = await _controller.GetAdminRequests();

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task CreateRequest_WithValidData_CreatesRequest()
        {
            // Arrange
            var user = new User { Id = 1, Email = "test@example.com", Guid = Guid.NewGuid() };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            SetupControllerWithUser(1);

            var createDto = new CreateRequestDTO { Message = "New request" };

            // Act
            var result = await _controller.CreateRequest(createDto);

            // Assert
            Assert.IsType<OkResult>(result);
            Assert.Single(await _context.Requests.ToListAsync());
        }
    }
}
