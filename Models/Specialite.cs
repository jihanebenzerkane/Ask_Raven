using System.Collections.Generic;

namespace Raven.Models
{
    public class Specialite
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty; // e.g., "HVAC", "TGBT", "Haute Tension", "Groupe Ã‰lectrogÃ¨ne", "Compresseur", "Automatisme", "Ã‰lectricitÃ© industrielle"
        public string Description { get; set; } = string.Empty;

        public List<Technicien> Techniciens { get; set; } = new();
    }
}

