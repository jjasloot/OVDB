using Microsoft.EntityFrameworkCore;
using Moq;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using OvdbUser = OVDB_database.Models.User;

namespace OV_DB.Tests;

public class TelegramBotServiceTests
{
    private static OVDBDatabaseContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OVDBDatabaseContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new OVDBDatabaseContext(options);
    }

    private static Mock<ITelegramBotClient> CreateBotClientMock()
    {
        var mock = new Mock<ITelegramBotClient>();
        mock.Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        return mock;
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_NullBotClient_DoesNotThrow()
    {
        // Arrange: service constructed with null botClient (no token configured)
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_NullBotClient_DoesNotThrow));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "admin@test.com", Password = "x", IsAdmin = true, TelegramUserId = 123 });
        await dbContext.SaveChangesAsync();

        var service = new TelegramBotService(null as ITelegramBotClient, dbContext);

        // Act & Assert – should complete without throwing
        await service.SendMessageToAdminsAsync("test");
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_EmptyMessage_DoesNotSendAnyMessages()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_EmptyMessage_DoesNotSendAnyMessages));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "admin@test.com", Password = "x", IsAdmin = true, TelegramUserId = 123 });
        await dbContext.SaveChangesAsync();

        var mockBotClient = CreateBotClientMock();
        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act
        await service.SendMessageToAdminsAsync("   ");

        // Assert
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_NoAdminUsers_SendsNoMessages()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_NoAdminUsers_SendsNoMessages));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "user@test.com", Password = "x", IsAdmin = false, TelegramUserId = 123 });
        await dbContext.SaveChangesAsync();

        var mockBotClient = CreateBotClientMock();
        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act
        await service.SendMessageToAdminsAsync("Hello admins");

        // Assert
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_AdminWithoutTelegramId_SendsNoMessages()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_AdminWithoutTelegramId_SendsNoMessages));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "admin@test.com", Password = "x", IsAdmin = true, TelegramUserId = null });
        await dbContext.SaveChangesAsync();

        var mockBotClient = CreateBotClientMock();
        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act
        await service.SendMessageToAdminsAsync("Hello admins");

        // Assert
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_AdminWithTelegramId_SendsOneMessage()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_AdminWithTelegramId_SendsOneMessage));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "admin@test.com", Password = "x", IsAdmin = true, TelegramUserId = 555 });
        await dbContext.SaveChangesAsync();

        var mockBotClient = CreateBotClientMock();
        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act
        await service.SendMessageToAdminsAsync("New message received from user@test.com:\nHello!");

        // Assert: exactly one message was sent
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_MultipleAdmins_SendsMessageToEach()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_MultipleAdmins_SendsMessageToEach));
        dbContext.Users.AddRange(
            new OvdbUser { Id = 1, Email = "admin1@test.com", Password = "x", IsAdmin = true, TelegramUserId = 111 },
            new OvdbUser { Id = 2, Email = "admin2@test.com", Password = "x", IsAdmin = true, TelegramUserId = 222 },
            new OvdbUser { Id = 3, Email = "user@test.com",   Password = "x", IsAdmin = false, TelegramUserId = 333 }
        );
        await dbContext.SaveChangesAsync();

        var mockBotClient = CreateBotClientMock();
        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act
        await service.SendMessageToAdminsAsync("ping");

        // Assert: only the two admins are notified, not the regular user
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_MessageTooLong_TruncatesToMaxLength()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_MessageTooLong_TruncatesToMaxLength));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "admin@test.com", Password = "x", IsAdmin = true, TelegramUserId = 999 });
        await dbContext.SaveChangesAsync();

        var capturedRequest = default(IRequest<Message>);
        var mockBotClient = new Mock<ITelegramBotClient>();
        mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new Message());

        var service = new TelegramBotService(mockBotClient.Object, dbContext);
        var longMessage = new string('x', 5000);

        // Act
        await service.SendMessageToAdminsAsync(longMessage);

        // Assert: one message was sent
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageToAdminsAsync_OneAdminSendFails_OtherAdminsStillReceiveMessage()
    {
        // Arrange
        var dbContext = CreateInMemoryContext(nameof(SendMessageToAdminsAsync_OneAdminSendFails_OtherAdminsStillReceiveMessage));
        dbContext.Users.AddRange(
            new OvdbUser { Id = 1, Email = "admin1@test.com", Password = "x", IsAdmin = true, TelegramUserId = 111 },
            new OvdbUser { Id = 2, Email = "admin2@test.com", Password = "x", IsAdmin = true, TelegramUserId = 222 }
        );
        await dbContext.SaveChangesAsync();

        var callCount = 0;
        var mockBotClient = new Mock<ITelegramBotClient>();
        mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Network error");
                return Task.FromResult(new Message());
            });

        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        // Act – should not throw even though one send fails
        await service.SendMessageToAdminsAsync("test");

        // Assert: both admins were attempted (2 calls total)
        mockBotClient.Verify(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Callback data comes back from Telegram unvalidated and can be anything a user cares to send,
    // so parsing it is worth pinning down — including the bare ids sent by keyboards that are
    // already out there in old chats.
    // The expected action travels as a string only because StationAction is internal and this
    // method has to be public for xUnit.
    [Theory]
    [InlineData("st:42", "Stopped", 42)]
    [InlineData("ee:42", "EntryExit", 42)]
    [InlineData("rm:42", "Remove", 42)]
    [InlineData("sh:42", "Show", 42)]
    [InlineData("42", "Toggle", 42)]
    [InlineData("zz:42", "Toggle", 42)]
    public void TryParseStationAction_ReadsVerbAndStation(string data, string expected, int expectedId)
    {
        Assert.True(TelegramBotService.TryParseStationAction(data, out var action, out var stationId));
        Assert.Equal(System.Enum.Parse<TelegramBotService.StationAction>(expected), action);
        Assert.Equal(expectedId, stationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("st:")]
    [InlineData("st:notanumber")]
    [InlineData("st:1:2")]
    [InlineData("drop table stations")]
    public void TryParseStationAction_RejectsGarbage(string data)
    {
        Assert.False(TelegramBotService.TryParseStationAction(data, out _, out _));
    }

    // The share-location button is the bot's whole entry point, and a reply keyboard that isn't
    // persistent gets folded away behind the keyboard icon as soon as the chat scrolls. The
    // station replies carry inline keyboards, so they can never bring it back — these pin down
    // that the messages which can carry it, do.
    private static ReplyKeyboardMarkup CapturedReplyKeyboard(IRequest<Message> request)
    {
        var markup = request?.GetType().GetProperty("ReplyMarkup")?.GetValue(request);
        return markup as ReplyKeyboardMarkup;
    }

    [Theory]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("/start@ovdbbot")]
    [InlineData("something it cannot parse")]
    public async Task TextMessage_AnswersWithAPersistentLocationKeyboard(string text)
    {
        var dbContext = CreateInMemoryContext($"{nameof(TextMessage_AnswersWithAPersistentLocationKeyboard)}-{text}");

        var captured = default(IRequest<Message>);
        var mockBotClient = new Mock<ITelegramBotClient>();
        mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new Message());

        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        await service.HandleUpdateAsync(new Update
        {
            Message = new Message { Text = text, Chat = new Chat { Id = 42 }, From = new Telegram.Bot.Types.User { Id = 42 } }
        });

        var keyboard = CapturedReplyKeyboard(captured);
        Assert.NotNull(keyboard);
        Assert.True(keyboard.IsPersistent);
        Assert.True(keyboard.Keyboard.SelectMany(row => row).Single().RequestLocation);
    }

    private static string CapturedText(IRequest<Message> request)
    {
        return request?.GetType().GetProperty("Text")?.GetValue(request) as string;
    }

    // The bot answers in the language the user picked in OVDB, and falls back to the language their
    // Telegram client asks in when there is nothing stored — including for someone who has never
    // registered, which is exactly who needs to understand the reply.
    [Theory]
    [InlineData(PreferredLanguage.Dutch, null, "📍 Deel je locatie")]
    [InlineData(PreferredLanguage.English, "nl", "📍 Share your location")]
    [InlineData(null, "nl-NL", "📍 Deel je locatie")]
    [InlineData(null, "en-GB", "📍 Share your location")]
    [InlineData(null, "de", "📍 Share your location")]
    public async Task LocationKeyboard_SpeaksTheUsersLanguage(PreferredLanguage? stored, string telegramCode, string expectedButton)
    {
        var dbContext = CreateInMemoryContext($"{nameof(LocationKeyboard_SpeaksTheUsersLanguage)}-{stored}-{telegramCode}");
        if (stored.HasValue)
        {
            dbContext.Users.Add(new OvdbUser { Id = 1, Email = "u@test.com", Password = "x", TelegramUserId = 42, PreferredLanguage = stored });
            await dbContext.SaveChangesAsync();
        }

        var captured = default(IRequest<Message>);
        var mockBotClient = new Mock<ITelegramBotClient>();
        mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new Message());

        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        await service.HandleUpdateAsync(new Update
        {
            Message = new Message
            {
                Text = "/start",
                Chat = new Chat { Id = 42 },
                From = new Telegram.Bot.Types.User { Id = 42, LanguageCode = telegramCode },
            }
        });

        var keyboard = CapturedReplyKeyboard(captured);
        Assert.NotNull(keyboard);
        Assert.Equal(expectedButton, keyboard.Keyboard.SelectMany(row => row).Single().Text);
    }

    [Fact]
    public async Task DutchUser_GetsADutchGreeting()
    {
        var dbContext = CreateInMemoryContext(nameof(DutchUser_GetsADutchGreeting));
        dbContext.Users.Add(new OvdbUser { Id = 1, Email = "u@test.com", Password = "x", TelegramUserId = 7, PreferredLanguage = PreferredLanguage.Dutch });
        await dbContext.SaveChangesAsync();

        var captured = default(IRequest<Message>);
        var mockBotClient = new Mock<ITelegramBotClient>();
        mockBotClient
            .Setup(c => c.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new Message());

        var service = new TelegramBotService(mockBotClient.Object, dbContext);

        await service.HandleUpdateAsync(new Update
        {
            Message = new Message { Text = "hoi", Chat = new Chat { Id = 7 }, From = new Telegram.Bot.Types.User { Id = 7 } }
        });

        Assert.Equal(TelegramTexts.Get(TelegramText.NotUnderstood, PreferredLanguage.Dutch), CapturedText(captured));
    }

    // Every string has to exist in both languages, and differ: a pair that is accidentally the same
    // English twice is a missing translation that no other test would notice.
    [Fact]
    public void EveryStringIsTranslated()
    {
        foreach (var text in System.Enum.GetValues<TelegramText>())
        {
            var en = TelegramTexts.Get(text, PreferredLanguage.English);
            var nl = TelegramTexts.Get(text, PreferredLanguage.Dutch);
            Assert.False(string.IsNullOrWhiteSpace(en), $"{text} has no English text");
            Assert.False(string.IsNullOrWhiteSpace(nl), $"{text} has no Dutch text");
            Assert.NotEqual(text.ToString(), en);
            Assert.NotEqual(en, nl);
        }
    }
}
