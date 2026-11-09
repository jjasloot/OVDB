using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Services
{
    /// <summary>
    /// Copies OSM station nodes onto Station rows. Shared by the single-station import and the
    /// whole-region station update so the two cannot drift apart.
    /// </summary>
    public static class OsmStationImport
    {
        /// <summary>
        /// The stations that already exist for these OSM nodes, keyed by OSM id. Regions are loaded
        /// because the upsert reassigns them; without them the old join rows are not seen, and
        /// re-adding them fails on a duplicate key.
        /// </summary>
        public static Task<Dictionary<long, Station>> LoadExistingAsync(OVDBDatabaseContext dbContext, OSMStationList list, CancellationToken cancellationToken = default)
        {
            var osmIds = list.Elements.Select(e => e.Id).ToList();
            return dbContext.Stations.Include(s => s.Regions)
                .Where(s => osmIds.Contains(s.OsmId))
                .ToDictionaryAsync(s => s.OsmId, cancellationToken);
        }

        /// <summary>Creates or updates the station for one OSM node. Nodes without a name or position are skipped.</summary>
        public static async Task UpsertAsync(OVDBDatabaseContext dbContext, Dictionary<long, Station> existing, OSMStationElement station, IStationRegionsService stationRegionsService)
        {
            if (!station.Tags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name) || (station.Lat == 0 && station.Lon == 0))
            {
                return;
            }
            if (!existing.TryGetValue(station.Id, out var stationToUpdate))
            {
                stationToUpdate = new Station { OsmId = station.Id };
                dbContext.Add(stationToUpdate);
                existing[station.Id] = stationToUpdate;
            }
            stationToUpdate.Lattitude = station.Lat;
            stationToUpdate.Longitude = station.Lon;
            stationToUpdate.Name = name;
            if (station.Tags.TryGetValue("ele", out var ele) && double.TryParse(ele, NumberStyles.Float, CultureInfo.InvariantCulture, out var elevation))
                stationToUpdate.Elevation = elevation;
            if (station.Tags.TryGetValue("network", out var network))
                stationToUpdate.Network = network;
            if (station.Tags.TryGetValue("operator", out var stationOperator))
                stationToUpdate.Operator = stationOperator;
            if (station.Tags.TryGetValue("usage", out var usage) && usage == "tourism")
                stationToUpdate.Special = true;
            await stationRegionsService.AssignRegionsToStationCacheRegionsAsync(stationToUpdate);
        }
    }
}
