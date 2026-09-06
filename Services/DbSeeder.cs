using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using Raven.Models;

namespace Raven.Services
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context, string? adminEmail = "admin@example.com", string? adminPassword = "ChangeMe2026!", bool force = false)
        {
            // 1. Ensure Specialites exist
            if (!context.Specialites.Any())
            {
                var defaultSpecialites = new List<Specialite>
                {
                    new() { Nom = "HVAC", Description = "Climatisation, Chauffage, Ventilation et Groupes Froid" },
                    new() { Nom = "TGBT", Description = "Tableaux GÃ©nÃ©raux Basse Tension et Armoires Ã‰lectriques" },
                    new() { Nom = "Haute Tension", Description = "Postes de Transformation et Cellules MT/HT" },
                    new() { Nom = "Groupe Ã‰lectrogÃ¨ne", Description = "Groupes Ã‰lectrogÃ¨nes et Onduleurs de secours" },
                    new() { Nom = "Compresseur", Description = "Centrales d'air comprimÃ© et pompes industrielles" },
                    new() { Nom = "Automatisme", Description = "Automates programmables, TÃ©lÃ©gestion et RÃ©gulation" },
                    new() { Nom = "Ã‰lectricitÃ© industrielle", Description = "Installations et cÃ¢blages Ã©lectriques industriels" },
                    new() { Nom = "Informatique & RÃ©seau", Description = "Serveurs, Postes, Baies de brassage et Switchs" }
                };
                context.Specialites.AddRange(defaultSpecialites);
                await context.SaveChangesAsync();
            }

            var specialites = await context.Specialites.ToListAsync();

            // 2. Ensure Admin User exists
            var targetEmail = (adminEmail ?? "admin@example.com").Trim().ToLowerInvariant();
            var existingAdmin = await context.Utilisateurs.FirstOrDefaultAsync(u => u.Email.ToLower() == targetEmail);
            var hasher = new PasswordHasher<Utilisateur>();

            if (existingAdmin == null)
            {
                var newAdmin = new Utilisateur
                {
                    Email = targetEmail,
                    Role = "Responsable",
                    DateCreation = DateTime.UtcNow
                };
                newAdmin.PasswordHash = hasher.HashPassword(newAdmin, adminPassword ?? "ChangeMe2026!");
                context.Utilisateurs.Add(newAdmin);
                await context.SaveChangesAsync();
            }
            else
            {
                // Ensure default admin has known password
                existingAdmin.PasswordHash = hasher.HashPassword(existingAdmin, adminPassword ?? "ChangeMe2026!");
                await context.SaveChangesAsync();
            }

            // Also ensure admin@raven.com exists for quick branded login
            var ravenAdmin = await context.Utilisateurs.FirstOrDefaultAsync(u => u.Email.ToLower() == "admin@raven.com");
            if (ravenAdmin == null)
            {
                var ra = new Utilisateur
                {
                    Email = "admin@raven.com",
                    Role = "Responsable",
                    DateCreation = DateTime.UtcNow
                };
                ra.PasswordHash = hasher.HashPassword(ra, adminPassword ?? "ChangeMe2026!");
                context.Utilisateurs.Add(ra);
                await context.SaveChangesAsync();
            }

            // 3. Operational Data (Clients, Sites, Marches, Techniciens, Equipements, Visites)
            if (force || !context.Equipements.Any() || !context.Visites.Any())
            {
                if (force)
                {
                    // Detach any users pointing to Techniciens to avoid FK violation
                    var usersWithTech = await context.Utilisateurs.Where(u => u.TechnicienId != null).ToListAsync();
                    foreach (var u in usersWithTech)
                    {
                        u.TechnicienId = null;
                    }
                    await context.SaveChangesAsync();

                    context.Visites.RemoveRange(context.Visites);
                    context.Equipements.RemoveRange(context.Equipements);
                    context.Marches.RemoveRange(context.Marches);
                    context.Sites.RemoveRange(context.Sites);
                    context.Clients.RemoveRange(context.Clients);
                    context.Techniciens.RemoveRange(context.Techniciens);
                    await context.SaveChangesAsync();
                }

                // Clients
                var clientOcp = new Client
                {
                    CodeClient = "CLI-OCP-001",
                    NomSociete = "OCP Group â€” Jorf Lasfar",
                    ContactPrincipal = "M. Ahmed Bennis (Dir. Technique)",
                    Email = "contact.jorf@ocpgroup.ma",
                    Telephone = "+212 5 23 34 50 00",
                    Adresse = "Zone Industrielle Jorf Lasfar, El Jadida"
                };

                var clientTangerMed = new Client
                {
                    CodeClient = "CLI-TMED-002",
                    NomSociete = "Tanger Med Port Authority",
                    ContactPrincipal = "Mme. Leila Tazi (Responsable Maintenance)",
                    Email = "maintenance@tangermed.ma",
                    Telephone = "+212 5 39 94 80 00",
                    Adresse = "Zone Franche Logistique Tanger Med, Ksar Sghir"
                };

                var clientBmce = new Client
                {
                    CodeClient = "CLI-BMCE-003",
                    NomSociete = "Bank of Africa â€” Tour CFC",
                    ContactPrincipal = "M. Mehdi Senhaji (Facility Manager)",
                    Email = "facility@bankofafrica.ma",
                    Telephone = "+212 5 22 29 88 88",
                    Adresse = "Tour Bank of Africa, Casa Finance City, Casablanca"
                };

                var clientRenault = new Client
                {
                    CodeClient = "CLI-REN-004",
                    NomSociete = "Renault Group Tanger Melloussa",
                    ContactPrincipal = "M. Rachid El Amrani (Ing. Ã‰nergie & Fluides)",
                    Email = "maintenance.tanger@renault.com",
                    Telephone = "+212 5 39 39 70 00",
                    Adresse = "Zone Franche de Melloussa, Tanger"
                };

                context.Clients.AddRange(clientOcp, clientTangerMed, clientBmce, clientRenault);
                await context.SaveChangesAsync();

                // Sites
                var siteJorf = new Site
                {
                    CodeSite = "SIT-JORF-01",
                    NomSite = "Complexe Chimique & Engrais",
                    ClientId = clientOcp.Id,
                    Adresse = "Secteur C, Jorf Lasfar",
                    Ville = "El Jadida",
                    CodePostal = "24000",
                    Latitude = 33.125,
                    Longitude = -8.625
                };

                var siteTangerLog = new Site
                {
                    CodeSite = "SIT-TMED-01",
                    NomSite = "Hub Logistique & Terminaux Conteneurs",
                    ClientId = clientTangerMed.Id,
                    Adresse = "Plateforme Portuaire TC1",
                    Ville = "Tanger",
                    CodePostal = "90000",
                    Latitude = 35.885,
                    Longitude = -5.512
                };

                var siteTourCfc = new Site
                {
                    CodeSite = "SIT-CFC-01",
                    NomSite = "Tour SiÃ¨ge 45 Ã‰tages",
                    ClientId = clientBmce.Id,
                    Adresse = "Boulevard Main Street, CFC",
                    Ville = "Casablanca",
                    CodePostal = "20250",
                    Latitude = 33.568,
                    Longitude = -7.662
                };

                var siteMelloussa = new Site
                {
                    CodeSite = "SIT-MEL-01",
                    NomSite = "Usine Montage & Emboutissage",
                    ClientId = clientRenault.Id,
                    Adresse = "RN 2, Melloussa",
                    Ville = "Tanger",
                    CodePostal = "90000",
                    Latitude = 35.712,
                    Longitude = -5.735
                };

                context.Sites.AddRange(siteJorf, siteTangerLog, siteTourCfc, siteMelloussa);
                await context.SaveChangesAsync();

                // Marches (Contracts)
                var marcheHvac = new Marche
                {
                    CodeMarche = "MAR-2026-HVAC-01",
                    Libelle = "Maintenance CVC & Groupes Froid Trane/Daikin",
                    ClientId = clientBmce.Id,
                    DateDebut = new DateTime(2026, 1, 1),
                    DateFin = new DateTime(2026, 12, 31),
                    SlaHeures = 12,
                    VisitesAnnuellesPrevues = 24,
                    VisitesRealisees = 14,
                    Statut = "Actif",
                    PvRequis = true
                };

                var marcheHt = new Marche
                {
                    CodeMarche = "MAR-2026-HT-02",
                    Libelle = "Contrat Maintenance Postes MT/HT & TGBT Schneider",
                    ClientId = clientOcp.Id,
                    DateDebut = new DateTime(2026, 1, 1),
                    DateFin = new DateTime(2027, 6, 30),
                    SlaHeures = 6,
                    VisitesAnnuellesPrevues = 18,
                    VisitesRealisees = 10,
                    Statut = "Actif",
                    PvRequis = true
                };

                var marcheFluides = new Marche
                {
                    CodeMarche = "MAR-2026-COMP-03",
                    Libelle = "Centrale Air ComprimÃ© & RÃ©seau Pompage",
                    ClientId = clientRenault.Id,
                    DateDebut = new DateTime(2025, 6, 1),
                    DateFin = new DateTime(2026, 10, 31),
                    SlaHeures = 24,
                    VisitesAnnuellesPrevues = 12,
                    VisitesRealisees = 8,
                    Statut = "En Renouvellement",
                    PvRequis = true
                };

                var marcheSecours = new Marche
                {
                    CodeMarche = "MAR-2026-GE-04",
                    Libelle = "Groupes Ã‰lectrogÃ¨nes Caterpillar & Onduleurs APC",
                    ClientId = clientTangerMed.Id,
                    DateDebut = new DateTime(2026, 3, 1),
                    DateFin = new DateTime(2027, 2, 28),
                    SlaHeures = 8,
                    VisitesAnnuellesPrevues = 12,
                    VisitesRealisees = 6,
                    Statut = "Actif",
                    PvRequis = true
                };

                context.Marches.AddRange(marcheHvac, marcheHt, marcheFluides, marcheSecours);
                await context.SaveChangesAsync();

                // Techniciens
                var specHvac = specialites.FirstOrDefault(s => s.Nom == "HVAC");
                var specTgbt = specialites.FirstOrDefault(s => s.Nom == "TGBT");
                var specHt = specialites.FirstOrDefault(s => s.Nom == "Haute Tension");
                var specGe = specialites.FirstOrDefault(s => s.Nom == "Groupe Ã‰lectrogÃ¨ne");
                var specComp = specialites.FirstOrDefault(s => s.Nom == "Compresseur");
                var specAuto = specialites.FirstOrDefault(s => s.Nom == "Automatisme");

                var techKarim = new Technicien
                {
                    Matricule = "RAV-T-001",
                    Nom = "Alami",
                    Prenom = "Karim",
                    Email = "karim.alami@ecs.ma",
                    Telephone = "+212 6 61 11 22 33",
                    DateEmbauche = new DateTime(2022, 3, 15),
                    Statut = "Actif",
                    Base = "Casablanca",
                    HeuresHebdo = 40,
                    HeuresTravaillees = 32,
                    HeuresPlanifiees = 8,
                    Disponible = true
                };
                if (specHvac != null) techKarim.Specialites.Add(specHvac);
                if (specTgbt != null) techKarim.Specialites.Add(specTgbt);

                var techYoussef = new Technicien
                {
                    Matricule = "RAV-T-002",
                    Nom = "Berrada",
                    Prenom = "Youssef",
                    Email = "youssef.berrada@ecs.ma",
                    Telephone = "+212 6 62 44 55 66",
                    DateEmbauche = new DateTime(2021, 6, 1),
                    Statut = "Actif",
                    Base = "Tanger",
                    HeuresHebdo = 40,
                    HeuresTravaillees = 28,
                    HeuresPlanifiees = 12,
                    Disponible = true
                };
                if (specHt != null) techYoussef.Specialites.Add(specHt);
                if (specTgbt != null) techYoussef.Specialites.Add(specTgbt);

                var techAmina = new Technicien
                {
                    Matricule = "RAV-T-003",
                    Nom = "Tazi",
                    Prenom = "Amina",
                    Email = "amina.tazi@ecs.ma",
                    Telephone = "+212 6 63 77 88 99",
                    DateEmbauche = new DateTime(2023, 1, 10),
                    Statut = "Actif",
                    Base = "Rabat",
                    HeuresHebdo = 40,
                    HeuresTravaillees = 36,
                    HeuresPlanifiees = 4,
                    Disponible = true
                };
                if (specAuto != null) techAmina.Specialites.Add(specAuto);
                if (specHvac != null) techAmina.Specialites.Add(specHvac);

                var techOmar = new Technicien
                {
                    Matricule = "RAV-T-004",
                    Nom = "Bennani",
                    Prenom = "Omar",
                    Email = "omar.bennani@ecs.ma",
                    Telephone = "+212 6 64 00 11 22",
                    DateEmbauche = new DateTime(2020, 9, 20),
                    Statut = "Actif",
                    Base = "Casablanca",
                    HeuresHebdo = 40,
                    HeuresTravaillees = 24,
                    HeuresPlanifiees = 16,
                    Disponible = true
                };
                if (specComp != null) techOmar.Specialites.Add(specComp);
                if (specGe != null) techOmar.Specialites.Add(specGe);

                context.Techniciens.AddRange(techKarim, techYoussef, techAmina, techOmar);
                await context.SaveChangesAsync();

                // Create user account for Karim Alami if missing
                var userKarim = await context.Utilisateurs.FirstOrDefaultAsync(u => u.Email.ToLower() == "karim.alami@ecs.ma");
                if (userKarim == null)
                {
                    var uk = new Utilisateur
                    {
                        Email = "karim.alami@ecs.ma",
                        Role = "Technicien",
                        TechnicienId = techKarim.Id,
                        DateCreation = DateTime.UtcNow
                    };
                    uk.PasswordHash = hasher.HashPassword(uk, "ChangeMe2026!");
                    context.Utilisateurs.Add(uk);
                    await context.SaveChangesAsync();
                }

                // Equipements
                var eq1 = new Equipement
                {
                    SerialNumber = "EQ-CHILLER-01",
                    Nom = "Groupe d'eau glacÃ©e Trane RTAC 250",
                    Categorie = "HVAC",
                    SiteId = siteTourCfc.Id,
                    DateInstallation = new DateTime(2021, 5, 10),
                    Criticite = 5,
                    ScoreSante = 65,
                    ScoreRisque = 78,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 7, 15),
                    ProchaineVisitePrevue = new DateTime(2026, 8, 25)
                };

                var eq2 = new Equipement
                {
                    SerialNumber = "EQ-TRANSFO-01",
                    Nom = "Transformateur HT/BT Schneider Trihal 1600 kVA",
                    Categorie = "Haute Tension",
                    SiteId = siteJorf.Id,
                    DateInstallation = new DateTime(2019, 11, 20),
                    Criticite = 5,
                    ScoreSante = 72,
                    ScoreRisque = 74,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 6, 20),
                    ProchaineVisitePrevue = new DateTime(2026, 8, 18)
                };

                var eq3 = new Equipement
                {
                    SerialNumber = "EQ-COMP-01",
                    Nom = "Compresseur Ã  vis lubrifiÃ©e Atlas Copco GA 75 VSD",
                    Categorie = "Compresseur",
                    SiteId = siteMelloussa.Id,
                    DateInstallation = new DateTime(2020, 2, 14),
                    Criticite = 4,
                    ScoreSante = 88,
                    ScoreRisque = 35,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 7, 28),
                    ProchaineVisitePrevue = new DateTime(2026, 9, 10)
                };

                var eq4 = new Equipement
                {
                    SerialNumber = "EQ-GEN-01",
                    Nom = "Groupe Ã‰lectrogÃ¨ne Caterpillar C18 700 kVA",
                    Categorie = "Groupe Ã‰lectrogÃ¨ne",
                    SiteId = siteTangerLog.Id,
                    DateInstallation = new DateTime(2022, 8, 5),
                    Criticite = 5,
                    ScoreSante = 92,
                    ScoreRisque = 22,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 8, 2),
                    ProchaineVisitePrevue = new DateTime(2026, 9, 2)
                };

                var eq5 = new Equipement
                {
                    SerialNumber = "EQ-TGBT-01",
                    Nom = "TGBT Principal Armoire Prisma P 3200A",
                    Categorie = "TGBT",
                    SiteId = siteTourCfc.Id,
                    DateInstallation = new DateTime(2021, 4, 18),
                    Criticite = 4,
                    ScoreSante = 80,
                    ScoreRisque = 45,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 7, 5),
                    ProchaineVisitePrevue = new DateTime(2026, 8, 28)
                };

                var eq6 = new Equipement
                {
                    SerialNumber = "EQ-CTA-02",
                    Nom = "Centrale de Traitement d'Air Carrier 39HQ",
                    Categorie = "HVAC",
                    SiteId = siteTourCfc.Id,
                    DateInstallation = new DateTime(2022, 1, 12),
                    Criticite = 3,
                    ScoreSante = 58,
                    ScoreRisque = 72,
                    Statut = "Maintenance Requise",
                    DerniereVisite = new DateTime(2026, 6, 10),
                    ProchaineVisitePrevue = new DateTime(2026, 8, 12)
                };

                var eq7 = new Equipement
                {
                    SerialNumber = "EQ-POMPE-01",
                    Nom = "Groupe de pompage incendie Grundfos Hydro Multi-E",
                    Categorie = "Compresseur",
                    SiteId = siteJorf.Id,
                    DateInstallation = new DateTime(2018, 9, 1),
                    Criticite = 5,
                    ScoreSante = 85,
                    ScoreRisque = 68,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 7, 22),
                    ProchaineVisitePrevue = new DateTime(2026, 8, 30)
                };

                var eq8 = new Equipement
                {
                    SerialNumber = "EQ-AUTO-01",
                    Nom = "Automate de rÃ©gulation Siemens S7-1500 & Scada WinCC",
                    Categorie = "Automatisme",
                    SiteId = siteMelloussa.Id,
                    DateInstallation = new DateTime(2023, 3, 15),
                    Criticite = 4,
                    ScoreSante = 95,
                    ScoreRisque = 18,
                    Statut = "OpÃ©rationnel",
                    DerniereVisite = new DateTime(2026, 8, 1),
                    ProchaineVisitePrevue = new DateTime(2026, 9, 15)
                };

                context.Equipements.AddRange(eq1, eq2, eq3, eq4, eq5, eq6, eq7, eq8);
                await context.SaveChangesAsync();

                // Visites
                var v1 = new Visite
                {
                    Reference = "VIS-2026-0801",
                    TypeVisite = "PrÃ©ventive",
                    Description = "Inspection trimestrielle du groupe froid : relevÃ© pressions HP/BP, vÃ©rification Ã©tanchÃ©itÃ© fluide R134a et serrage Ã©lectrique.",
                    EquipementId = eq1.Id,
                    TechnicienId = techKarim.Id,
                    MarcheId = marcheHvac.Id,
                    DatePrevue = new DateTime(2026, 8, 10, 9, 0, 0),
                    DateRealisee = new DateTime(2026, 8, 10, 11, 45, 0),
                    DureeEstimeeMinutes = 150,
                    DureeReelleMinutes = 165,
                    Statut = "ValidÃ©e",
                    ScorePriorite = 65.0,
                    RapportTechnique = "RelevÃ© des pressions conforme (HP 14.2 bar, BP 3.8 bar). Nettoyage des filtres condenseurs effectuÃ©.",
                    ActionsCorrectives = "Resserrage des bornes du compresseur 1 et appoint d'huile frigorifique 0.8L."
                };

                var v2 = new Visite
                {
                    Reference = "VIS-2026-0802",
                    TypeVisite = "Curative",
                    Description = "Remplacement des filtres d'admission et contrÃ´le thermique par thermographie infrarouge sur la CTA 02.",
                    EquipementId = eq6.Id,
                    TechnicienId = techKarim.Id,
                    MarcheId = marcheHvac.Id,
                    DatePrevue = new DateTime(2026, 8, 12, 14, 0, 0),
                    DureeEstimeeMinutes = 120,
                    Statut = "En retard",
                    ScorePriorite = 88.0,
                    RapportTechnique = "",
                    ActionsCorrectives = ""
                };

                var v3 = new Visite
                {
                    Reference = "VIS-2026-0803",
                    TypeVisite = "Audit",
                    Description = "PrÃ©lÃ¨vement d'huile diÃ©lectrique et contrÃ´le du relais Buchholz du transformateur Trihal 1600kVA.",
                    EquipementId = eq2.Id,
                    TechnicienId = techYoussef.Id,
                    MarcheId = marcheHt.Id,
                    DatePrevue = new DateTime(2026, 8, 18, 10, 0, 0),
                    DureeEstimeeMinutes = 180,
                    Statut = "En retard",
                    ScorePriorite = 92.0,
                    RapportTechnique = "",
                    ActionsCorrectives = ""
                };

                var v4 = new Visite
                {
                    Reference = "VIS-2026-0804",
                    TypeVisite = "PrÃ©ventive",
                    Description = "Essai de dÃ©marrage en charge et analyse des vibrations alternateur sur le groupe Ã©lectrogÃ¨ne C18.",
                    EquipementId = eq4.Id,
                    TechnicienId = techOmar.Id,
                    MarcheId = marcheSecours.Id,
                    DatePrevue = new DateTime(2026, 8, 26, 9, 30, 0),
                    DureeEstimeeMinutes = 120,
                    Statut = "PlanifiÃ©e",
                    ScorePriorite = 55.0
                };

                var v5 = new Visite
                {
                    Reference = "VIS-2026-0805",
                    TypeVisite = "Diagnostic",
                    Description = "ContrÃ´le thermographique infrarouge des dÃ©parts TGBT et vÃ©rification de dÃ©clenchement diffÃ©rentiel.",
                    EquipementId = eq5.Id,
                    TechnicienId = techKarim.Id,
                    MarcheId = marcheHvac.Id,
                    DatePrevue = new DateTime(2026, 8, 28, 14, 0, 0),
                    DureeEstimeeMinutes = 120,
                    Statut = "PlanifiÃ©e",
                    ScorePriorite = 60.0
                };

                var v6 = new Visite
                {
                    Reference = "VIS-2026-0806",
                    TypeVisite = "PrÃ©ventive",
                    Description = "Vidange huile synthÃ©tique et changement sÃ©parateur air/huile sur compresseur Atlas Copco.",
                    EquipementId = eq3.Id,
                    TechnicienId = techOmar.Id,
                    MarcheId = marcheFluides.Id,
                    DatePrevue = new DateTime(2026, 9, 2, 8, 30, 0),
                    DureeEstimeeMinutes = 180,
                    Statut = "PlanifiÃ©e",
                    ScorePriorite = 45.0
                };

                var v7 = new Visite
                {
                    Reference = "VIS-2026-0701",
                    TypeVisite = "PrÃ©ventive",
                    Description = "ContrÃ´le du dÃ©bit et Ã©talonnage des transmetteurs de pression sur groupe incendie.",
                    EquipementId = eq7.Id,
                    TechnicienId = techOmar.Id,
                    MarcheId = marcheFluides.Id,
                    DatePrevue = new DateTime(2026, 7, 22, 10, 0, 0),
                    DateRealisee = new DateTime(2026, 7, 22, 12, 10, 0),
                    DureeEstimeeMinutes = 120,
                    DureeReelleMinutes = 130,
                    Statut = "ValidÃ©e",
                    ScorePriorite = 40.0,
                    RapportTechnique = "Pression d'enclenchement 8.5 bar, pression d'arrÃªt 12 bar conforme au cahier des charges.",
                    ActionsCorrectives = "Remplacement du manomÃ¨tre glycÃ©rine dÃ©fectueux."
                };

                context.Visites.AddRange(v1, v2, v3, v4, v5, v6, v7);
                await context.SaveChangesAsync();
            }
        }
    }
}

