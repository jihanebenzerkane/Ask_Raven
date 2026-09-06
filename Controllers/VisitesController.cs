using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Raven.Data;
using Raven.Models;
using Raven.Services;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Raven.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Responsable,Technicien")]
    public class VisitesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ScoringService _scoringService;
        private readonly ILogger<VisitesController> _logger;

        public VisitesController(AppDbContext context, ScoringService scoringService, ILogger<VisitesController> logger)
        {
            _context = context;
            _scoringService = scoringService;
            _logger = logger;
        }

        private int? GetCurrentTechnicienId()
        {
            var techIdClaim = User.FindFirst("technicienId")?.Value;
            if (int.TryParse(techIdClaim, out var techId) && techId > 0)
            {
                return techId;
            }
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetVisites(
            [FromQuery] string? statut, 
            [FromQuery] int? technicienId, 
            [FromQuery] string? technicien,
            [FromQuery] string? typeVisite,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? page,
            [FromQuery] int? pageSize)
        {
            var isTechnicien = User.IsInRole("Technicien");
            var currentTechId = GetCurrentTechnicienId();

            var query = _context.Visites
                .AsNoTracking()
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .ThenInclude(s => s!.Client)
                .Include(v => v.Technicien)
                .Include(v => v.Marche)
                .AsQueryable();

            // SÃ©curitÃ© RBAC : un technicien ne peut STRICTEMENT voir que ses propres visites
            if (isTechnicien)
            {
                if (!currentTechId.HasValue)
                {
                    return Ok(new List<object>());
                }
                query = query.Where(v => v.TechnicienId == currentTechId.Value);
            }
            else
            {
                // Responsable / Admin : filtres optionnels
                if (technicienId.HasValue)
                {
                    query = query.Where(v => v.TechnicienId == technicienId.Value);
                }
                if (!string.IsNullOrWhiteSpace(technicien))
                {
                    query = query.Where(v => v.Technicien != null &&
                        (v.Technicien.Nom.Contains(technicien) || v.Technicien.Prenom.Contains(technicien)));
                }
            }

            var today = DateTime.Today;

            if (!string.IsNullOrWhiteSpace(statut))
            {
                if (string.Equals(statut, "En retard", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(v => v.Statut == "En retard" || (v.Statut == "PlanifiÃ©e" && v.DatePrevue < today));
                }
                else if (string.Equals(statut, "PlanifiÃ©e", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(v => v.Statut == "PlanifiÃ©e" && v.DatePrevue >= today);
                }
                else
                {
                    query = query.Where(v => v.Statut == statut);
                }
            }

            if (!string.IsNullOrWhiteSpace(typeVisite))
            {
                query = query.Where(v => v.TypeVisite == typeVisite);
            }

            if (dateDebut.HasValue)
            {
                query = query.Where(v => v.DatePrevue >= dateDebut.Value);
            }

            if (dateFin.HasValue)
            {
                query = query.Where(v => v.DatePrevue <= dateFin.Value);
            }

            // Pagination optionnelle si demandÃ©e
            if (page.HasValue && page.Value > 0 && pageSize.HasValue && pageSize.Value > 0)
            {
                query = query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value);
            }

            var result = await query
                .OrderByDescending(v => v.ScorePriorite)
                .Select(v => new
                {
                    v.Id,
                    v.Reference,
                    v.TypeVisite,
                    v.TypeVisiteAutre,
                    v.Description,
                    TypeVisiteAffiche = v.TypeVisite == "Autre" && !string.IsNullOrWhiteSpace(v.TypeVisiteAutre) ? $"Autre ({v.TypeVisiteAutre})" : v.TypeVisite,
                    v.EquipementId,
                    EquipementNom = v.Equipement != null ? v.Equipement.Nom : "Inconnu",
                    EquipementSerial = v.Equipement != null ? v.Equipement.SerialNumber : "",
                    EquipementCategorie = v.Equipement != null ? v.Equipement.Categorie : "",
                    SiteNom = v.Equipement != null && v.Equipement.Site != null ? v.Equipement.Site.NomSite : "",
                    ClientNom = v.Equipement != null && v.Equipement.Site != null && v.Equipement.Site.Client != null ? v.Equipement.Site.Client.NomSociete : "",
                    v.TechnicienId,
                    TechnicienNom = v.Technicien != null ? $"{v.Technicien.Prenom} {v.Technicien.Nom}".Trim() : "Non assignÃ©",
                    TechnicienMatricule = v.Technicien != null ? v.Technicien.Matricule : "",
                    v.MarcheId,
                    MarcheCode = v.Marche != null ? v.Marche.CodeMarche : "",
                    v.DatePrevue,
                    v.DateRealisee,
                    v.DureeEstimeeMinutes,
                    v.DureeReelleMinutes,
                    Statut = (v.Statut == "PlanifiÃ©e" && v.DatePrevue < today) ? "En retard" : v.Statut,
                    v.ScorePriorite,
                    v.RapportTechnique,
                    v.ActionsCorrectives
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("mes-visites")]
        public async Task<IActionResult> GetMesVisites([FromQuery] int? technicienId)
        {
            var isTechnicien = User.IsInRole("Technicien");
            var currentTechId = GetCurrentTechnicienId();
            var today = DateTime.Today;

            int? targetTechId = isTechnicien ? currentTechId : (technicienId ?? currentTechId);

            var query = _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .ThenInclude(s => s!.Client)
                .Include(v => v.Technicien)
                .Include(v => v.Marche)
                .AsQueryable();

            if (targetTechId.HasValue)
            {
                query = query.Where(v => v.TechnicienId == targetTechId.Value);
            }
            else if (isTechnicien)
            {
                return Ok(new List<object>());
            }

            var result = await query
                .OrderBy(v => v.DatePrevue)
                .Select(v => new
                {
                    v.Id,
                    v.Reference,
                    v.TypeVisite,
                    v.TypeVisiteAutre,
                    v.Description,
                    TypeVisiteAffiche = v.TypeVisite == "Autre" && !string.IsNullOrWhiteSpace(v.TypeVisiteAutre) ? $"Autre ({v.TypeVisiteAutre})" : v.TypeVisite,
                    v.EquipementId,
                    EquipementNom = v.Equipement != null ? v.Equipement.Nom : "Inconnu",
                    EquipementSerial = v.Equipement != null ? v.Equipement.SerialNumber : "",
                    SiteNom = v.Equipement != null && v.Equipement.Site != null ? v.Equipement.Site.NomSite : "",
                    ClientNom = v.Equipement != null && v.Equipement.Site != null && v.Equipement.Site.Client != null ? v.Equipement.Site.Client.NomSociete : "",
                    v.TechnicienId,
                    TechnicienNom = v.Technicien != null ? $"{v.Technicien.Prenom} {v.Technicien.Nom}".Trim() : "Non assignÃ©",
                    v.MarcheId,
                    MarcheCode = v.Marche != null ? v.Marche.CodeMarche : "",
                    v.DatePrevue,
                    v.DateRealisee,
                    v.DureeEstimeeMinutes,
                    v.DureeReelleMinutes,
                    Statut = (v.Statut == "PlanifiÃ©e" && v.DatePrevue < today) ? "En retard" : v.Statut,
                    v.ScorePriorite,
                    v.RapportTechnique,
                    v.ActionsCorrectives
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVisiteById(int id)
        {
            var visite = await _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .ThenInclude(s => s!.Client)
                .Include(v => v.Technicien)
                .Include(v => v.Marche)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (visite == null) return NotFound(new { message = "Visite non trouvÃ©e." });

            // SÃ©curitÃ© RBAC : un technicien ne peut pas consulter la visite d'un collÃ¨gue
            if (User.IsInRole("Technicien"))
            {
                var currentTechId = GetCurrentTechnicienId();
                if (visite.TechnicienId != currentTechId)
                {
                    return Forbid();
                }
            }

            return Ok(visite);
        }

        [HttpPost]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> CreateVisite([FromBody] Visite model)
        {
            if (model == null) return BadRequest(new { message = "DonnÃ©es invalides." });

            // Validation rÃ¨gle mÃ©tier : si TypeVisite == "Autre", TypeVisiteAutre est obligatoire
            if (string.Equals(model.TypeVisite, "Autre", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.TypeVisiteAutre))
                {
                    return BadRequest(new { message = "Le champ 'PrÃ©cisez le type de visite' est obligatoire lorsque le type 'Autre' est sÃ©lectionnÃ©." });
                }
            }

            var equipement = await _context.Equipements.Include(e => e.Site).FirstOrDefaultAsync(e => e.Id == model.EquipementId);
            if (equipement != null)
            {
                model.ScorePriorite = _scoringService.CalculerPrioriteVisite(equipement, model.TypeVisite, model.DatePrevue);
            }

            if (string.IsNullOrWhiteSpace(model.Reference))
            {
                var year = DateTime.Now.Year;
                var prefix = $"VIS-{year}-";
                
                var existingRefs = await _context.Visites
                    .Where(v => v.Reference.StartsWith(prefix))
                    .Select(v => v.Reference)
                    .ToListAsync();

                int maxSeq = 0;
                foreach (var r in existingRefs)
                {
                    if (r.Length > prefix.Length && int.TryParse(r.Substring(prefix.Length), out int seq) && seq > maxSeq)
                    {
                        maxSeq = seq;
                    }
                }

                model.Reference = $"{prefix}{(maxSeq + 1):D4}";
            }

            _context.Visites.Add(model);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // En cas de collision concurrente immÃ©diate, regÃ©nÃ©rer un numÃ©ro supÃ©rieur unique
                var uniqueSuffix = Guid.NewGuid().ToString("N")[..4].ToUpper();
                model.Reference = $"VIS-{DateTime.Now.Year}-{uniqueSuffix}";
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetVisiteById), new { id = model.Id }, model);
        }

        [HttpPut("{id}/statut")]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] StatutUpdateRequest update)
        {
            var visite = await _context.Visites.FindAsync(id);
            if (visite == null) return NotFound();

            // SÃ©curitÃ© RBAC : vÃ©rification de l'assignation du technicien
            if (User.IsInRole("Technicien"))
            {
                var currentTechId = GetCurrentTechnicienId();
                if (visite.TechnicienId != currentTechId)
                {
                    return Forbid();
                }
            }

            visite.Statut = update.Statut;
            if (!string.IsNullOrWhiteSpace(update.RapportTechnique))
            {
                visite.RapportTechnique = update.RapportTechnique;
            }
            if (!string.IsNullOrWhiteSpace(update.ActionsCorrectives))
            {
                visite.ActionsCorrectives = update.ActionsCorrectives;
            }
            if (update.DureeReelleMinutes.HasValue && update.DureeReelleMinutes.Value > 0)
            {
                visite.DureeReelleMinutes = update.DureeReelleMinutes.Value;
            }

            if (update.Statut == "ValidÃ©e")
            {
                var dateRealisee = DateTime.Now;
                visite.DateRealisee = dateRealisee;

                var equipement = await _context.Equipements
                    .Include(e => e.Site)
                    .FirstOrDefaultAsync(e => e.Id == visite.EquipementId);

                if (equipement != null && equipement.Site != null)
                {
                    equipement.DerniereVisite = dateRealisee;

                    var activeMarche = await _context.Marches
                        .Where(m => m.ClientId == equipement.Site.ClientId && m.Statut == "Actif")
                        .OrderByDescending(m => m.DateFin)
                        .FirstOrDefaultAsync();

                    if (activeMarche != null && activeMarche.VisitesAnnuellesPrevues > 0)
                    {
                        int intervalleJours = 365 / activeMarche.VisitesAnnuellesPrevues;
                        equipement.ProchaineVisitePrevue = dateRealisee.AddDays(intervalleJours);
                        activeMarche.VisitesRealisees += 1;
                        _logger.LogInformation("Prochaine visite pour l'Ã©quipement {EquipementId} recalculÃ©e au {ProchaineDate}.",
                            equipement.Id, equipement.ProchaineVisitePrevue);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(visite);
        }

        /// <summary>
        /// Moteur de recommandation dynamique de techniciens pour un Ã©quipement donnÃ© (RÃ©servÃ© Responsable)
        /// </summary>
        [HttpGet("recommandations-techniciens")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> GetRecommandationsTechniciens(
            [FromQuery] int equipementId, 
            [FromQuery] DateTime? datePrevue = null, 
            [FromQuery] int dureeMinutes = 120)
        {
            var equipement = await _context.Equipements
                .Include(e => e.Site)
                .FirstOrDefaultAsync(e => e.Id == equipementId);

            if (equipement == null)
            {
                return NotFound(new { message = "Ã‰quipement non trouvÃ©." });
            }

            var targetDate = datePrevue ?? DateTime.Now;

            // Load all technicians with their specialties and visits to compute real-time capacity
            var techniciens = await _context.Techniciens
                .Include(t => t.Specialites)
                .Include(t => t.Visites)
                .ToListAsync();

            // Calculate weekly load
            var startOfWeek = targetDate.Date.AddDays(-(int)targetDate.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7);

            var scoredList = techniciens.Select(t =>
            {
                var visitesSemaine = t.Visites.Where(v => v.DatePrevue >= startOfWeek && v.DatePrevue < endOfWeek && (v.Statut == "PlanifiÃ©e" || v.Statut == "En cours")).ToList();
                int heuresPlanifieesSemaine = (int)Math.Ceiling(visitesSemaine.Sum(v => v.DureeEstimeeMinutes) / 60.0);

                var evaluation = _scoringService.EvaluerTechnicien(t, equipement, targetDate, dureeMinutes, heuresPlanifieesSemaine);

                int heuresRestantes = Math.Max(0, t.HeuresHebdo - heuresPlanifieesSemaine);

                return new
                {
                    TechnicienId = t.Id,
                    t.Matricule,
                    NomComplet = $"{t.Prenom} {t.Nom}".Trim(),
                    t.Base,
                    t.Disponible,
                    t.Statut,
                    t.HeuresHebdo,
                    HeuresPlanifiees = heuresPlanifieesSemaine,
                    HeuresRestantes = heuresRestantes,
                    Specialites = t.Specialites.Select(s => s.Nom).ToList(),
                    Score = evaluation.ScoreTotal,
                    evaluation.ScoreCompetence,
                    evaluation.ScoreDisponibilite,
                    evaluation.ScoreCharge,
                    evaluation.ScoreProximite,
                    evaluation.DetailsCompetence,
                    evaluation.DetailsDisponibilite,
                    evaluation.DetailsCharge,
                    evaluation.DetailsProximite
                };
            })
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.HeuresRestantes)
            .ToList();

            return Ok(scoredList);
        }

        // â”€â”€ EXPORTS (RÃ©servÃ© Responsable) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpGet("export")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> ExportVisites([FromQuery] string? statut, [FromQuery] string format = "excel",
            [FromServices] PdfExportService? pdfService = null, [FromServices] CsvExportService? csvService = null)
        {
            var query = _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .ThenInclude(s => s!.Client)
                .Include(v => v.Technicien)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statut))
            {
                query = query.Where(v => v.Statut == statut);
            }

            var visites = await query.OrderBy(v => v.DatePrevue).ToListAsync();

            var headers = new string[] { "RÃ©fÃ©rence", "Type", "Ã‰quipement", "Client / Site", "Technicien", "Date PrÃ©vue", "Statut" };
            var data = visites.Select(v => new string[]
            {
                v.Reference,
                v.TypeVisite == "Autre" && !string.IsNullOrWhiteSpace(v.TypeVisiteAutre) ? $"Autre ({v.TypeVisiteAutre})" : v.TypeVisite,
                v.Equipement?.Nom ?? "",
                $"{v.Equipement?.Site?.Client?.NomSociete} / {v.Equipement?.Site?.NomSite}",
                v.Technicien != null ? $"{v.Technicien.Prenom} {v.Technicien.Nom}" : "Non assignÃ©",
                v.DatePrevue.ToString("dd/MM/yyyy"),
                v.Statut
            }).ToArray();

            if (format == "pdf")
            {
                if (pdfService == null) return StatusCode(500, "PDF service unavailable");
                var pdfBytes = pdfService.GenerateTablePdf("Planning des Visites", headers, data);
                return File(pdfBytes, "application/pdf", $"Visites_{DateTime.Now:yyyyMMdd}.pdf");
            }
            else if (format == "csv")
            {
                if (csvService == null) return StatusCode(500, "CSV service unavailable");
                var csvBytes = csvService.GenerateCsv(headers, data);
                return File(csvBytes, "text/csv; charset=utf-8", $"Visites_{DateTime.Now:yyyyMMdd}.csv");
            }
            else // default: excel
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Planification");
                for (int c = 0; c < headers.Length; c++)
                    worksheet.Cell(1, c + 1).Value = headers[c];
                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                for (int i = 0; i < data.Length; i++)
                    for (int c = 0; c < data[i].Length; c++)
                        worksheet.Cell(i + 2, c + 1).Value = data[i][c];
                worksheet.Columns().AdjustToContents();
                using var stream = new System.IO.MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Visites_{DateTime.Now:yyyyMMdd}.xlsx");
            }
        }

        [HttpGet("{id}/pv-pdf")]
        public async Task<IActionResult> ExportPvPdf(int id, [FromServices] PdfExportService pdfService)
        {
            var visite = await _context.Visites
                .Include(v => v.Equipement)
                .ThenInclude(e => e!.Site)
                .ThenInclude(s => s!.Client)
                .Include(v => v.Technicien)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (visite == null) return NotFound(new { message = "Visite introuvable." });

            // SÃ©curitÃ© RBAC : si technicien, vÃ©rifier l'assignation
            if (User.IsInRole("Technicien"))
            {
                var currentTechId = GetCurrentTechnicienId();
                if (visite.TechnicienId != currentTechId)
                {
                    return Forbid();
                }
            }

            if (visite.Statut != "ValidÃ©e") return BadRequest(new { message = "Le PV ne peut Ãªtre gÃ©nÃ©rÃ© que pour une visite validÃ©e." });

            var pdfBytes = pdfService.GeneratePvPdf(visite);
            return File(pdfBytes, "application/pdf", $"PV_{visite.Reference}.pdf");
        }
    }

    public class StatutUpdateRequest
    {
        public string Statut { get; set; } = string.Empty;
        public string? RapportTechnique { get; set; }
        public string? ActionsCorrectives { get; set; }
        public int? DureeReelleMinutes { get; set; }
    }
}

