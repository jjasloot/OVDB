using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OVDB_database.Models
{
    public enum TrawellingInboxState
    {
        Pending = 0,
        ChangedAfterImport = 1,
        DeletedUpstream = 2,
    }

    public enum TrawellingInboxSource
    {
        Sweep = 0,
        Webhook = 1,
    }

    /// <summary>
    /// A Träwelling check-in that is known but not yet imported: the queue behind the
    /// "unimported trips" list. Rows are deleted on import/ignore — imported and ignored
    /// state lives on RouteInstance.TrawellingStatusId and TrawellingIgnoredStatuses.
    /// The raw StatusResource JSON is stored verbatim and parsed on read, so mapping
    /// fixes apply retroactively to everything still in the inbox.
    /// </summary>
    [Index(nameof(UserId), nameof(TrawellingStatusId), IsUnique = true)]
    public class TrawellingInboxStatus
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int TrawellingStatusId { get; set; }

        [Required]
        public string PayloadJson { get; set; }

        public TrawellingInboxState State { get; set; }

        public TrawellingInboxSource Source { get; set; }

        /// <summary>Denormalised from the payload so the list can sort without parsing JSON.</summary>
        public DateTime? DepartureAt { get; set; }

        public DateTime ReceivedAt { get; set; }

        public DateTime LastEventAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
    }
}
