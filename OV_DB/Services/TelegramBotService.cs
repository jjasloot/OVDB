using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using OVDB_database.Database;
using OVDB_database.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types.ReplyMarkups;
using OV_DB.Models;

namespace OV_DB.Services
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly OVDBDatabaseContext _dbContext;

        public TelegramBotService(IConfiguration configuration, OVDBDatabaseContext dbContext)
        {
            _botClient = new TelegramBotClient(configuration["TelegramBotToken"]);
            _dbContext = dbContext;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            if (update.Type == UpdateType.Message && update.Message.Type is MessageType.Location or MessageType.Venue)
            {
                await HandleLocationMessageAsync(update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
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
                await HandleUnknownUserAsync(message);
                return;
            }

            var nearbyStations = await GetNearbyStationsAsync(location.Latitude, location.Longitude, user.Id);

            var responseText = "Nearby stations:\n";
            await _botClient.SendMessage(message.Chat.Id, responseText, replyMarkup: GetStationsInlineKeyboard(nearbyStations));
        }

        private string FormatStation(StationDTO station)
        {
            var flagEmoji = GetCountryFlagEmoji(station.Regions);
            return $"{station.Name} {flagEmoji} - {(station.Visited ? "✅" : "❌")}";
        }

        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var stationId = int.Parse(callbackQuery.Data);
            var userId = callbackQuery.From.Id;

            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.TelegramUserId == userId);
            if (user == null)
            {
                await HandleUnknownUserAsync(callbackQuery.Message);
                return;
            }

            var stationVisit = await _dbContext.StationVisits.SingleOrDefaultAsync(sv => sv.StationId == stationId && sv.UserId == user.Id);
            var visited = false;
            if (stationVisit == null)
            {
                _dbContext.StationVisits.Add(new StationVisit { StationId = stationId, UserId = user.Id });
                visited = true;
            }
            else
            {
                _dbContext.StationVisits.Remove(stationVisit);
            }

            await _dbContext.SaveChangesAsync();

            var station = await _dbContext.Stations.Include(s => s.Regions).SingleOrDefaultAsync(s => s.Id == stationId);
            if (station != null)
            {
                var regionIds = station.Regions.Select(r => r.Id).ToList();
                var percentageMessage = string.Empty;
                foreach (var region in regionIds)
                {
                    var totalStationsInRegion = await _dbContext.Stations.Where(s=>!s.Special && !s.Hidden).CountAsync(s => s.Regions.Any(r => r.Id== region));
                    var visitedStationsInRegion = await _dbContext.StationVisits.CountAsync(sv => sv.UserId == user.Id && sv.Station.Regions.Any(r => r.Id == region) && !sv.Station.Special && !sv.Station.Hidden);
                    var regionName = await _dbContext.Regions.Where(r => r.Id == region).Select(r => r.Name).FirstOrDefaultAsync();
                    var percentageVisited = Math.Round((double)visitedStationsInRegion / totalStationsInRegion * 100, 2);
                    percentageMessage += $"{regionName}: {percentageVisited}%\n\r";
                }

                await _botClient.SendMessage(callbackQuery.Message.Chat.Id, $"""{station.Name}: {(visited? "✅": "❌")}"""+ $"\n\r{percentageMessage}", replyMarkup: KeyboardButton.WithRequestLocation("Share your location"));
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "✅");
            }
            else
            {
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, "❌");
            }
        }

        private async Task HandleUnknownMessageAsync(Message message)
        {
            var responseText = "Sorry, I didn't understand that. Please share your location to find nearby stations.";
            await _botClient.SendMessage(message.Chat.Id, responseText, replyMarkup:  KeyboardButton.WithRequestLocation("Share your location"));
        }

        private async Task HandleUnknownUserAsync(Message message)
        {
            var responseText = "Sorry, I couldn't identify you. Please make sure you have registered your Telegram user ID.";
            await _botClient.SendMessage(message.Chat.Id, responseText);
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

        private InlineKeyboardButton[][] GetStationsInlineKeyboard(List<StationDTO> stations)
        {
            var inlineKeyboardButtons = stations.Select(s => InlineKeyboardButton.WithCallbackData(FormatStation(s), s.Id.ToString()))
                .Select(b => new InlineKeyboardButton[] { b })
                .ToArray();
            return inlineKeyboardButtons;
        }

        private string GetCountryFlagEmoji(IEnumerable<StationRegionDTO> regions)
        {
            var headRegion = regions.FirstOrDefault(r => !r.HasParentRegion);
            if (headRegion == null)
                return string.Empty;

            return headRegion.FlagEmoji;
        }
    }
}
