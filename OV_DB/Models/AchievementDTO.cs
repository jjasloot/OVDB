using System;

namespace OV_DB.Models
{
    public class AchievementDTO
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string NameNL { get; set; }
        public string Description { get; set; }
        public string DescriptionNL { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public string IconName { get; set; }
        public string IconUrl { get; set; }
        public int ThresholdValue { get; set; }
        public int CurrentProgress { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public int? Year { get; set; }
    }
}
