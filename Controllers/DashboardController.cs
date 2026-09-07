using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Raven.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Responsable")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var today = DateTime.Today;
            var totalVisites = await _context.Visites.CountAsync();
            var visitesPlanifiees = await _context.Visites.CountAsync(v => v.Statut == "Planifiée" && v.DatePrevue >= today);
            var visitesEnRetard = await _context.Visites.CountAsync(v => v.Statut == "En retard" || (v.Statut == "Planifiée" && v.DatePrevue < today));
            var visitesValidees = await _context.Visites.CountAsync(v => v.Statut == "Validée");

            var totalEquipements = await _context.Equipements.CountAsync();
            var equipementsCritiques = await _context.Equipements.CountAsync(e => e.ScoreRisque >= 70);
            var tauxConformite = totalVisites > 0 ? (double)visitesValidees / totalVisites * 100.0 : 100.0;

            var alertesVisites = await _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .Where(v => v.Statut == "En retard" || (v.Statut == "Planifiée" && v.DatePrevue < today) || v.ScorePriorite >= 80)
                .OrderByDescending(v => v.ScorePriorite)
                .Take(5)
                .Select(v => new
                {
                    v.Id,
                    v.Reference,
                    v.TypeVisite,
                    Equipement = v.Equipement != null ? v.Equipement.Nom : "N/A",
                    Site = v.Equipement != null && v.Equipement.Site != null ? v.Equipement.Site.NomSite : "N/A",
                    v.DatePrevue,
                    v.Statut,
                    v.ScorePriorite
                })
                .ToListAsync();

            var totalMarches = await _context.Marches.CountAsync();
            var marchesActifs = await _context.Marches.CountAsync(m => m.Statut == "Actif");
            var totalClients = await _context.Clients.CountAsync();
            var totalTechniciens = await _context.Techniciens.CountAsync();

            return Ok(new
            {
                TotalVisites = totalVisites,
                VisitesPlanifiees = visitesPlanifiees,
                VisitesEnRetard = visitesEnRetard,
                VisitesValidees = visitesValidees,
                TotalEquipements = totalEquipements,
                EquipementsCritiques = equipementsCritiques,
                TauxConformite = Math.Round(tauxConformite, 1),
                TotalMarches = totalMarches,
                MarchesActifs = marchesActifs,
                TotalClients = totalClients,
                TotalTechniciens = totalTechniciens,
                AlertesUrgent = alertesVisites
            });
        }

        // ----------------------------------------------------------------
        // REAL ALERT ARCHITECTURE — GET /api/dashboard/alerts
        // Returns typed, categorized, severity-sorted alert objects
        // ----------------------------------------------------------------
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var today = DateTime.Today;
            var alerts = new List<AlertDto>();

            // 1. VISIT_OVERDUE — visites en retard (statut En retard OU planifiée passée)
            var overdueVisits = await _context.Visites
                .Include(v => v.Equipement).ThenInclude(e => e!.Site)
                .Include(v => v.Marche)
                .Where(v => v.Statut == "En retard" || (v.Statut == "Planifiée" && v.DatePrevue < today))
                .OrderByDescending(v => v.ScorePriorite)
                .Take(10)
                .ToListAsync();

            foreach (var v in overdueVisits)
            {
                var retardJours = (today - v.DatePrevue.Date).Days;
                var slaHeures = v.Marche?.SlaHeures ?? 24;
                var isSla = retardJours * 24 > slaHeures;

                alerts.Add(new AlertDto
                {
                    Id = v.Id,
                    Type = isSla ? "SLA_BREACH" : "VISIT_OVERDUE",
                    Severity = isSla ? "critical" : (retardJours > 7 ? "high" : "medium"),
                    Title = isSla
                        ? $"Dépassement SLA — {v.Reference}"
                        : $"Visite en retard — {v.Reference}",
                    Message = v.Equipement != null
                        ? $"{v.Equipement.Nom} • {v.Equipement.Site?.NomSite ?? "N/A"} • {retardJours}j de retard"
                        : $"Visite {v.Reference} — {retardJours}j de retard",
                    EntityId = v.Id,
                    EntityRef = v.Reference,
                    EntityType = "visite",
                    CreatedAt = v.DatePrevue
                });
            }

            // 2. VISIT_CRITICAL_PRIO — score de priorité ≥ 80, pas encore en retard
            var criticalPrioVisits = await _context.Visites
                .Include(v => v.Equipement).ThenInclude(e => e!.Site)
                .Where(v => v.ScorePriorite >= 80 && v.Statut == "Planifiée" && v.DatePrevue >= today)
                .OrderByDescending(v => v.ScorePriorite)
                .Take(5)
                .ToListAsync();

            foreach (var v in criticalPrioVisits)
            {
                // Avoid duplicate if already in overdue
                if (alerts.Any(a => a.EntityId == v.Id && a.EntityType == "visite")) continue;
                alerts.Add(new AlertDto
                {
                    Id = v.Id,
                    Type = "VISIT_CRITICAL_PRIO",
                    Severity = "high",
                    Title = $"Priorité critique — {v.Reference}",
                    Message = v.Equipement != null
                        ? $"{v.Equipement.Nom} • Score {v.ScorePriorite}/100 • Prévue le {v.DatePrevue:dd/MM/yyyy}"
                        : $"Visite {v.Reference} • Score {v.ScorePriorite}/100",
                    EntityId = v.Id,
                    EntityRef = v.Reference,
                    EntityType = "visite",
                    CreatedAt = v.DatePrevue
                });
            }

            // 3. EQUIPMENT_CRITICAL — équipements avec score risque ≥ 70
            var criticalEquipements = await _context.Equipements
                .Include(e => e.Site)
                .Where(e => e.ScoreRisque >= 70)
                .OrderByDescending(e => e.ScoreRisque)
                .Take(5)
                .ToListAsync();

            foreach (var eq in criticalEquipements)
            {
                alerts.Add(new AlertDto
                {
                    Id = eq.Id,
                    Type = "EQUIPMENT_CRITICAL",
                    Severity = eq.ScoreRisque >= 90 ? "critical" : "high",
                    Title = $"Équipement critique — {eq.SerialNumber}",
                    Message = $"{eq.Nom} • {eq.Site?.NomSite ?? "N/A"} • Score risque {eq.ScoreRisque}/100",
                    EntityId = eq.Id,
                    EntityRef = eq.SerialNumber,
                    EntityType = "equipement",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Sort: critical first, then high, then medium, then by date
            var severityOrder = new Dictionary<string, int> { ["critical"] = 0, ["high"] = 1, ["medium"] = 2, ["low"] = 3 };
            var sorted = alerts
                .OrderBy(a => severityOrder.GetValueOrDefault(a.Severity, 9))
                .ThenByDescending(a => a.CreatedAt)
                .Take(20)
                .ToList();

            return Ok(new
            {
                Count = sorted.Count,
                CriticalCount = sorted.Count(a => a.Severity == "critical"),
                Alerts = sorted
            });
        }


        [HttpPost("reset-data")]
        public async Task<IActionResult> ResetData()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Vider complètement toutes les tables opérationnelles
                var usersWithTech = await _context.Utilisateurs.Where(u => u.TechnicienId != null).ToListAsync();
                foreach (var u in usersWithTech)
                {
                    u.TechnicienId = null;
                }
                await _context.SaveChangesAsync();

                _context.Visites.RemoveRange(_context.Visites);
                _context.Equipements.RemoveRange(_context.Equipements);
                _context.Marches.RemoveRange(_context.Marches);
                _context.Sites.RemoveRange(_context.Sites);
                _context.Clients.RemoveRange(_context.Clients);
                _context.Techniciens.RemoveRange(_context.Techniciens);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Toutes les tables ont été réinitialisées avec succès.",
                    clients = 0,
                    sites = 0,
                    techniciens = 0,
                    equipements = 0,
                    marches = 0,
                    visites = 0
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = $"Erreur lors du vidage des tables : {ex.Message}" });
            }
        }

        [HttpPost("seed-demo-data")]
        public async Task<IActionResult> SeedDemoData()
        {
            try
            {
                await Raven.Services.DbSeeder.SeedAsync(_context, force: true);
                var stats = await GetStats();
                return Ok(new
                {
                    message = "Données industrielles démo Raven ERP initialisées avec succès dans SQL Server.",
                    stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Erreur lors de l'initialisation des données : {ex.Message}" });
            }
        }
    }
}

/// <summary>Typed alert object returned by GET /api/dashboard/alerts</summary>
public record AlertDto
{
    public int Id { get; init; }
    public string Type { get; init; } = string.Empty;      // VISIT_OVERDUE | VISIT_CRITICAL_PRIO | EQUIPMENT_CRITICAL | SLA_BREACH
    public string Severity { get; init; } = "medium";      // critical | high | medium | low
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int EntityId { get; init; }
    public string EntityRef { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty; // visite | equipement
    public DateTime CreatedAt { get; init; }
}
