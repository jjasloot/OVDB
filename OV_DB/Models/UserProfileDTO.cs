namespace OV_DB.Models
{
    public class UserProfileDTO
    {
        public string Email { get; set; }
        public string? PreferredLanguage { get; set; }
        public long? TelegramUserId { get; set; }
        public bool HasTraewelling { get; set; }
    }
}