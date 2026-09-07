using System.Collections.Generic;

namespace Raven.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string CodeClient { get; set; } = string.Empty;
        public string NomSociete { get; set; } = string.Empty;
        public string ContactPrincipal { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string Adresse { get; set; } = string.Empty;
        public List<Site> Sites { get; set; } = new();
        public List<Marche> Marches { get; set; } = new();
    }
}

