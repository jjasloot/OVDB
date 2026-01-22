using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OVDB_database.Enums;

namespace OVDB_database.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; }
        
        [Required]
        [MaxLength(512)]
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime LastLogin { get; set; }
        
        /// <summary>
        /// DEPRECATED: Legacy refresh token field. Use RefreshTokens collection instead.
        /// Kept for backwards compatibility and database schema stability.
        /// </summary>
        public string RefreshToken { get; set; }
        
        public Guid Guid { get; set; }
        public List<Map> Maps { get; set; }
        public List<RouteType> RouteTypes { get; set; }
        public long? TelegramUserId { get; set; }
        public PreferredLanguage? PreferredLanguage { get; set; }
        
        // Träwelling OAuth2 integration fields
        public string TrawellingAccessToken { get; set; }
        public string TrawellingRefreshToken { get; set; }
        public DateTime? TrawellingTokenExpiresAt { get; set; }
        public string TrawellingUsername { get; set; }
        
        /// <summary>
        /// Collection of active refresh tokens for this user (supports multiple sessions)
        /// </summary>
        public List<RefreshToken> RefreshTokens { get; set; }
    }
}
