using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Raven.Models
{
    /// <summary>
    /// Stores one-time password (OTP) codes for MFA step-2 authentication.
    /// The actual 6-digit code is never stored — only its SHA-256 hash.
    /// </summary>
    public class OtpCode
    {
        public int Id { get; set; }

        [Required]
        public int UtilisateurId { get; set; }

        [ForeignKey("UtilisateurId")]
        public Utilisateur Utilisateur { get; set; } = null!;

        /// <summary>SHA-256 hex hash of the 6-digit code.</summary>
        [Required]
        [MaxLength(64)]
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>
        /// Opaque short-lived token returned to the browser after step-1.
        /// Used to correlate the OTP submission with the correct user session
        /// without exposing the user ID directly.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string TempToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Set when the code has been consumed successfully.</summary>
        public DateTime? UsedAt { get; set; }
    }
}

