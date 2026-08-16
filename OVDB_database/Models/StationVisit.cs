using Microsoft.EntityFrameworkCore;
using OVDB_database.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    /// <summary>
    /// A station the user has visited. The row existing *is* the visit: un-marking deletes it, so
    /// there is no state in which a row means "not visited". Dates may be filled in later by the
    /// backfill review, so an undated row is a normal, valid visit rather than an incomplete one.
    /// </summary>
    [Index(nameof(StationId), nameof(UserId), IsUnique = true)]
    [Index(nameof(UserId), nameof(FirstStoppedDate))]
    [Index(nameof(UserId), nameof(FirstEntryExitDate))]
    public class StationVisit
    {
        [Key]
        public long Id { get; set; }
        public int StationId { get; set; }
        public Station Station { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }

        /// <summary>
        /// Local civil date, at midnight, on which a train the user was aboard first stopped here.
        /// Null when not yet known. Getting on or off implies stopping, so this is never later than
        /// <see cref="FirstEntryExitDate"/> when both are known.
        /// </summary>
        public DateTime? FirstStoppedDate { get; set; }

        /// <summary>
        /// Local civil date, at midnight, on which the user first got on or off here. Null when not
        /// yet known.
        /// </summary>
        /// <remarks>
        /// The visit level is derived from which of the two dates are set — neither means undated,
        /// stopped-only means stopped at, and this one set means got on/off — so there is no level
        /// enum that can drift out of step with the dates.
        /// </remarks>
        public DateTime? FirstEntryExitDate { get; set; }

        /// <summary>Trip that established <see cref="FirstStoppedDate"/>, when known.</summary>
        public int? FirstStoppedRouteInstanceId { get; set; }
        public RouteInstance FirstStoppedRouteInstance { get; set; }

        /// <summary>Trip that established <see cref="FirstEntryExitDate"/>, when known.</summary>
        public int? FirstEntryExitRouteInstanceId { get; set; }
        public RouteInstance FirstEntryExitRouteInstance { get; set; }

        public StationVisitSource Source { get; set; }

        /// <summary>When the row was created. Null means it predates visit history.</summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// Set when the user declined to date this visit, so the backfill review stops offering it.
        /// </summary>
        public bool DatingSkipped { get; set; }
    }
}
