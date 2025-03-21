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
            if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Location)
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
            foreach (var station in nearbyStations)
            {
                var flagEmoji = GetCountryFlagEmoji(station.StationCountryId);
                responseText += $"{flagEmoji} {station.Name} - {(station.Visited ? "Visited" : "Not visited")}\n";
            }

            await _botClient.SendTextMessageAsync(message.Chat.Id, responseText, replyMarkup: GetStationsInlineKeyboard(nearbyStations));
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
            if (stationVisit == null)
            {
                _dbContext.StationVisits.Add(new StationVisit { StationId = stationId, UserId = user.Id });
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
                var totalStationsInRegion = await _dbContext.Stations.CountAsync(s => s.Regions.Any(r => regionIds.Contains(r.Id)));
                var visitedStationsInRegion = await _dbContext.StationVisits.CountAsync(sv => sv.UserId == user.Id && sv.Station.Regions.Any(r => regionIds.Contains(r.Id)));
                var percentageVisited = (double)visitedStationsInRegion / totalStationsInRegion * 100;

                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"Station visit status updated. {percentageVisited}% of stations visited in the region.");
            }
            else
            {
                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Station visit status updated.");
            }
        }

        private async Task HandleUnknownMessageAsync(Message message)
        {
            var responseText = "Sorry, I didn't understand that. Please share your location to find nearby stations.";
            await _botClient.SendTextMessageAsync(message.Chat.Id, responseText);
        }

        private async Task HandleUnknownUserAsync(Message message)
        {
            var responseText = "Sorry, I couldn't identify you. Please make sure you have registered your Telegram user ID.";
            await _botClient.SendTextMessageAsync(message.Chat.Id, responseText);
        }

        private async Task<List<StationDTO>> GetNearbyStationsAsync(double latitude, double longitude, int userId)
        {
            var nearbyStations = await _dbContext.Stations
                .Where(s => s.Lattitude >= latitude - 0.05 && s.Lattitude <= latitude + 0.05 && s.Longitude >= longitude - 0.05 && s.Longitude <= longitude + 0.05)
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
                    StationCountryId = s.StationCountryId
                })
                .ToListAsync();

            return nearbyStations;
        }

        private InlineKeyboardMarkup GetStationsInlineKeyboard(List<StationDTO> stations)
        {
            var inlineKeyboardButtons = stations.Select(s => InlineKeyboardButton.WithCallbackData(s.Name, s.Id.ToString())).ToArray();
            return new InlineKeyboardMarkup(inlineKeyboardButtons);
        }

        private string GetCountryFlagEmoji(int? countryId)
        {
            if (!countryId.HasValue)
            {
                return string.Empty;
            }

            var country = _dbContext.StationCountries.SingleOrDefault(c => c.Id == countryId.Value);
            if (country == null)
            {
                return string.Empty;
            }

            return country.FlagEmoji;
        }
    }
}
