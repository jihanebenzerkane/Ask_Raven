using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using Raven.Models;
using Raven.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Raven.Controllers
{
    public class TechnicienDto
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public DateTime DateEmbauche { get; set; }
        public string Statut { get; set; } = "Actif";
        public string Base { get; set; } = "Casablanca";
        public int HeuresHebdo { get; set; } = 40;
        public int HeuresTravaillees { get; set; }
        public int HeuresPlanifiees { get; set; }
        public bool Disponible { get; set; } = true;
        public List<int> SpecialiteIds { get; set; } = new();
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Responsable")]
    public class TechniciensController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public TechniciensController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: api/Techniciens
        [HttpGet]
        public async Task<IActionResult> GetTechniciens()
        {
            var techniciens = await _context.Techniciens
                .Include(t => t.Specialites)
                .Include(t => t.Visites)
                .ToListAsync();

            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7);

            var result = techniciens.Select(t =>
            {
                var visitesCetteSemaine = t.Visites.Where(v => v.DatePrevue >= startOfWeek && v.DatePrevue < endOfWeek).ToList();
                int heuresPlanifiees = (int)Math.Ceiling(visitesCetteSemaine.Where(v => v.Statut == "PlanifiÃ©e" || v.Statut == "En cours").Sum(v => v.DureeEstimeeMinutes) / 60.0);
                int heuresRealisees = (int)Math.Ceiling(t.Visites.Where(v => v.Statut == "ValidÃ©e").Sum(v => (v.DureeReelleMinutes ?? v.DureeEstimeeMinutes)) / 60.0);

                var visitesValidees = t.Visites.Where(v => v.Statut == "ValidÃ©e").ToList();
                double dureeMoyenne = visitesValidees.Count > 0 
                    ? Math.Round(visitesValidees.Average(v => v.DureeReelleMinutes ?? v.DureeEstimeeMinutes), 0)
                    : 120;

                return new
                {
                    t.Id,
                    t.Matricule,
                    t.Nom,
                    t.Prenom,
                    NomComplet = $"{t.Prenom} {t.Nom}".Trim(),
                    t.Email,
                    t.Telephone,
                    t.DateEmbauche,
                    t.Statut,
                    t.Base,
                    t.HeuresHebdo,
                    HeuresPlanifiees = heuresPlanifiees,
                    HeuresTravaillees = heuresRealisees,
                    t.Disponible,
                    Specialites = t.Specialites.Select(s => new { s.Id, s.Nom }).ToList(),
                    TotalVisites = t.Visites.Count,
                    VisitesActives = t.Visites.Count(v => v.Statut == "PlanifiÃ©e" || v.Statut == "En cours"),
                    DureeMoyenneVisiteMinutes = dureeMoyenne
                };
            }).ToList();

            return Ok(result);
        }

        // GET: api/Techniciens/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTechnicien(int id)
        {
            var t = await _context.Techniciens
                .Include(t => t.Specialites)
                .Include(t => t.Visites)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (t == null)
            {
                return NotFound(new { message = "Technicien non trouvÃ©." });
            }

            return Ok(new
            {
                t.Id,
                t.Matricule,
                t.Nom,
                t.Prenom,
                NomComplet = $"{t.Prenom} {t.Nom}".Trim(),
                t.Email,
                t.Telephone,
                t.DateEmbauche,
                t.Statut,
                t.Base,
                t.HeuresHebdo,
                t.HeuresTravaillees,
                t.HeuresPlanifiees,
                t.Disponible,
                Specialites = t.Specialites.Select(s => new { s.Id, s.Nom }).ToList()
            });
        }

        // POST: api/Techniciens
        [HttpPost]
        public async Task<IActionResult> CreateTechnicien([FromBody] TechnicienDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Matricule) || string.IsNullOrWhiteSpace(dto.Nom))
            {
                return BadRequest(new { message = "Le matricule et le nom sont obligatoires." });
            }

            if (await _context.Techniciens.AnyAsync(t => t.Matricule == dto.Matricule))
            {
                return BadRequest(new { message = "Un technicien avec ce matricule existe dÃ©jÃ ." });
            }

            var tech = new Technicien
            {
                Matricule = dto.Matricule.Trim(),
                Nom = dto.Nom.Trim(),
                Prenom = dto.Prenom?.Trim() ?? string.Empty,
                Email = dto.Email?.Trim() ?? string.Empty,
                Telephone = dto.Telephone?.Trim() ?? string.Empty,
                DateEmbauche = dto.DateEmbauche == default ? DateTime.Today : dto.DateEmbauche,
                Statut = dto.Statut ?? "Actif",
                Base = dto.Base ?? "Casablanca",
                HeuresHebdo = dto.HeuresHebdo > 0 ? dto.HeuresHebdo : 40,
                Disponible = dto.Disponible
            };

            if (dto.SpecialiteIds != null && dto.SpecialiteIds.Any())
            {
                var specs = await _context.Specialites.Where(s => dto.SpecialiteIds.Contains(s.Id)).ToListAsync();
                tech.Specialites = specs;
            }

            _context.Techniciens.Add(tech);
            await _context.SaveChangesAsync();

            // CrÃ©er le compte utilisateur pour le technicien s'il a un email
            await EnsureTechnicienUserAccountAsync(tech);

            return CreatedAtAction(nameof(GetTechnicien), new { id = tech.Id }, tech);
        }

        // PUT: api/Techniciens/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTechnicien(int id, [FromBody] TechnicienDto dto)
        {
            var tech = await _context.Techniciens
                .Include(t => t.Specialites)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tech == null)
            {
                return NotFound(new { message = "Technicien introuvable." });
            }

            tech.Nom = dto.Nom.Trim();
            tech.Prenom = dto.Prenom?.Trim() ?? string.Empty;
            tech.Email = dto.Email?.Trim() ?? string.Empty;
            tech.Telephone = dto.Telephone?.Trim() ?? string.Empty;
            tech.Statut = dto.Statut ?? tech.Statut;
            tech.Base = dto.Base ?? tech.Base;
            tech.HeuresHebdo = dto.HeuresHebdo > 0 ? dto.HeuresHebdo : tech.HeuresHebdo;
            tech.Disponible = dto.Disponible;

            if (dto.SpecialiteIds != null)
            {
                var specs = await _context.Specialites.Where(s => dto.SpecialiteIds.Contains(s.Id)).ToListAsync();
                tech.Specialites = specs;
            }

            await _context.SaveChangesAsync();
            await EnsureTechnicienUserAccountAsync(tech);

            return Ok(tech);
        }

        // DELETE: api/Techniciens/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTechnicien(int id)
        {
            var tech = await _context.Techniciens.FindAsync(id);
            if (tech == null) return NotFound();

            var user = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.TechnicienId == id);
            if (user != null) _context.Utilisateurs.Remove(user);

            _context.Techniciens.Remove(tech);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Technicien supprimÃ©." });
        }

        // GET: api/Techniciens/specialites
        [HttpGet("specialites")]
        public async Task<IActionResult> GetSpecialites()
        {
            var specs = await _context.Specialites.OrderBy(s => s.Nom).ToListAsync();
            return Ok(specs);
        }

        // â”€â”€ IMPORT EXCEL TECHNICIENS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [HttpPost("import/preview")]
        public async Task<IActionResult> PreviewTechniciensImport(IFormFile? file, [FromServices] ExcelImportService importService)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Fichier Excel requis (.xlsx)." });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Format non supportÃ© : seul le format Excel (.xlsx) est acceptÃ©." });

            try
            {
                using var stream = file.OpenReadStream();
                var rows = importService.ParseTechniciensExcel(stream);

                if (!rows.Any())
                    return BadRequest(new { error = "Le fichier ne contient aucune ligne valide de technicien." });

                return Ok(new
                {
                    rowCount = rows.Count,
                    preview = rows.Take(10),
                    allRows = rows
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Erreur lors de l'analyse du fichier Excel : {ex.Message}" });
            }
        }

        [HttpPost("import/confirm")]
        public async Task<IActionResult> ConfirmTechniciensImport([FromBody] List<TechnicienImportRow> rows)
        {
            if (rows == null || !rows.Any())
                return BadRequest(new { error = "Aucune donnÃ©e Ã  importer." });

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    int imported = 0;
                    int updated = 0;

                    var allSpecs = await _context.Specialites.ToListAsync();
                    var specMap = allSpecs.ToDictionary(s => s.Nom.ToLower().Trim(), s => s);

                    foreach (var row in rows)
                    {
                        var matricule = row.Matricule?.Trim() ?? string.Empty;
                        var nom = row.Nom?.Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(matricule) && string.IsNullOrEmpty(nom)) continue;

                        if (string.IsNullOrEmpty(matricule))
                            matricule = $"TECH-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

                        var tech = await _context.Techniciens
                            .Include(t => t.Specialites)
                            .FirstOrDefaultAsync(t => t.Matricule == matricule || (t.Nom == nom && t.Prenom == row.Prenom));

                        bool isNew = tech == null;
                        if (isNew)
                        {
                            tech = new Technicien
                            {
                                Matricule = matricule,
                                Nom = nom,
                                Prenom = row.Prenom?.Trim() ?? string.Empty,
                                Email = row.Email?.Trim() ?? $"{matricule.ToLower()}@Raven.ma",
                                Telephone = row.Telephone?.Trim() ?? string.Empty,
                                Base = row.Base?.Trim() ?? "Casablanca",
                                Statut = row.Statut?.Trim() ?? "Actif",
                                HeuresHebdo = row.HeuresHebdo > 0 ? row.HeuresHebdo : 40,
                                Disponible = true,
                                DateEmbauche = DateTime.Today
                            };
                            _context.Techniciens.Add(tech);
                            imported++;
                        }
                        else
                        {
                            tech!.Nom = string.IsNullOrEmpty(nom) ? tech.Nom : nom;
                            tech.Prenom = string.IsNullOrEmpty(row.Prenom) ? tech.Prenom : row.Prenom.Trim();
                            tech.Email = string.IsNullOrEmpty(row.Email) ? tech.Email : row.Email.Trim();
                            tech.Telephone = string.IsNullOrEmpty(row.Telephone) ? tech.Telephone : row.Telephone.Trim();
                            tech.Base = string.IsNullOrEmpty(row.Base) ? tech.Base : row.Base.Trim();
                            tech.Statut = string.IsNullOrEmpty(row.Statut) ? tech.Statut : row.Statut.Trim();
                            if (row.HeuresHebdo > 0) tech.HeuresHebdo = row.HeuresHebdo;
                            updated++;
                        }

                        // Attach specialities
                        if (!string.IsNullOrWhiteSpace(row.Specialites))
                        {
                            var specNames = row.Specialites.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var name in specNames)
                            {
                                var clean = name.Trim().ToLower();
                                if (specMap.TryGetValue(clean, out var specObj))
                                {
                                    if (!tech.Specialites.Any(s => s.Id == specObj.Id))
                                        tech.Specialites.Add(specObj);
                                }
                                else
                                {
                                    var newSpec = new Specialite { Nom = name.Trim(), Description = "AjoutÃ© via import Excel" };
                                    _context.Specialites.Add(newSpec);
                                    await _context.SaveChangesAsync();
                                    specMap[clean] = newSpec;
                                    tech.Specialites.Add(newSpec);
                                }
                            }
                        }

                        await _context.SaveChangesAsync();

                        // CrÃ©ation / synchronisation du compte utilisateur
                        await EnsureTechnicienUserAccountAsync(tech);
                    }

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        message = $"Importation rÃ©ussie : {imported} technicien(s) crÃ©Ã©(s), {updated} mis Ã  jour.",
                        imported,
                        updated
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { error = $"Ã‰chec de l'import des techniciens : {ex.Message}" });
                }
            });
        }

        private async Task EnsureTechnicienUserAccountAsync(Technicien tech)
        {
            if (string.IsNullOrWhiteSpace(tech.Email)) return;

            var existingUser = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.TechnicienId == tech.Id || u.Email.ToLower() == tech.Email.ToLower());
            if (existingUser == null)
            {
                var hasher = new PasswordHasher<Utilisateur>();
                var user = new Utilisateur
                {
                    Email = tech.Email.ToLower().Trim(),
                    Role = "Technicien",
                    TechnicienId = tech.Id,
                    DateCreation = DateTime.UtcNow
                };
                var defaultPassword = _configuration["Technician:DefaultPassword"]
                    ?? _configuration["Admin:DefaultPassword"]
                    ?? Environment.GetEnvironmentVariable("TECHNICIAN_DEFAULT_PASSWORD")
                    ?? Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");

                if (string.IsNullOrWhiteSpace(defaultPassword))
                {
                    // Aucun mot de passe par dÃ©faut n'est configurÃ© : le compte sera crÃ©Ã© manuellement via l'interface d'administration
                    return;
                }

                user.PasswordHash = hasher.HashPassword(user, defaultPassword);
                _context.Utilisateurs.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                existingUser.TechnicienId = tech.Id;
                existingUser.Email = tech.Email.ToLower().Trim();
                await _context.SaveChangesAsync();
            }
        }
    }
}

