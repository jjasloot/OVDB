using OVDB_database.Enums;

namespace OV_DB.Helpers
{
    public static class LanguageHelper
    {
        public static string ToLanguageCode(this PreferredLanguage language)
        {
            return language switch
            {
                PreferredLanguage.English => "en",
                PreferredLanguage.Dutch => "nl",
                _ => "en"
            };
        }

        public static PreferredLanguage FromLanguageCode(string languageCode)
        {
            return languageCode?.ToLower() switch
            {
                "nl" => PreferredLanguage.Dutch,
                "en" => PreferredLanguage.English,
                _ => PreferredLanguage.English
            };
        }
    }
}