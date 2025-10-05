using System;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    public class UserAchievement
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        public User User { get; set; }
        
        public int AchievementId { get; set; }
        
        public Achievement Achievement { get; set; }
        
        public DateTime UnlockedAt { get; set; }
        
        public int CurrentProgress { get; set; }
    }
}
