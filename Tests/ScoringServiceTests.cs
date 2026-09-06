using System;
using System.Collections.Generic;
using Raven.Models;
using Raven.Services;
using Xunit;

namespace Raven.Tests
{
    public class ScoringServiceTests
    {
        private readonly ScoringService _scoringService = new();

        #region 1. Tests CompÃ©tence (40%)

        [Fact]
        public void EvaluerTechnicien_SpecialiteExacte_Attribue40PointsCompetence()
        {
            // Arrange
            var technicien = new Technicien
            {
                Nom = "Alami",
                Prenom = "Karim",
                Statut = "Actif",
                Disponible = true,
                Base = "Casablanca",
                HeuresHebdo = 40,
                HeuresPlanifiees = 0,
                Specialites = new List<Specialite>
                {
                    new() { Id = 1, Nom = "HVAC" }
                }
            };

            var equipement = new Equipement
            {
                Nom = "Climatiseur Central",
                Categorie = "HVAC",
                Site = new Site { Ville = "Casablanca" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(40, evaluation.ScoreCompetence);
            Assert.Contains("SpÃ©cialitÃ© certifiÃ©e HVAC", evaluation.DetailsCompetence);
        }

        [Fact]
        public void EvaluerTechnicien_SpecialiteConnexe_Attribue25PointsCompetence()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = true,
                Specialites = new List<Specialite>
                {
                    new() { Id = 2, Nom = "Ã‰lectricitÃ© industrielle" }
                }
            };

            var equipement = new Equipement
            {
                Categorie = "Ã‰lectricitÃ©",
                Site = new Site { Ville = "Rabat" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(25, evaluation.ScoreCompetence);
            Assert.Contains("CompÃ©tence connexe", evaluation.DetailsCompetence);
        }

        [Fact]
        public void EvaluerTechnicien_SansSpecialiteDirecte_Attribue5PointsCompetence()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = true,
                Specialites = new List<Specialite>
                {
                    new() { Id = 3, Nom = "Compresseur" }
                }
            };

            var equipement = new Equipement
            {
                Categorie = "Haute Tension",
                Site = new Site { Ville = "Tanger" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(5, evaluation.ScoreCompetence);
            Assert.Equal("Sans spÃ©cialitÃ© directe", evaluation.DetailsCompetence);
        }

        #endregion

        #region 2. Tests DisponibilitÃ© (30%)

        [Fact]
        public void EvaluerTechnicien_TechnicienIndisponible_Attribue0PointsDisponibilite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = false // MarquÃ© indisponible
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(0, evaluation.ScoreDisponibilite);
            Assert.Contains("Indisponible", evaluation.DetailsDisponibilite);
        }

        [Fact]
        public void EvaluerTechnicien_TechnicienNonActif_Attribue0PointsDisponibilite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "En congÃ©",
                Disponible = true
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(0, evaluation.ScoreDisponibilite);
            Assert.Contains("Indisponible (En congÃ©)", evaluation.DetailsDisponibilite);
        }

        [Fact]
        public void EvaluerTechnicien_TechnicienDisponibleAvecHeuresSuffisantes_Attribue30PointsDisponibilite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = true,
                HeuresHebdo = 40,
                HeuresPlanifiees = 10 // 30h restantes, visite = 2h
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now, dureeEstimeeMinutes: 120);

