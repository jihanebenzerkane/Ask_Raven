using System;
using System.Collections.Generic;

namespace Raven.Models
{
    public class Technicien
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = string.Empty; // e.g. "ECS-T-001"
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;

        public DateTime DateEmbauche { get; set; } = DateTime.UtcNow;
        public string Statut { get; set; } = "Actif"; // Actif, En congé, Indisponible, Inactif
        public string Base { get; set; } = "Casablanca"; // Agence ECS principale (Casablanca, Rabat, Tanger, etc.)

        public int HeuresHebdo { get; set; } = 40; // Contrat hebdomadaire en heures
        public int HeuresTravaillees { get; set; } = 0; // Total ou cumul heures réalisées
        public int HeuresPlanifiees { get; set; } = 0; // Heures actuellement planifiées

        public bool Disponible { get; set; } = true;

        public List<Specialite> Specialites { get; set; } = new();
        public List<Visite> Visites { get; set; } = new();
    }
}

