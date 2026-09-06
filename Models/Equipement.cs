using System;
using System.Collections.Generic;

namespace Raven.Models
{
    public class Equipement
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty; // e.g. "EQ-HVAC-001"
        public string Nom { get; set; } = string.Empty;
        public string Categorie { get; set; } = string.Empty; // HVAC, Groupe Ã‰lectrogÃ¨ne, Transformateur, Compresseur, TGBT, Automatisme, etc.
        public int SiteId { get; set; }
        public Site? Site { get; set; }
        public DateTime DateInstallation { get; set; } = DateTime.UtcNow;
        public int Criticite { get; set; } = 3; // 1 (Faible) Ã  5 (Critique)
        public int ScoreSante { get; set; } = 85; // 0-100%
        public int ScoreRisque { get; set; } = 15; // 0-100 (calculÃ© dynamiquement)
        public string Statut { get; set; } = "OpÃ©rationnel"; // OpÃ©rationnel, En Panne, Maintenance Requise, En RÃ©vision, Inactif
        public DateTime DerniereVisite { get; set; } = DateTime.UtcNow;
        public DateTime ProchaineVisitePrevue { get; set; } = DateTime.UtcNow.AddMonths(3);

        public List<Visite> Visites { get; set; } = new();
    }
}

