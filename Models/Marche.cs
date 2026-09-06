using System;
using System.Collections.Generic;

namespace Raven.Models
{
    public class Marche
    {
        public int Id { get; set; }
        public string CodeMarche { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public Client? Client { get; set; }
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public int SlaHeures { get; set; } = 24;
        public int VisitesAnnuellesPrevues { get; set; } = 12;
        public int VisitesRealisees { get; set; } = 0;
        public string Statut { get; set; } = "Actif"; // Actif, En Renouvellement, ExpirÃ©

        public List<Visite> Visites { get; set; } = new();

        // â”€â”€ Fields added for Excel import â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Type de contrat libre (e.g. "Maintenance") â€” source: colonne "Type de contrat"</summary>
        public string? TypeContrat { get; set; }

        /// <summary>PV requis â€” source: colonne "PV O/N" (O = true)</summary>
        public bool PvRequis { get; set; } = false;

        /// <summary>Facture requise â€” source: colonne "F O/N" (O = true)</summary>
        public bool FactureRequise { get; set; } = false;

        /// <summary>Nombre de postes de travail fixes</summary>
        public int NombrePC { get; set; } = 0;

        /// <summary>Nombre de PC portables</summary>
        public int NombrePCPortable { get; set; } = 0;

        /// <summary>Nombre d'imprimantes</summary>
        public int NombreImprimante { get; set; } = 0;

        /// <summary>Nombre de serveurs</summary>
        public int NombreServeur { get; set; } = 0;

        /// <summary>Ã‰quipements divers non structurÃ©s â€” source: colonne "Autres", stockÃ© tel quel</summary>
        public string? EquipementsDivers { get; set; }

        /// <summary>Commentaire brut depuis Excel â€” NE PAS utiliser comme Statut fonctionnel</summary>
        public string? CommentaireImport { get; set; }
    }
}


