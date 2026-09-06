using System;
using System.Linq;
using Raven.Models;

namespace Raven.Services
{
    public class TechnicienScoreDetail
    {
        public int ScoreTotal { get; set; }
        public int ScoreCompetence { get; set; }
        public int ScoreDisponibilite { get; set; }
        public int ScoreCharge { get; set; }
        public int ScoreProximite { get; set; }
        public string DetailsCompetence { get; set; } = string.Empty;
        public string DetailsDisponibilite { get; set; } = string.Empty;
        public string DetailsCharge { get; set; } = string.Empty;
        public string DetailsProximite { get; set; } = string.Empty;
    }

    public class ScoringService
    {
        /// <summary>
        /// Calcul du score de risque d'un Ã©quipement (0 Ã  100).
        /// BasÃ© sur l'Ã¢ge de l'Ã©quipement, la criticitÃ©, et le temps Ã©coulÃ© depuis la derniÃ¨re visite.
        /// </summary>
        public int CalculerScoreRisque(Equipement equipement)
        {
            if (equipement == null) return 0;

            double scoreAge = Math.Min(40, (DateTime.Now - equipement.DateInstallation).TotalDays / 365.25 * 4);
            double scoreCriticite = equipement.Criticite * 8.0; // 8 Ã  40
            double joursSansVisite = (DateTime.Now - equipement.DerniereVisite).TotalDays;
            double scoreVusterite = Math.Min(20, Math.Max(0, joursSansVisite / 15.0));

            int total = (int)Math.Round(scoreAge + scoreCriticite + scoreVusterite);
            return Math.Clamp(total, 5, 98);
        }

        /// <summary>
        /// Calcul de la prioritÃ© d'une visite de maintenance.
        /// </summary>
        public double CalculerPrioriteVisite(Equipement equipement, string typeVisite, DateTime datePrevue)
        {
            double baseScore = CalculerScoreRisque(equipement);

            if (typeVisite == "Curative")
            {
                baseScore += 35.0;
            }
            else if (typeVisite == "Audit")
            {
                baseScore += 15.0;
            }
            else if (typeVisite == "Diagnostic")
            {
                baseScore += 20.0;
            }

            // Majoration si la date prÃ©vue est dÃ©passÃ©e
            if (datePrevue < DateTime.Now.Date)
            {
                double retardJours = (DateTime.Now.Date - datePrevue.Date).TotalDays;
                baseScore += Math.Min(30, retardJours * 5.0);
            }

            return Math.Round(Math.Clamp(baseScore, 10.0, 100.0), 1);
        }

        /// <summary>
        /// Moteur de scoring dynamique (0-100) pour l'affectation d'un technicien ECS Ã  une intervention :
        /// - 40% CompÃ©tence (adÃ©quation spÃ©cialitÃ© / catÃ©gorie Ã©quipement)
        /// - 30% DisponibilitÃ© (statut actif/disponible + capacitÃ© horaire restante)
        /// - 20% Charge de travail (Ã©quilibrage des heures planifiÃ©es)
        /// - 10% ProximitÃ© gÃ©ographique (base agence ECS vs site client)
        /// </summary>
        public TechnicienScoreDetail EvaluerTechnicien(Technicien technicien, Equipement equipement, DateTime datePrevue, int dureeEstimeeMinutes = 120, int? heuresPlanifiees = null)
        {
            var res = new TechnicienScoreDetail();
            if (technicien == null || equipement == null) return res;

            int planifiees = heuresPlanifiees ?? technicien.HeuresPlanifiees;

            // 1. CompÃ©tence (40%)
            var cat = equipement.Categorie ?? string.Empty;
            bool matchExact = technicien.Specialites.Any(s => string.Equals(s.Nom, cat, StringComparison.OrdinalIgnoreCase));
            bool matchPartiel = !matchExact && technicien.Specialites.Any(s =>
                s.Nom.Contains(cat, StringComparison.OrdinalIgnoreCase) ||
                cat.Contains(s.Nom, StringComparison.OrdinalIgnoreCase));

            if (matchExact)
            {
                res.ScoreCompetence = 40;
                res.DetailsCompetence = $"SpÃ©cialitÃ© certifiÃ©e {cat}";
            }
            else if (matchPartiel)
            {
                res.ScoreCompetence = 25;
                res.DetailsCompetence = $"CompÃ©tence connexe pour {cat}";
            }
            else
            {
                res.ScoreCompetence = 5;
                res.DetailsCompetence = "Sans spÃ©cialitÃ© directe";
            }

            // 2. DisponibilitÃ© (30%)
            if (!technicien.Disponible || technicien.Statut != "Actif")
            {
                res.ScoreDisponibilite = 0;
                res.DetailsDisponibilite = $"Indisponible ({technicien.Statut})";
            }
            else
            {
                int heuresHebdo = technicien.HeuresHebdo > 0 ? technicien.HeuresHebdo : 40;
                int heuresRestantes = Math.Max(0, heuresHebdo - planifiees);
                double dureeHeures = Math.Ceiling(dureeEstimeeMinutes / 60.0);

                if (heuresRestantes >= dureeHeures)
                {
                    res.ScoreDisponibilite = 30;
                    res.DetailsDisponibilite = $"Disponible ({heuresRestantes}h restantes)";
                }
                else if (heuresRestantes > 0)
                {
                    res.ScoreDisponibilite = (int)Math.Round(30.0 * heuresRestantes / Math.Max(1.0, dureeHeures));
                    res.DetailsDisponibilite = $"CapacitÃ© limitÃ©e ({heuresRestantes}h restantes)";
                }
                else
                {
                    res.ScoreDisponibilite = 5;
                    res.DetailsDisponibilite = "Semaine complÃ¨te (surcharge)";
                }
            }

            // 3. Charge de travail (20%)
            int capacite = technicien.HeuresHebdo > 0 ? technicien.HeuresHebdo : 40;
            double ratioCharge = (double)planifiees / capacite;
            res.ScoreCharge = (int)Math.Round(Math.Max(0, (1.0 - Math.Min(1.0, ratioCharge)) * 20.0));
            int pctCharge = (int)Math.Round(ratioCharge * 100);
            res.DetailsCharge = $"Charge {pctCharge}% ({planifiees}h/{capacite}h)";

            // 4. ProximitÃ© (10%)
            var villeSite = equipement.Site?.Ville ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(technicien.Base) && !string.IsNullOrWhiteSpace(villeSite) &&
                string.Equals(technicien.Base.Trim(), villeSite.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                res.ScoreProximite = 10;
                res.DetailsProximite = $"MÃªme ville ({technicien.Base})";
            }
            else if (!string.IsNullOrWhiteSpace(technicien.Base))
            {
                res.ScoreProximite = 5;
                res.DetailsProximite = $"Base {technicien.Base} â†’ {villeSite}";
            }
            else
            {
                res.ScoreProximite = 4;
                res.DetailsProximite = "Base non renseignÃ©e";
            }

            res.ScoreTotal = Math.Clamp(res.ScoreCompetence + res.ScoreDisponibilite + res.ScoreCharge + res.ScoreProximite, 0, 100);
            return res;
        }

        /// <summary>
        /// RÃ©tro-compatibilitÃ© : retourne le score total d'affectation
        /// </summary>
        public int CalculerScoreAffectationTechnicien(Technicien technicien, Visite visite, Equipement equipement)
        {
            var date = visite?.DatePrevue ?? DateTime.Now;
            var duree = visite?.DureeEstimeeMinutes ?? 120;
            return EvaluerTechnicien(technicien, equipement, date, duree).ScoreTotal;
        }
    }
}

