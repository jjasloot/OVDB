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
    public class ImporterControllerTests2
    {
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<OVDBDatabaseContext> _mockDbContext;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IRouteRegionsService> _mockRouteRegionsService;
        private readonly ImporterController _controller;

        public ImporterControllerTests2()
        {
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockDbContext = new Mock<OVDBDatabaseContext>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockRouteRegionsService = new Mock<IRouteRegionsService>();
            _controller = new ImporterController(_mockMemoryCache.Object, _mockDbContext.Object, _mockConfiguration.Object, _mockRouteRegionsService.Object);
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

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithRoundabout()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithReversedList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
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

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithEmptyList()
        {
            // Arrange
            var input = new List<List<IPosition>>();

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithSingleList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleLists()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndRoundabout()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndReversedList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndEmptyList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition>(),
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndSingleList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndMultipleRoundabouts()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(7, 7) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(7, 7) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithMultipleListsAndMultipleReversedLists()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(5, 5), new Position(4, 4) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6) },
                new List<IPosition> { new Position(7, 7), new Position(6, 6) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6) },
                new List<IPosition> { new Position(6, 6), new Position(7, 7) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrder()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3) }
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

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndRoundabout()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3), new Position(4, 4) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4), new Position(3, 3) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndReversedList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3) }
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

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndEmptyList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition>(),
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3) }
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

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndSingleList()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndMultipleRoundabouts()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(5, 5), new Position(4, 4) },
                new List<IPosition> { new Position(6, 6), new Position(5, 5), new Position(6, 6) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4), new Position(3, 3) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6), new Position(5, 5) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }

        [Fact]
        public async Task SortListOfList_ReturnsSortedLists_WithDifferentOrderAndMultipleReversedLists()
        {
            // Arrange
            var input = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(3, 3), new Position(2, 2) },
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(4, 4), new Position(3, 3) },
                new List<IPosition> { new Position(5, 5), new Position(4, 4) },
                new List<IPosition> { new Position(6, 6), new Position(5, 5) },
                new List<IPosition> { new Position(7, 7), new Position(6, 6) }
            };
            var expectedOutput = new List<List<IPosition>>
            {
                new List<IPosition> { new Position(1, 1), new Position(2, 2) },
                new List<IPosition> { new Position(2, 2), new Position(3, 3) },
                new List<IPosition> { new Position(3, 3), new Position(4, 4) },
                new List<IPosition> { new Position(4, 4), new Position(5, 5) },
                new List<IPosition> { new Position(5, 5), new Position(6, 6) },
                new List<IPosition> { new Position(6, 6), new Position(7, 7) }
            };

            // Act
            var result = _controller.SortListOfList(input);

            // Assert
            Assert.Equal(expectedOutput, result);
        }
    }
}
