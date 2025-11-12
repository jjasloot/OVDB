using System.Collections.Generic;

namespace OV_DB.Models
{
    public class AchievementProgressDTO
    {
        public string Category { get; set; }
        public int CurrentValue { get; set; }
        public List<AchievementDTO> Achievements { get; set; }
    }
}
