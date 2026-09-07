using System;

namespace Raven.Models
{
    public class Visite
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty; // e.g. "VIS-2026-0001"
        public string TypeVisite { get; set; } = "Préventive"; // Préventive, Curative, Audit, Diagnostic, Autre
        public string? TypeVisiteAutre { get; set; } // Obligatoire si TypeVisite == "Autre"
        public string? Description { get; set; }

        public int EquipementId { get; set; }
        public Equipement? Equipement { get; set; }

        public int? TechnicienId { get; set; }
        public Technicien? Technicien { get; set; }

        public int? MarcheId { get; set; }
        public Marche? Marche { get; set; }

        public DateTime DatePrevue { get; set; }
        public DateTime? DateRealisee { get; set; }
        public int DureeEstimeeMinutes { get; set; } = 120;
        public int? DureeReelleMinutes { get; set; }

        public string Statut { get; set; } = "Planifiée"; // Planifiée, En cours, Validée, En retard, Annulée
        public double ScorePriorite { get; set; } = 50.0;
        public string RapportTechnique { get; set; } = string.Empty;
        public string ActionsCorrectives { get; set; } = string.Empty;
    }
}

