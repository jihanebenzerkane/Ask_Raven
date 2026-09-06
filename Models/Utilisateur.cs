using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Raven.Models
{
    public class Utilisateur
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Technicien"; // "Responsable" or "Technicien"

        public int? TechnicienId { get; set; }

        [ForeignKey("TechnicienId")]
        public Technicien? Technicien { get; set; }

        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }
}

