using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    public class Achievement
    {
        [Key]
        public int Id { get; set; }
        
        public string Key { get; set; }
        
        public string Name { get; set; }
        
        public string NameNL { get; set; }
        
        public string Description { get; set; }
        
        public string DescriptionNL { get; set; }
        
        public string Category { get; set; }
        
        public string Level { get; set; }
        
        public string IconName { get; set; }
        
        public int ThresholdValue { get; set; }
        
        public int DisplayOrder { get; set; }
        
        public string IconUrl { get; set; }
        
        public List<UserAchievement> UserAchievements { get; set; }
    }
}
