using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Raven.Models
{
    public class PasswordResetToken
    {
        public int Id { get; set; }

        [Required]
        public int UtilisateurId { get; set; }

        [ForeignKey("UtilisateurId")]
        public Utilisateur? Utilisateur { get; set; }

        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

