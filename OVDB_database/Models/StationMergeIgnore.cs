using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    /// <summary>
    /// Records a station pair that should not resurface in the merge queue (either merged or
    /// deliberately kept separate). <br/>
    /// <strong>Invariant:</strong> <c>Station1Id &lt; Station2Id</c> is always enforced by the
    /// application layer so that the unique index on (Station1Id, Station2Id) prevents duplicates
    /// regardless of argument order.
    /// </summary>
    [Index(nameof(Station1Id), nameof(Station2Id), IsUnique = true)]
    public class StationMergeIgnore
    {
        [Key]
        public long Id { get; set; }
        /// <summary>Always the smaller of the two station IDs.</summary>
        public int Station1Id { get; set; }
        public Station Station1 { get; set; }
        /// <summary>Always the larger of the two station IDs.</summary>
        public int Station2Id { get; set; }
        public Station Station2 { get; set; }
    }
}
