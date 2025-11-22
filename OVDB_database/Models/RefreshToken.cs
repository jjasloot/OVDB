using System;
using System.ComponentModel.DataAnnotations;

namespace OVDB_database.Models
{
    /// <summary>
    /// Represents a refresh token for maintaining user sessions.
    /// Supports multiple concurrent sessions per user.
    /// </summary>
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The refresh token value - a cryptographically secure random string
        /// </summary>
        [Required]
        public string Token { get; set; }

        /// <summary>
        /// Foreign key to the User who owns this refresh token
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Navigation property to the User
        /// </summary>
        public User User { get; set; }

        /// <summary>
        /// When this refresh token was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this refresh token expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Optional device/client information for user's reference
        /// </summary>
        public string DeviceInfo { get; set; }

        /// <summary>
        /// When this token was last used to refresh an access token
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Whether this refresh token has been revoked (logged out)
        /// </summary>
        public bool IsRevoked { get; set; }

        /// <summary>
        /// When this token was revoked (if applicable)
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// Helper property to check if token is expired
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        /// <summary>
        /// Helper property to check if token is active (not expired and not revoked)
        /// </summary>
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