            // Assert
            Assert.Equal(30, evaluation.ScoreDisponibilite);
            Assert.Contains("30h restantes", evaluation.DetailsDisponibilite);
        }

        [Fact]
        public void EvaluerTechnicien_TechnicienEnSurcharge_Attribue5PointsDisponibilite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = true,
                HeuresHebdo = 40,
                HeuresPlanifiees = 40 // 0h restante
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now, dureeEstimeeMinutes: 120);

            // Assert
            Assert.Equal(5, evaluation.ScoreDisponibilite);
            Assert.Contains("surcharge", evaluation.DetailsDisponibilite);
        }

        #endregion

        #region 3. Tests Charge de travail (20%)

        [Fact]
        public void EvaluerTechnicien_FaibleCharge_AttribueScoreChargeEleve()
        {
            // Arrange
            var technicien = new Technicien
            {
                HeuresHebdo = 40,
                HeuresPlanifiees = 0 // 0% de charge
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(20, evaluation.ScoreCharge); // 100% du poids charge
            Assert.Contains("Charge 0%", evaluation.DetailsCharge);
        }

        [Fact]
        public void EvaluerTechnicien_ChargeComplete_Attribue0PointCharge()
        {
            // Arrange
            var technicien = new Technicien
            {
                HeuresHebdo = 40,
                HeuresPlanifiees = 40 // 100% de charge
            };

            var equipement = new Equipement { Categorie = "HVAC" };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(0, evaluation.ScoreCharge);
            Assert.Contains("Charge 100%", evaluation.DetailsCharge);
        }

        #endregion

        #region 4. Tests ProximitÃ© (10%)

        [Fact]
        public void EvaluerTechnicien_MemeVille_Attribue10PointsProximite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Base = "Casablanca"
            };

            var equipement = new Equipement
            {
                Site = new Site { Ville = "Casablanca" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(10, evaluation.ScoreProximite);
            Assert.Contains("MÃªme ville", evaluation.DetailsProximite);
        }

        [Fact]
        public void EvaluerTechnicien_VilleDifferente_Attribue5PointsProximite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Base = "Rabat"
            };

            var equipement = new Equipement
            {
                Site = new Site { Ville = "Casablanca" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(5, evaluation.ScoreProximite);
            Assert.Contains("Base Rabat â†’ Casablanca", evaluation.DetailsProximite);
        }

        [Fact]
        public void EvaluerTechnicien_BaseNonRenseignee_Attribue4PointsProximite()
        {
            // Arrange
            var technicien = new Technicien
            {
                Base = ""
            };

            var equipement = new Equipement
            {
                Site = new Site { Ville = "Casablanca" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(4, evaluation.ScoreProximite);
            Assert.Equal("Base non renseignÃ©e", evaluation.DetailsProximite);
        }

        #endregion

        #region 5. Tests Score Total & Cas limites

        [Fact]
        public void EvaluerTechnicien_ScoreTotal_EstSommeDesComposantesEtClampe()
        {
            // Arrange : Profil idÃ©al (40 comp + 30 dispo + 20 charge + 10 prox = 100)
            var technicien = new Technicien
            {
                Statut = "Actif",
                Disponible = true,
                Base = "Casablanca",
                HeuresHebdo = 40,
                HeuresPlanifiees = 0,
                Specialites = new List<Specialite> { new() { Nom = "TGBT" } }
            };

            var equipement = new Equipement
            {
                Categorie = "TGBT",
                Site = new Site { Ville = "Casablanca" }
            };

            // Act
            var evaluation = _scoringService.EvaluerTechnicien(technicien, equipement, DateTime.Now);

            // Assert
            Assert.Equal(100, evaluation.ScoreTotal);
            Assert.Equal(40, evaluation.ScoreCompetence);
            Assert.Equal(30, evaluation.ScoreDisponibilite);
            Assert.Equal(20, evaluation.ScoreCharge);
            Assert.Equal(10, evaluation.ScoreProximite);
        }

        [Fact]
        public void EvaluerTechnicien_ParametresNull_RetourneScoreVide()
        {
            // Act
            var evalNullTech = _scoringService.EvaluerTechnicien(null!, new Equipement(), DateTime.Now);
            var evalNullEq = _scoringService.EvaluerTechnicien(new Technicien(), null!, DateTime.Now);

            // Assert
            Assert.Equal(0, evalNullTech.ScoreTotal);
            Assert.Equal(0, evalNullEq.ScoreTotal);
        }

        #endregion

        #region 6. Tests Score Risque & PrioritÃ© Visite

        [Fact]
        public void CalculerScoreRisque_EquipementNull_RetourneZero()
        {
            var score = _scoringService.CalculerScoreRisque(null!);
            Assert.Equal(0, score);
        }

        [Fact]
        public void CalculerScoreRisque_EquipementCritiqueEtAncien_RetourneScoreEleve()
        {
            var equipement = new Equipement
            {
                DateInstallation = DateTime.Now.AddYears(-15),
                Criticite = 5, // Max
                DerniereVisite = DateTime.Now.AddMonths(-6)
            };

            var score = _scoringService.CalculerScoreRisque(equipement);

            // 40 (age max) + 40 (crit max) + 12 (6 mois sans visite) = ~92
            Assert.True(score >= 80, $"Le score de risque ({score}) devrait Ãªtre >= 80 pour un Ã©quipement ancien et critique.");
        }

        [Fact]
        public void CalculerPrioriteVisite_Curative_AjouteMajoration35Points()
        {
            var equipement = new Equipement
            {
                DateInstallation = DateTime.Now.AddYears(-1),
                Criticite = 2,
                DerniereVisite = DateTime.Now
            };

            var scorePreventive = _scoringService.CalculerPrioriteVisite(equipement, "PrÃ©ventive", DateTime.Now.AddDays(7));
            var scoreCurative = _scoringService.CalculerPrioriteVisite(equipement, "Curative", DateTime.Now.AddDays(7));

            Assert.Equal(35.0, scoreCurative - scorePreventive, precision: 1);
        }

        [Fact]
        public void CalculerPrioriteVisite_DatePrevueDepassee_AjouteMajorationRetard()
        {
            var equipement = new Equipement
            {
                DateInstallation = DateTime.Now,
                Criticite = 2,
                DerniereVisite = DateTime.Now
            };

            var dateDansLePasse = DateTime.Now.Date.AddDays(-4); // 4 jours de retard -> 4 * 5 = +20 pts
            var dateFuture = DateTime.Now.Date.AddDays(4);

            var scoreRetard = _scoringService.CalculerPrioriteVisite(equipement, "PrÃ©ventive", dateDansLePasse);
            var scoreNormal = _scoringService.CalculerPrioriteVisite(equipement, "PrÃ©ventive", dateFuture);

            Assert.True(scoreRetard > scoreNormal, "Une visite en retard doit avoir une prioritÃ© plus Ã©levÃ©e qu'une visite future.");
        }

        #endregion
    }
}

