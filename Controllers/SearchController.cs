using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raven.Data;

namespace Raven.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly AppDbContext _db;

        public SearchController(AppDbContext db)
        {
            _db = db;
        }

        public class SearchResultItemDto
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Subtitle { get; set; } = string.Empty;
            public string EntityType { get; set; } = string.Empty;
            public string TargetNav { get; set; } = string.Empty;
            public string? Badge { get; set; }
            public string? BadgeColor { get; set; }
        }

        public class SearchCategoryDto
        {
            public string Name { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public int Count { get; set; }
            public List<SearchResultItemDto> Items { get; set; } = new();
        }

        public class QuickActionDto
        {
            public string Label { get; set; } = string.Empty;
            public string Command { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string ActionType { get; set; } = string.Empty;
        }

        public class SearchResponseDto
        {
            public string Query { get; set; } = string.Empty;
            public int TotalCount { get; set; }
            public List<SearchCategoryDto> Categories { get; set; } = new();
            public List<QuickActionDto> QuickActions { get; set; } = new();
        }

        /// <summary>
        /// Global search endpoint queried by the Ask Raven NetSuite command palette.
        /// Searches across all core SQL Server entities (Equipements, Marches, Techniciens, Visites, Clients, Sites).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GlobalSearch([FromQuery] string? q)
        {
            var query = (q ?? string.Empty).Trim();
            var response = new SearchResponseDto
            {
                Query = query,
                QuickActions = GetQuickActions(query)
            };

            if (string.IsNullOrWhiteSpace(query))
            {
                // Return default quick actions when query is empty
                return Ok(response);
            }

            var pattern = $"%{query}%";

            // 1. Equipements
            var equipements = await _db.Equipements
                .Include(e => e.Site)
                .ThenInclude(s => s!.Client)
                .Where(e => EF.Functions.Like(e.Nom, pattern)
                         || EF.Functions.Like(e.SerialNumber, pattern)
                         || EF.Functions.Like(e.Categorie, pattern)
                         || (e.Site != null && EF.Functions.Like(e.Site.NomSite, pattern))
                         || (e.Site != null && e.Site.Client != null && EF.Functions.Like(e.Site.Client.NomSociete, pattern)))
                .Take(6)
                .Select(e => new SearchResultItemDto
                {
                    Id = e.Id,
                    Title = e.Nom,
                    Subtitle = $"SN: {e.SerialNumber} • {e.Categorie} • {e.Site!.NomSite} ({e.Site.Client!.NomSociete})",
                    EntityType = "Equipement",
                    TargetNav = "equipements",
                    Badge = e.Statut,
                    BadgeColor = e.Statut == "Opérationnel" ? "success" : "warning"
                })
                .ToListAsync();

            if (equipements.Count > 0)
            {
                response.Categories.Add(new SearchCategoryDto
                {
                    Name = "Équipements",
                    Icon = "settings",
                    Count = equipements.Count,
                    Items = equipements
                });
            }

            // 2. Marchés / Contrats
            var marches = await _db.Marches
                .Include(m => m.Client)
                .Where(m => EF.Functions.Like(m.CodeMarche, pattern)
                         || EF.Functions.Like(m.Libelle, pattern)
                         || (m.Client != null && EF.Functions.Like(m.Client.NomSociete, pattern))
                         || (m.TypeContrat != null && EF.Functions.Like(m.TypeContrat, pattern)))
                .Take(6)
                .Select(m => new SearchResultItemDto
                {
                    Id = m.Id,
                    Title = $"{m.CodeMarche} - {m.Libelle}",
                    Subtitle = $"Client: {m.Client!.NomSociete} • Fin: {m.DateFin:dd/MM/yyyy} • {m.VisitesRealisees}/{m.VisitesAnnuellesPrevues} visites",
                    EntityType = "Marche",
                    TargetNav = "marches",
                    Badge = m.Statut,
                    BadgeColor = m.Statut == "Actif" ? "success" : "info"
                })
                .ToListAsync();

            if (marches.Count > 0)
            {
                response.Categories.Add(new SearchCategoryDto
                {
                    Name = "Marchés & Contrats",
                    Icon = "briefcase",
                    Count = marches.Count,
                    Items = marches
                });
            }

            // 3. Techniciens
            var techniciens = await _db.Techniciens
                .Include(t => t.Specialites)
                .Where(t => EF.Functions.Like(t.Nom, pattern)
                         || EF.Functions.Like(t.Prenom, pattern)
                         || EF.Functions.Like(t.Matricule, pattern)
                         || EF.Functions.Like(t.Base, pattern)
                         || EF.Functions.Like(t.Email, pattern)
                         || t.Specialites.Any(s => EF.Functions.Like(s.Nom, pattern)))
                .Take(6)
                .Select(t => new SearchResultItemDto
                {
                    Id = t.Id,
                    Title = $"{t.Prenom} {t.Nom} ({t.Matricule})",
                    Subtitle = $"Base: {t.Base} • {string.Join(", ", t.Specialites.Select(s => s.Nom))} • {t.Telephone}",
                    EntityType = "Technicien",
                    TargetNav = "techniciens",
                    Badge = t.Statut,
                    BadgeColor = t.Statut == "Actif" ? "success" : "secondary"
                })
                .ToListAsync();

            if (techniciens.Count > 0)
            {
                response.Categories.Add(new SearchCategoryDto
                {
                    Name = "Techniciens",
                    Icon = "users",
                    Count = techniciens.Count,
                    Items = techniciens
                });
            }

            // 4. Visites
            var visites = await _db.Visites
                .Include(v => v.Equipement)
                .Include(v => v.Technicien)
                .Where(v => EF.Functions.Like(v.Reference, pattern)
                         || EF.Functions.Like(v.TypeVisite, pattern)
                         || (v.Equipement != null && EF.Functions.Like(v.Equipement.Nom, pattern))
                         || (v.Technicien != null && (EF.Functions.Like(v.Technicien.Nom, pattern) || EF.Functions.Like(v.Technicien.Prenom, pattern))))
                .Take(6)
                .Select(v => new SearchResultItemDto
                {
                    Id = v.Id,
                    Title = $"{v.Reference} ({v.TypeVisite})",
                    Subtitle = $"Date: {v.DatePrevue:dd/MM/yyyy} • Équipement: {v.Equipement!.Nom} • Tech: {v.Technicien!.Prenom} {v.Technicien.Nom}",
                    EntityType = "Visite",
                    TargetNav = "visites",
                    Badge = v.Statut,
                    BadgeColor = v.Statut == "Validée" ? "success" : v.Statut == "Planifiée" ? "warning" : "info"
                })
                .ToListAsync();

            if (visites.Count > 0)
            {
                response.Categories.Add(new SearchCategoryDto
                {
                    Name = "Visites de Maintenance",
                    Icon = "calendar",
                    Count = visites.Count,
                    Items = visites
                });
            }

            // 5. Clients & Sites
            var clients = await _db.Clients
                .Include(c => c.Sites)
                .Where(c => EF.Functions.Like(c.NomSociete, pattern)
                         || EF.Functions.Like(c.CodeClient, pattern)
                         || EF.Functions.Like(c.Adresse, pattern)
                         || c.Sites.Any(s => EF.Functions.Like(s.Ville, pattern) || EF.Functions.Like(s.NomSite, pattern)))
                .Take(4)
                .Select(c => new SearchResultItemDto
                {
                    Id = c.Id,
                    Title = $"{c.NomSociete} ({c.CodeClient})",
                    Subtitle = $"Adresse: {c.Adresse} • {c.Sites.Count} site(s)",
                    EntityType = "Client",
                    TargetNav = "clients",
                    Badge = c.Sites.FirstOrDefault() != null ? c.Sites.First().Ville : "Maroc",
                    BadgeColor = "info"
                })
                .ToListAsync();

            if (clients.Count > 0)
            {
                response.Categories.Add(new SearchCategoryDto
                {
                    Name = "Clients",
                    Icon = "layers",
                    Count = clients.Count,
                    Items = clients
                });
            }

            response.TotalCount = response.Categories.Sum(c => c.Count);
            return Ok(response);
        }

        private List<QuickActionDto> GetQuickActions(string query)
        {
            var actions = new List<QuickActionDto>
            {
                new QuickActionDto
                {
                    Label = "Créer une nouvelle visite",
                    Command = "/visite",
                    Description = "Planifier une intervention préventive ou corrective",
                    ActionType = "NEW_VISITE"
                },
                new QuickActionDto
                {
                    Label = "Importer un fichier (HITL)",
                    Command = "/import",
                    Description = "Lancer l'import intelligent PDF, PNG, XLSX, CSV",
                    ActionType = "OPEN_IMPORT"
                },
                new QuickActionDto
                {
                    Label = "Consulter le planning techniciens",
                    Command = "/planning",
                    Description = "Vue calendrier et charge hebdomadaire",
                    ActionType = "NAV_PLANNING"
                },
                new QuickActionDto
                {
                    Label = "Exporter l'audit parc",
                    Command = "/export",
                    Description = "Génération rapport d'audit PDF & inventaire CSV",
                    ActionType = "EXPORT_AUDIT"
                }
            };

            if (string.IsNullOrWhiteSpace(query)) return actions;

            var q = query.ToLowerInvariant();
            return actions.Where(a => a.Command.Contains(q) || a.Label.ToLowerInvariant().Contains(q) || a.Description.ToLowerInvariant().Contains(q)).ToList();
        }
    }
}
