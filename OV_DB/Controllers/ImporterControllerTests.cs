using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using OV_DB.Controllers;
using OV_DB.Enum;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OV_DB.Tests
{
    public class ImporterControllerTests
    {
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<OVDBDatabaseContext> _mockDbContext;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IRouteRegionsService> _mockRouteRegionsService;
        private readonly ImporterController _controller;

        public ImporterControllerTests()
        {
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockDbContext = new Mock<OVDBDatabaseContext>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockRouteRegionsService = new Mock<IRouteRegionsService>();
            _controller = new ImporterController(_mockMemoryCache.Object, _mockDbContext.Object, _mockConfiguration.Object, _mockRouteRegionsService.Object);
        }

        [Fact]
        public async Task GetLinesAsync_ReturnsBadRequest_WhenReferenceIsNullOrEmpty()
        {
            // Arrange
            string reference = null;

            // Act
            var result = await _controller.GetLinesAsync(reference, null, null, null);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetLinesAsync_ReturnsOkResult_WhenReferenceIsValid()
        {
            // Arrange
            string reference = "validReference";
            var expectedResponse = new List<OSMLineDTO> { new OSMLineDTO { Name = "Test Line" } };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<List<OSMLineDTO>>>>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetLinesAsync(reference, null, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<List<OSMLineDTO>>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task GetNetworkLinesAsync_ReturnsBadRequest_WhenNetworkIsNullOrEmpty()
        {
            // Arrange
            string network = null;

            // Act
            var result = await _controller.GetNetworkLinesAsync(network, null);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GetNetworkLinesAsync_ReturnsOkResult_WhenNetworkIsValid()
        {
            // Arrange
            string network = "validNetwork";
            var expectedResponse = new List<OSMLineDTO> { new OSMLineDTO { Name = "Test Line" } };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<List<OSMLineDTO>>>>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetNetworkLinesAsync(network, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<List<OSMLineDTO>>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task GetStops_ReturnsOkResult_WithListOfStops()
        {
            // Arrange
            int id = 1;
            var expectedResponse = new List<OSMLineStop> { new OSMLineStop { Id = 1, Name = "Test Stop" } };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<OSM>>>())).ReturnsAsync(new OSM
            {
                Elements = new List<Element>
                {
                    new Element
                    {
                        Id = 1,
                        Type = TypeEnum.Relation,
                        Members = new List<Member>
                        {
                            new Member { Ref = 1, Role = "Platform" }
                        },
                        Tags = new Dictionary<string, string> { { "name", "Test Stop" } }
                    }
                }
            });

            // Act
            var result = await _controller.GetStops(id, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<List<OSMLineStop>>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task Read_ReturnsOkResult_WithOSMLineDTO()
        {
            // Arrange
            int id = 1;
            long from = 1;
            long to = 2;
            var expectedResponse = new OSMLineDTO { Id = 1, Name = "Test Line" };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<OSM>>>())).ReturnsAsync(new OSM
            {
                Elements = new List<Element>
                {
                    new Element
                    {
                        Id = 1,
                        Type = TypeEnum.Relation,
                        Members = new List<Member>
                        {
                            new Member { Ref = 1, Role = "Platform" }
                        },
                        Tags = new Dictionary<string, string> { { "name", "Test Line" } }
                    }
                }
            });

            // Act
            var result = await _controller.Read(id, from, to, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<OSMLineDTO>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task CreateCache_ReturnsOSM_WhenValidIdAndDateTime()
        {
            // Arrange
            int id = 1;
            DateTime? dateTime = DateTime.Now;
            var expectedResponse = new OSM
            {
                Elements = new List<Element>
                {
                    new Element
                    {
                        Id = 1,
                        Type = TypeEnum.Relation,
                        Members = new List<Member>
                        {
                            new Member { Ref = 1, Role = "Platform" }
                        },
                        Tags = new Dictionary<string, string> { { "name", "Test Line" } }
                    }
                }
            };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<OSM>>>())).ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.Read(id, 1, 2, dateTime);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<OSMLineDTO>(okResult.Value);
            Assert.Equal(expectedResponse.Elements[0].Tags["name"], actualResponse.Name);
        }

        [Fact]
        public async Task CreateCacheLines_ReturnsListOfOSMLines_WhenValidReferenceAndRouteType()
        {
            // Arrange
            string reference = "validReference";
            OSMRouteType? routeType = OSMRouteType.bus;
            var expectedResponse = new List<OSMLineDTO> { new OSMLineDTO { Name = "Test Line" } };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<List<OSMLineDTO>>>>())).ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetLinesAsync(reference, routeType, null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<List<OSMLineDTO>>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task CreateCacheLinesNetwork_ReturnsListOfOSMLines_WhenValidNetwork()
        {
            // Arrange
            string network = "validNetwork";
            var expectedResponse = new List<OSMLineDTO> { new OSMLineDTO { Name = "Test Line" } };
            _mockMemoryCache.Setup(m => m.GetOrCreateAsync(It.IsAny<object>(), It.IsAny<Func<ICacheEntry, Task<List<OSMLineDTO>>>>())).ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetNetworkLinesAsync(network, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualResponse = Assert.IsType<List<OSMLineDTO>>(okResult.Value);
            Assert.Equal(expectedResponse, actualResponse);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }
    }
}
