using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using System;
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
            var visitesPlanifiees = await _context.Visites.CountAsync(v => v.Statut == "PlanifiÃ©e" && v.DatePrevue >= today);
            var visitesEnRetard = await _context.Visites.CountAsync(v => v.Statut == "En retard" || (v.Statut == "PlanifiÃ©e" && v.DatePrevue < today));
            var visitesValidees = await _context.Visites.CountAsync(v => v.Statut == "ValidÃ©e");

            var totalEquipements = await _context.Equipements.CountAsync();
            var equipementsCritiques = await _context.Equipements.CountAsync(e => e.ScoreRisque >= 70);
            var tauxConformite = totalVisites > 0 ? (double)visitesValidees / totalVisites * 100.0 : 100.0;

            var alertesVisites = await _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .Where(v => v.Statut == "En retard" || (v.Statut == "PlanifiÃ©e" && v.DatePrevue < today) || v.ScorePriorite >= 80)
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

        [HttpPost("reset-data")]
        public async Task<IActionResult> ResetData()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Vider complÃ¨tement toutes les tables opÃ©rationnelles
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
                    message = "Toutes les tables ont Ã©tÃ© rÃ©initialisÃ©es avec succÃ¨s.",
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
                    message = "DonnÃ©es industrielles dÃ©mo Raven ERP initialisÃ©es avec succÃ¨s dans SQL Server.",
                    stats
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Erreur lors de l'initialisation des donnÃ©es : {ex.Message}" });
            }
        }
    }
}


