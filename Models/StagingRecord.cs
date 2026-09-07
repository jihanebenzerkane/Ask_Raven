using System;

namespace Raven.Models
{
    public class StagingRecord
    {
        public int Id { get; set; }
        public string BatchId { get; set; } = string.Empty;              // Groups rows from a single import session
        public string EntityType { get; set; } = "Equipement";           // "Equipement" | "Marche" | "Technicien"
        public string RawDataJson { get; set; } = string.Empty;          // Full extracted row as JSON
        public double ConfidenceScore { get; set; } = 1.0;               // 0.0 – 1.0
        public string? SuggestedTechnicienMatricule { get; set; }
        public string? SuggestedTechnicienName { get; set; }
        public string Status { get; set; } = "Pending";                  // "Pending" | "Approved" | "Rejected" | "Committed"
        public string? ValidationErrors { get; set; }
        public string? UserEditsJson { get; set; }                       // Manager's corrections before commit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByUserId { get; set; }
    }
}
