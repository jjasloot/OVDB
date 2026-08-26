using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using OVDB_database.Database;
using OVDB_database.Enums;
using OVDB_database.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.ReplyMarkups;
using OV_DB.Models;
using OV_DB.Helpers;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OV_DB.Tests")]

namespace OV_DB.Services
{
    public class TelegramBotService
    {
        private const int TelegramMaxMessageLength = 4096;
        private readonly ITelegramBotClient _botClient;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TelegramBotService> _logger;
        private readonly IStationVisitService _stationVisitService;

        public TelegramBotService(IConfiguration configuration, IHttpClientFactory httpClientFactory, OVDBDatabaseContext dbContext, ILogger<TelegramBotService> logger, IStationVisitService stationVisitService)
        {
            _stationVisitService = stationVisitService;
            var token = configuration["TelegramBotToken"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Use the named HttpClient so the pooled handler is shared instead of creating a
                // fresh client (and socket) per request scope.
                _botClient = new TelegramBotClient(token, httpClientFactory.CreateClient("Telegram"));
            }
            _dbContext = dbContext;
            _logger = logger;
        }

        internal TelegramBotService(ITelegramBotClient botClient, OVDBDatabaseContext dbContext, ILogger<TelegramBotService> logger = null, IStationVisitService stationVisitService = null)
        {
            _botClient = botClient;
            _dbContext = dbContext;
            _logger = logger;
            _stationVisitService = stationVisitService;
        }

        public async Task SendMessageToAdminsAsync(string message)
        {
            if (_botClient == null)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            if (message.Length > TelegramMaxMessageLength)
                message = message[..TelegramMaxMessageLength];

            var adminTelegramIds = await _dbContext.Users
                .Where(u => u.IsAdmin && u.TelegramUserId.HasValue)
                .Select(u => u.TelegramUserId.Value)
                .ToListAsync();

            foreach (var telegramId in adminTelegramIds)
            {
                try
                {
                    await _botClient.SendMessage(telegramId, message);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send Telegram message to admin {TelegramId}", telegramId);
                }
            }
        }

        public async Task HandleUpdateAsync(Update update)
        {
            if (_botClient == null)
                return;
            if (update.Type == UpdateType.Message && update.Message.Type is MessageType.Location or MessageType.Venue)
            {
                await HandleLocationMessageAsync(update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
            }
            else if (update.Type == UpdateType.Message && IsCommand(update.Message))
            {
                await HandleCommandAsync(update.Message);
            }
            else if (update.Type == UpdateType.Message)
            {
                await HandleUnknownMessageAsync(update.Message);
            }
        }

        private async Task HandleLocationMessageAsync(Message message)
        {
            var userId = message.From.Id;
            var location = message.Location;

            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.TelegramUserId == userId);
            if (user == null)
            {
                await HandleUnknownUserAsync(message, LanguageFromTelegram(message.From?.LanguageCode));
                return;
            }
            var language = LanguageFor(user, message.From?.LanguageCode);

            var nearbyStations = await GetNearbyStationsAsync(location.Latitude, location.Longitude, user.Id);
            if (nearbyStations.Count == 0)
            {
                // An inline keyboard with no buttons would leave a header pointing at nothing
                await _botClient.SendMessage(message.Chat.Id, Text(TelegramText.NoStationsNearby, language),
                    replyMarkup: LocationKeyboard(language));
                return;
            }

            await _botClient.SendMessage(message.Chat.Id, Text(TelegramText.NearbyStations, language),
                replyMarkup: GetStationsInlineKeyboard(nearbyStations));
        }

        private string FormatStation(StationDTO station)
        {
            var flagEmoji = GetCountryFlagEmoji(station.Regions);
            return $"{station.Name} {flagEmoji} - {VisitMarker(station.Visited, station.VisitLevel)}";
        }

