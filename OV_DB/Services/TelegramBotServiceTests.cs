//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using Moq;
//using OVDB_database.Database;
//using OVDB_database.Models;
//using Telegram.Bot;
//using Telegram.Bot.Types;
//using Telegram.Bot.Types.Enums;
//using Telegram.Bot.Types.ReplyMarkups;
//using Xunit;

//namespace OV_DB.Services.Tests
//{
//    public class TelegramBotServiceTests
//    {
//        private readonly Mock<IConfiguration> _mockConfiguration;
//        private readonly Mock<OVDBDatabaseContext> _mockDbContext;
//        private readonly Mock<DbSet<User>> _mockUserDbSet;
//        private readonly Mock<DbSet<Station>> _mockStationDbSet;
//        private readonly Mock<DbSet<StationVisit>> _mockStationVisitDbSet;
//        private readonly Mock<DbSet<StationCountry>> _mockStationCountryDbSet;
//        private readonly Mock<ITelegramBotClient> _mockBotClient;
//        private readonly TelegramBotService _telegramBotService;

//        public TelegramBotServiceTests()
//        {
//            _mockConfiguration = new Mock<IConfiguration>();
//            _mockDbContext = new Mock<OVDBDatabaseContext>();
//            _mockUserDbSet = new Mock<DbSet<User>>();
//            _mockStationDbSet = new Mock<DbSet<Station>>();
//            _mockStationVisitDbSet = new Mock<DbSet<StationVisit>>();
//            _mockStationCountryDbSet = new Mock<DbSet<StationCountry>>();
//            _mockBotClient = new Mock<ITelegramBotClient>();

//            _mockDbContext.Setup(m => m.Users).Returns(_mockUserDbSet.Object);
//            _mockDbContext.Setup(m => m.Stations).Returns(_mockStationDbSet.Object);
//            _mockDbContext.Setup(m => m.StationVisits).Returns(_mockStationVisitDbSet.Object);
//            _mockDbContext.Setup(m => m.StationCountries).Returns(_mockStationCountryDbSet.Object);

//            _telegramBotService = new TelegramBotService(_mockConfiguration.Object, _mockDbContext.Object);
//        }

//        [Fact]
//        public async Task HandleUpdateAsync_LocationMessage_ShouldReturnNearbyStations()
//        {
//            // Arrange
//            var update = new Update
//            {
//                Type = UpdateType.Message,
//                Message = new Message
//                {
//                    Type = MessageType.Location,
//                    From = new User { Id = 1 },
//                    Location = new Location { Latitude = 52.5200, Longitude = 13.4050 }
//                }
//            };

//            var user = new User { Id = 1, TelegramUserId = 1 };
//            var stations = new List<Station>
//            {
//                new Station { Id = 1, Name = "Station 1", Lattitude = 52.5200, Longitude = 13.4050, StationCountryId = 1, Visited = false },
//                new Station { Id = 2, Name = "Station 2", Lattitude = 52.5201, Longitude = 13.4051, StationCountryId = 1, Visited = true }
//            };

//            _mockUserDbSet.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Func<User, bool>>())).ReturnsAsync(user);
//            _mockStationDbSet.Setup(m => m.Where(It.IsAny<Func<Station, bool>>())).Returns(stations.AsQueryable().BuildMockDbSet().Object);

//            // Act
//            await _telegramBotService.HandleUpdateAsync(update);

//            // Assert
//            _mockBotClient.Verify(m => m.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains("Nearby stations:")), It.IsAny<ParseMode>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<IReplyMarkup>(), It.IsAny<CancellationToken>()), Times.Once);
//        }

//        [Fact]
//        public async Task HandleUpdateAsync_UnknownMessage_ShouldReturnExplanation()
//        {
//            // Arrange
//            var update = new Update
//            {
//                Type = UpdateType.Message,
//                Message = new Message
//                {
//                    Type = MessageType.Text,
//                    From = new User { Id = 1 },
//                    Text = "Unknown message"
//                }
//            };

//            // Act
//            await _telegramBotService.HandleUpdateAsync(update);

//            // Assert
//            _mockBotClient.Verify(m => m.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains("Sorry, I didn't understand that.")), It.IsAny<ParseMode>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<IReplyMarkup>(), It.IsAny<CancellationToken>()), Times.Once);
//        }

//        [Fact]
//        public async Task HandleUpdateAsync_UnknownUser_ShouldReturnExplanation()
//        {
//            // Arrange
//            var update = new Update
//            {
//                Type = UpdateType.Message,
//                Message = new Message
//                {
//                    Type = MessageType.Location,
//                    From = new User { Id = 1 },
//                    Location = new Location { Latitude = 52.5200, Longitude = 13.4050 }
//                }
//            };

//            _mockUserDbSet.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Func<User, bool>>())).ReturnsAsync((User)null);

//            // Act
//            await _telegramBotService.HandleUpdateAsync(update);

//            // Assert
//            _mockBotClient.Verify(m => m.SendTextMessageAsync(It.IsAny<ChatId>(), It.Is<string>(s => s.Contains("Sorry, I couldn't identify you.")), It.IsAny<ParseMode>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<IReplyMarkup>(), It.IsAny<CancellationToken>()), Times.Once);
//        }

//        [Fact]
//        public async Task HandleCallbackQueryAsync_ShouldToggleStationVisitStatus()
//        {
//            // Arrange
//            var callbackQuery = new CallbackQuery
//            {
//                Id = "1",
//                From = new User { Id = 1 },
//                Data = "1"
//            };

//            var user = new User { Id = 1, TelegramUserId = 1 };
//            var station = new Station { Id = 1, Name = "Station 1", Lattitude = 52.5200, Longitude = 13.4050, StationCountryId = 1, Visited = false };
//            var stationVisit = new StationVisit { StationId = 1, UserId = 1 };

//            _mockUserDbSet.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Func<User, bool>>())).ReturnsAsync(user);
//            _mockStationDbSet.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Func<Station, bool>>())).ReturnsAsync(station);
//            _mockStationVisitDbSet.Setup(m => m.SingleOrDefaultAsync(It.IsAny<Func<StationVisit, bool>>())).ReturnsAsync(stationVisit);

//            // Act
//            await _telegramBotService.HandleCallbackQueryAsync(callbackQuery);

//            // Assert
//            _mockStationVisitDbSet.Verify(m => m.Add(It.IsAny<StationVisit>()), Times.Once);
//            _mockStationVisitDbSet.Verify(m => m.Remove(It.IsAny<StationVisit>()), Times.Once);
//            _mockBotClient.Verify(m => m.AnswerCallbackQueryAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Station visit status updated.")), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
//        }
//    }
//}
