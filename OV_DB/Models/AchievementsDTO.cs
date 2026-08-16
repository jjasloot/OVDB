using System;
using System.Collections.Generic;

namespace OV_DB.Models
{
    public class AchievementsDTO
    {
        public List<AchievementFamilyDTO> Families { get; set; } = [];
    }

    /// <summary>
    /// One tiered achievement (e.g. total distance), with only the tier the user is working
    /// towards surfaced - later tiers exist but are deliberately not shown prominently.
    /// </summary>
    public class AchievementFamilyDTO
    {
        public string Key { get; set; }
        public string Icon { get; set; }
        /// <summary>Formatting hint for the frontend: "km", "kmh", "hours" or "count".</summary>
        public string Unit { get; set; }
        public double CurrentValue { get; set; }
        public int EarnedTiers { get; set; }
        public int TotalTiers { get; set; }

        /// <summary>Highest tier reached, null when nothing is earned yet.</summary>
        public AchievementTierDTO CurrentTier { get; set; }
        /// <summary>The tier being worked towards, null once every tier is earned.</summary>
        public AchievementTierDTO NextTier { get; set; }
        /// <summary>0..1 towards <see cref="NextTier"/>, measured from the previous threshold.</summary>
        public double ProgressToNext { get; set; }
    }

    public class AchievementTierDTO
    {
        public int Tier { get; set; }
        public double Threshold { get; set; }
        public bool Earned { get; set; }
        /// <summary>
        /// The date the threshold was first crossed. Available for anything derived from trips;
        /// null for values that cannot be dated from the current schema.
        /// </summary>
        public DateTime? EarnedOn { get; set; }
    }
}