        /// <summary>Three states, the same three the web map paints.</summary>
        private static string VisitMarker(bool visited, StationVisitLevel? level)
        {
            if (!visited)
            {
                return "❌";
            }
            return level == StationVisitLevel.EntryExit ? "🚉" : "✅";
        }

        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            // callbackQuery.Data is attacker-influenced and Message may be null for old messages.
            if (!TryParseStationAction(callbackQuery.Data, out var action, out var stationId) || callbackQuery.Message == null)
            {
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "❌");
                return;
            }
            var userId = callbackQuery.From.Id;

            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.TelegramUserId == userId);
            if (user == null)
            {
                await HandleUnknownUserAsync(callbackQuery.Message, LanguageFromTelegram(callbackQuery.From?.LanguageCode));
                return;
            }
            var language = LanguageFor(user, callbackQuery.From?.LanguageCode);

            var station = await _dbContext.Stations.Include(s => s.Regions).SingleOrDefaultAsync(s => s.Id == stationId);
            if (station == null)
            {
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "❌");
                return;
            }

            var existing = await _stationVisitService.GetAsync(user.Id, stationId);
            switch (action)
            {
                case StationAction.Show:
                    // Opening an already-visited station changes nothing; the reply carries the
                    // options. Nothing here is destructive on a stray tap.
                    break;
                case StationAction.Remove:
                    await _stationVisitService.UnmarkAsync(user.Id, stationId);
                    break;
                case StationAction.EntryExit:
                    // Standing on the platform is today's news, so stamp today where the station is.
                    await _stationVisitService.MarkAsync(user.Id, stationId, StationVisitLevel.EntryExit,
                        await _stationVisitService.LocalDateAtStationAsync(station), StationVisitSource.Telegram);
                    break;
                case StationAction.Stopped:
                    if (existing?.FirstEntryExitDate != null)
                    {
                        await _stationVisitService.DowngradeToStoppedAsync(user.Id, stationId);
                    }
                    else
                    {
                        await _stationVisitService.MarkAsync(user.Id, stationId, StationVisitLevel.Stopped,
                            await _stationVisitService.LocalDateAtStationAsync(station), StationVisitSource.Telegram);
                    }
                    break;
                default:
                    // Keyboards sent before the verbs existed still toggle, which is what their
                    // labels promised at the time.
                    if (existing != null)
                    {
                        await _stationVisitService.UnmarkAsync(user.Id, stationId);
                    }
                    else
                    {
                        await _stationVisitService.MarkAsync(user.Id, stationId, StationVisitLevel.Stopped,
                            await _stationVisitService.LocalDateAtStationAsync(station), StationVisitSource.Telegram);
                    }
                    break;
            }

            {
                var regionIds = station.Regions.Select(r => r.Id).ToList();
                var percentageMessage = string.Empty;
                foreach (var region in regionIds)
                {
                    var totalStationsInRegion = await _dbContext.Stations.Where(s=>!s.Special && !s.Hidden).CountAsync(s => s.Regions.Any(r => r.Id== region));
                    var visitedStationsInRegion = await _dbContext.StationVisits.CountAsync(sv => sv.UserId == user.Id && sv.Station.Regions.Any(r => r.Id == region) && !sv.Station.Special && !sv.Station.Hidden);
                    // Regions carry both names; the bot says the one the user reads everywhere else.
                    var names = await _dbContext.Regions.Where(r => r.Id == region)
                        .Select(r => new { r.Name, r.NameNL })
                        .FirstOrDefaultAsync();
                    var regionName = language == PreferredLanguage.Dutch
                        ? names?.NameNL ?? names?.Name
                        : names?.Name;
                    var percentageVisited = Math.Round((double)visitedStationsInRegion / totalStationsInRegion * 100, 2);
                    percentageMessage += $"{regionName}: {percentageVisited}%\n\r";
                }

                var visit = await _stationVisitService.GetAsync(user.Id, stationId);
                var level = Text(visit == null
                    ? TelegramText.LevelNone
                    : visit.FirstEntryExitDate.HasValue ? TelegramText.LevelEntryExit : TelegramText.LevelStopped, language);

                await _botClient.SendMessage(callbackQuery.Message.Chat.Id,
                    $"{station.Name}: {level}\n\r{percentageMessage}",
                    replyMarkup: BuildStationActions(stationId, visit, language));
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "✅");
            }
        }

        internal enum StationAction
        {
            Toggle,
            Stopped,
            EntryExit,
            Remove,
            Show
        }

        /// <summary>
        /// Callback data is "&lt;verb&gt;:&lt;stationId&gt;", with a bare station id still accepted so
        /// keyboards sent before the verbs existed keep working. Well inside Telegram's 64-byte limit.
        /// </summary>
        internal static bool TryParseStationAction(string data, out StationAction action, out int stationId)
        {
            action = StationAction.Toggle;
            stationId = 0;
            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            var parts = data.Split(':');
            if (parts.Length == 1)
            {
                return int.TryParse(parts[0], out stationId);
            }
            if (parts.Length != 2 || !int.TryParse(parts[1], out stationId))
            {
                return false;
            }

            action = parts[0] switch
            {
                "st" => StationAction.Stopped,
                "ee" => StationAction.EntryExit,
                "rm" => StationAction.Remove,
                "sh" => StationAction.Show,
                _ => StationAction.Toggle
            };
            return true;
        }

        /// <summary>
        /// What can be done next: an unvisited station can be marked at either level, a stopped-at
        /// one raised, an entry/exit one corrected back down, and anything visited removed.
        /// </summary>
        private static InlineKeyboardMarkup BuildStationActions(int stationId, StationVisit visit, PreferredLanguage language)
        {
            var buttons = new List<InlineKeyboardButton>();
            if (visit == null)
            {
                buttons.Add(InlineKeyboardButton.WithCallbackData(Text(TelegramText.ActionStopped, language), $"st:{stationId}"));
                buttons.Add(InlineKeyboardButton.WithCallbackData(Text(TelegramText.ActionEntryExit, language), $"ee:{stationId}"));
            }
            else
            {
                buttons.Add(visit.FirstEntryExitDate.HasValue
                    ? InlineKeyboardButton.WithCallbackData(Text(TelegramText.ActionOnlyStopped, language), $"st:{stationId}")
                    : InlineKeyboardButton.WithCallbackData(Text(TelegramText.ActionEntryExit, language), $"ee:{stationId}"));
                buttons.Add(InlineKeyboardButton.WithCallbackData(Text(TelegramText.ActionRemove, language), $"rm:{stationId}"));
            }
            return new InlineKeyboardMarkup(buttons);
        }

        /// <summary>
        /// The one thing this bot needs from the phone. Persistent, because a reply keyboard that
        /// isn't gets folded away behind the keyboard icon the moment the chat scrolls, and the
        /// station replies carry inline keyboards of their own — they can't bring it back.
        /// </summary>
        private static ReplyKeyboardMarkup LocationKeyboard(PreferredLanguage language)
            => new(KeyboardButton.WithRequestLocation(Text(TelegramText.ShareLocationButton, language)))
            {
                IsPersistent = true,
                ResizeKeyboard = true,
            };

        private static string Text(TelegramText text, PreferredLanguage language) => TelegramTexts.Get(text, language);

        /// <summary>
        /// The language the user set in OVDB, falling back to the one their Telegram client asks in.
        /// A stored preference wins: it is the language they chose for this data.
        /// </summary>
        private static PreferredLanguage LanguageFor(OVDB_database.Models.User user, string telegramLanguageCode)
            => user?.PreferredLanguage ?? LanguageFromTelegram(telegramLanguageCode);

        /// <summary>Telegram sends IETF tags like "nl" or "en-GB"; only the language part counts.</summary>
        private static PreferredLanguage LanguageFromTelegram(string languageCode)
            => LanguageHelper.FromLanguageCode(languageCode?.Split('-')[0]);

        /// <summary>
        /// The language for someone who has sent something that loads no user: their stored
        /// preference if they are registered at all, otherwise their Telegram client's.
        /// </summary>
        private async Task<PreferredLanguage> LanguageForSenderAsync(Message message)
        {
            var telegramUserId = message.From?.Id;
            if (telegramUserId.HasValue)
            {
                var stored = await _dbContext.Users
                    .Where(u => u.TelegramUserId == telegramUserId.Value)
                    .Select(u => u.PreferredLanguage)
                    .FirstOrDefaultAsync();
                if (stored.HasValue)
                {
                    return stored.Value;
                }
            }
            return LanguageFromTelegram(message.From?.LanguageCode);
        }

        private static bool IsCommand(Message message)
        {
            return message.Text?.StartsWith('/') == true;
        }

        /// <summary>
        /// /start and /help both answer the same question, and both are worth answering with the
        /// keyboard attached: a fresh chat has no keyboard at all until some message brings one.
        /// </summary>
        private async Task HandleCommandAsync(Message message)
        {
            var language = await LanguageForSenderAsync(message);
            var command = message.Text.Split(' ', '@')[0].ToLowerInvariant();
            var responseText = Text(command is "/start" or "/help" ? TelegramText.Welcome : TelegramText.UnknownCommand, language);
            await _botClient.SendMessage(message.Chat.Id, responseText, replyMarkup: LocationKeyboard(language));
        }

        private async Task HandleUnknownMessageAsync(Message message)
        {
            var language = await LanguageForSenderAsync(message);
            await _botClient.SendMessage(message.Chat.Id, Text(TelegramText.NotUnderstood, language),
                replyMarkup: LocationKeyboard(language));
        }

        private async Task HandleUnknownUserAsync(Message message, PreferredLanguage language)
        {
            await _botClient.SendMessage(message.Chat.Id, Text(TelegramText.UnknownUser, language));
        }

        private async Task<List<StationDTO>> GetNearbyStationsAsync(double latitude, double longitude, int userId)
        {
            var nearbyStations = await _dbContext.Stations
                .Where(s => !s.Special && !s.Hidden)
                .OrderBy(s => (s.Lattitude - latitude) * (s.Lattitude - latitude) + (s.Longitude - longitude) * (s.Longitude - longitude))
                .Take(5)
                .Select(s => new StationDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Lattitude = s.Lattitude,
                    Longitude = s.Longitude,
                    Elevation = s.Elevation,
                    Network = s.Network,
                    Operator = s.Operator,
                    Visited = s.StationVisits.Any(sv => sv.UserId == userId),
                    VisitLevel = s.StationVisits
                        .Where(sv => sv.UserId == userId)
                        .Select(sv => sv.FirstEntryExitDate.HasValue
                            ? StationVisitLevel.EntryExit
                            : (StationVisitLevel?)StationVisitLevel.Stopped)
                        .FirstOrDefault(),
                    Regions = s.Regions.Select(r => new StationRegionDTO
                    {
                        Id = r.Id,
                        OriginalName = r.OriginalName,
                        HasParentRegion = r.ParentRegionId.HasValue,
                        FlagEmoji = r.FlagEmoji
                    })
                })
                .ToListAsync();

            return nearbyStations;
        }

        /// <summary>
        /// An unvisited station is one tap from being marked as stopped at; a visited one opens its
        /// options instead, so a mistaken tap in the list can never un-mark anything.
        /// </summary>
        private InlineKeyboardButton[][] GetStationsInlineKeyboard(List<StationDTO> stations)
        {
            var inlineKeyboardButtons = stations
                .Select(s => InlineKeyboardButton.WithCallbackData(FormatStation(s), $"{(s.Visited ? "sh" : "st")}:{s.Id}"))
                .Select(b => new InlineKeyboardButton[] { b })
                .ToArray();
            return inlineKeyboardButtons;
        }

        private string GetCountryFlagEmoji(IEnumerable<StationRegionDTO> regions)
        {
            var headRegion = regions.FirstOrDefault(r => !r.HasParentRegion);
            if (headRegion == null)
                return string.Empty;

            var subregionsWithFlags = regions.Where(r => r.HasParentRegion && !string.IsNullOrWhiteSpace(r.FlagEmoji));
            var flags = headRegion.FlagEmoji;
            if (subregionsWithFlags.Any())
            {
                flags += " ";
                flags += string.Join(" ", subregionsWithFlags.Select(r => r.FlagEmoji));
            }
            return flags;
        }
    }
}
