using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;

namespace OV_DB.Models;

public class MapDataDTO
{
    public FeatureCollection Routes { get; set; }

    public MapBoundsDTO? Area { get; set; }
}


public class MapBoundsDTO
{
    public Position SouthEast { get; set; }
    public Position NorthWest { get; set; }
}