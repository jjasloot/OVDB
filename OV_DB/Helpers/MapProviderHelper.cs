using OVDB_database.Enums;

namespace OV_DB.Helpers
{
    public static class MapProviderHelper
    {
        public static string ToMapProviderString(this PreferredMapProvider mapProvider)
        {
            return mapProvider switch
            {
                PreferredMapProvider.Leaflet => "leaflet",
                PreferredMapProvider.MapLibre => "maplibre",
                _ => "leaflet"
            };
        }

        public static PreferredMapProvider FromMapProviderString(string mapProviderString)
        {
            return mapProviderString?.ToLower() switch
            {
                "maplibre" => PreferredMapProvider.MapLibre,
                "leaflet" => PreferredMapProvider.Leaflet,
                _ => PreferredMapProvider.Leaflet
            };
        }
    }
}
