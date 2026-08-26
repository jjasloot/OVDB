using OVDB_database.Enums;

namespace OV_DB.Services
{
    /// <summary>Every string the Telegram bot says to a user.</summary>
    internal enum TelegramText
    {
        ShareLocationButton,
        Welcome,
        UnknownCommand,
        NotUnderstood,
        UnknownUser,
        NoStationsNearby,
        NearbyStations,
        ActionStopped,
        ActionEntryExit,
        ActionOnlyStopped,
        ActionRemove,
        LevelEntryExit,
        LevelStopped,
        LevelNone,
    }

    /// <summary>
    /// The bot's two languages side by side. The frontend's ngx-translate JSON is unreachable from
    /// the server and one bot does not earn a resx pipeline, so the pairs live here where they can
    /// be read against each other — a translation that drifts is visible on the same line.
    /// </summary>
    internal static class TelegramTexts
    {
        public static string Get(TelegramText text, PreferredLanguage language)
        {
            var (en, nl) = text switch
            {
                TelegramText.ShareLocationButton => ("📍 Share your location", "📍 Deel je locatie"),
                TelegramText.Welcome => ("Share your location and I'll list the stations nearest to you, with what you have already marked at each.",
                    "Deel je locatie en ik geef de stations het dichtst bij je, met wat je er al hebt aangegeven."),
                TelegramText.UnknownCommand => ("I take locations, not commands. Share your location to find nearby stations.",
                    "Ik werk met locaties, niet met commando's. Deel je locatie om stations in de buurt te vinden."),
                TelegramText.NotUnderstood => ("Sorry, I didn't understand that. Please share your location to find nearby stations.",
                    "Sorry, dat begreep ik niet. Deel je locatie om stations in de buurt te vinden."),
                TelegramText.UnknownUser => ("Sorry, I couldn't identify you. Please make sure you have registered your Telegram user ID.",
                    "Sorry, ik kon je niet herkennen. Controleer of je je Telegram-gebruikers-ID hebt vastgelegd."),
                TelegramText.NoStationsNearby => ("No stations known anywhere near there.", "Geen stations bekend in die buurt."),
                TelegramText.NearbyStations => ("Nearby stations:\n", "Stations in de buurt:\n"),
                TelegramText.ActionStopped => ("Stopped at", "Gestopt"),
                TelegramText.ActionEntryExit => ("Got on/off", "In-/uitgestapt"),
                TelegramText.ActionOnlyStopped => ("Only stopped at", "Alleen gestopt"),
                TelegramText.ActionRemove => ("Remove", "Verwijderen"),
                TelegramText.LevelEntryExit => ("🚉 got on/off", "🚉 in-/uitgestapt"),
                TelegramText.LevelStopped => ("✅ stopped at", "✅ gestopt"),
                TelegramText.LevelNone => ("❌ not visited", "❌ niet bezocht"),
                _ => (text.ToString(), text.ToString()),
            };
            return language == PreferredLanguage.Dutch ? nl : en;
        }
    }
}
