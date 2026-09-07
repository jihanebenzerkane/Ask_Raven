using System.Collections.Generic;

namespace Raven.Models
{
    public class Site
    {
        public int Id { get; set; }
        public string CodeSite { get; set; } = string.Empty;
        public string NomSite { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public Client? Client { get; set; }
        public string Adresse { get; set; } = string.Empty;
        public string Ville { get; set; } = string.Empty;
        public string CodePostal { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<Equipement> Equipements { get; set; } = new();
    }
}

