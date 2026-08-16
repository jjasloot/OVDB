using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    /// <summary>
    /// "Do not suggest this station to me again." Written when the user actively declines a station
    /// proposed after importing a route, so the same station is not offered on every subsequent trip
    /// through it.
    /// </summary>
    /// <remarks>
    /// Deliberately its own table rather than a flag on <see cref="StationVisit"/>: a dismissal is
    /// not a visit, and a visit row that means "not visited" would be a trap for anyone reading the
    /// data later.
    /// </remarks>
    [Index(nameof(UserId), nameof(StationId), IsUnique = true)]
    public class StationSuggestionDismissal
    {
        [Key]
        public long Id { get; set; }
        public int StationId { get; set; }
        public Station Station { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public DateTime DismissedOn { get; set; }
    }
}
