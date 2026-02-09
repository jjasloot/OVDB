using System.Collections.Generic;

namespace OV_DB.Models
{
    public class UserProfileDTO
    {
        public string Email { get; set; }
        public string? PreferredLanguage { get; set; }
        public long? TelegramUserId { get; set; }
        public bool HasTraewelling { get; set; }
        public string TrainlogMaterialKey { get; set; }
        public string TrainlogRegistrationKey { get; set; }
        public string TrainlogPlatformKey { get; set; }
        public string TrainlogSeatKey { get; set; }
        public bool EnableTrainlogExport { get; set; }
        public List<TraewellingTagMappingDTO> TraewellingTagMappings { get; set; }
    }
}