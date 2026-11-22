using System;
using System.Collections.Generic;
using System.Linq;
using OV_DB.Models;
using OVDB_database.Models;

namespace OV_DB.Services
{
    /// <summary>
    /// How OVDB refers to a Träwelling status: its UUID when known, else its numeric id (rows from
    /// before UUIDs that no sweep has stamped yet). Matching accepts either, so a numeric key from an
    /// old tab still finds a row that has since been given its UUID.
    /// </summary>
    public readonly record struct TraewellingStatusKey(int? Id, string Uuid)
    {
        public bool IsEmpty => Id == null && Uuid == null;

        /// <summary>A key from a client: a GUID matches on the UUID, a positive number on the numeric id.</summary>
        public static TraewellingStatusKey Parse(string key) =>
            Guid.TryParse(key, out var guid) ? new(null, guid.ToString("D"))
            : int.TryParse(key, out var id) && id > 0 ? new(id, null)
            : default;

        public static TraewellingStatusKey Of(TrawellingStatus status) =>
            new(status.Id > 0 ? status.Id : null, string.IsNullOrEmpty(status.Uuid) ? null : status.Uuid);

        public static TraewellingStatusKey Of(TrawellingInboxStatus row) => new(row.TrawellingStatusId, row.TrawellingStatusUuid);

        /// <summary>The UUID when known, else the number; also what Träwelling's /status/{id} accepts.</summary>
        public override string ToString() => Uuid ?? Id?.ToString();
    }

    /// <summary>A set of statuses that contains a key when either its number or its UUID was added.</summary>
    public class KnownStatuses
    {
        private readonly HashSet<int> _ids = new();
        private readonly HashSet<string> _uuids = new(StringComparer.OrdinalIgnoreCase);

        public bool IsEmpty => _ids.Count == 0 && _uuids.Count == 0;

        public void Add(TraewellingStatusKey key)
        {
            if (key.Id.HasValue)
                _ids.Add(key.Id.Value);
            if (key.Uuid != null)
                _uuids.Add(key.Uuid);
        }

        public void AddRange(IEnumerable<TraewellingStatusKey> keys)
        {
            foreach (var key in keys)
                Add(key);
        }

        public bool Contains(TraewellingStatusKey key) =>
            (key.Id.HasValue && _ids.Contains(key.Id.Value)) || (key.Uuid != null && _uuids.Contains(key.Uuid));
    }

    public static class TraewellingStatusKeyQueries
    {
        public static IQueryable<TrawellingInboxStatus> WhereStatus(this IQueryable<TrawellingInboxStatus> query, TraewellingStatusKey key)
        {
            var (id, uuid) = key;
            return query.Where(s => (uuid != null && s.TrawellingStatusUuid == uuid) || (id != null && s.TrawellingStatusId == id));
        }

        public static IQueryable<TrawellingIgnoredStatus> WhereStatus(this IQueryable<TrawellingIgnoredStatus> query, TraewellingStatusKey key)
        {
            var (id, uuid) = key;
            return query.Where(s => (uuid != null && s.TrawellingStatusUuid == uuid) || (id != null && s.TrawellingStatusId == id));
        }

        public static IQueryable<RouteInstance> WhereStatus(this IQueryable<RouteInstance> query, TraewellingStatusKey key)
        {
            var (id, uuid) = key;
            return query.Where(ri => (uuid != null && ri.TrawellingStatusUuid == uuid) || (id != null && ri.TrawellingStatusId == id));
        }
    }
}
