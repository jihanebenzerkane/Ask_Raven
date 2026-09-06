using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Data;
using Raven.Models;

namespace Raven.Controllers
{
    public class SiteDto
    {
        public string CodeSite { get; set; } = string.Empty;
        public string NomSite { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public string Adresse { get; set; } = string.Empty;
        public string Ville { get; set; } = string.Empty;
        public string CodePostal { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Responsable")]
    public class SitesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SitesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/sites
        [HttpGet]
        public async Task<IActionResult> GetSites([FromQuery] int? clientId, [FromQuery] string? ville)
        {
            var query = _context.Sites
                .Include(s => s.Client)
                .Include(s => s.Equipements)
                .AsQueryable();

            if (clientId.HasValue)
            {
                query = query.Where(s => s.ClientId == clientId.Value);
            }

            if (!string.IsNullOrWhiteSpace(ville))
            {
                query = query.Where(s => s.Ville.ToLower() == ville.Trim().ToLower());
            }

            var sites = await query
                .OrderBy(s => s.NomSite)
                .Select(s => new
                {
                    s.Id,
                    s.CodeSite,
                    s.NomSite,
                    s.ClientId,
                    ClientNom = s.Client != null ? s.Client.NomSociete : "Inconnu",
                    s.Adresse,
                    s.Ville,
                    s.CodePostal,
                    s.Latitude,
                    s.Longitude,
                    TotalEquipements = s.Equipements.Count
                })
                .ToListAsync();

            return Ok(sites);
        }

        // GET: api/sites/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSiteById(int id)
        {
            var site = await _context.Sites
                .Include(s => s.Client)
                .Include(s => s.Equipements)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (site == null)
            {
                return NotFound(new { message = "Site non trouvÃ©." });
            }

            return Ok(new
            {
                site.Id,
                site.CodeSite,
                site.NomSite,
                site.ClientId,
                ClientNom = site.Client != null ? site.Client.NomSociete : "Inconnu",
                site.Adresse,
                site.Ville,
                site.CodePostal,
                site.Latitude,
                site.Longitude,
                Equipements = site.Equipements.Select(e => new
                {
                    e.Id,
                    e.SerialNumber,
                    e.Nom,
                    e.Categorie,
                    e.Statut,
                    e.Criticite
                }).ToList()
            });
        }

        // POST: api/sites
        [HttpPost]
        public async Task<IActionResult> CreateSite([FromBody] SiteDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NomSite))
            {
                return BadRequest(new { message = "Le nom du site est obligatoire." });
            }

            var client = await _context.Clients.FindAsync(dto.ClientId);
            if (client == null)
            {
                return BadRequest(new { message = "Le client spÃ©cifiÃ© n'existe pas." });
            }

            var codeSite = string.IsNullOrWhiteSpace(dto.CodeSite)
                ? $"SITE-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
                : dto.CodeSite.Trim();

            if (await _context.Sites.AnyAsync(s => s.CodeSite == codeSite))
            {
                return BadRequest(new { message = "Un site avec ce code existe dÃ©jÃ ." });
            }

            var site = new Site
            {
                CodeSite = codeSite,
                NomSite = dto.NomSite.Trim(),
                ClientId = dto.ClientId,
                Adresse = dto.Adresse?.Trim() ?? string.Empty,
                Ville = dto.Ville?.Trim() ?? string.Empty,
                CodePostal = dto.CodePostal?.Trim() ?? string.Empty,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude
            };

            _context.Sites.Add(site);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSiteById), new { id = site.Id }, site);
        }

        // PUT: api/sites/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSite(int id, [FromBody] SiteDto dto)
        {
            var site = await _context.Sites.FindAsync(id);
            if (site == null)
            {
                return NotFound(new { message = "Site non trouvÃ©." });
            }

            if (!string.IsNullOrWhiteSpace(dto.NomSite)) site.NomSite = dto.NomSite.Trim();
            if (dto.ClientId > 0 && dto.ClientId != site.ClientId)
            {
                var clientExists = await _context.Clients.AnyAsync(c => c.Id == dto.ClientId);
                if (!clientExists) return BadRequest(new { message = "Client spÃ©cifiÃ© introuvable." });
                site.ClientId = dto.ClientId;
            }

            if (!string.IsNullOrWhiteSpace(dto.Adresse)) site.Adresse = dto.Adresse.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Ville)) site.Ville = dto.Ville.Trim();
            if (!string.IsNullOrWhiteSpace(dto.CodePostal)) site.CodePostal = dto.CodePostal.Trim();
            site.Latitude = dto.Latitude;
            site.Longitude = dto.Longitude;

            await _context.SaveChangesAsync();
            return Ok(site);
        }

        // DELETE: api/sites/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSite(int id)
        {
            var site = await _context.Sites
                .Include(s => s.Equipements)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (site == null)
            {
                return NotFound(new { message = "Site non trouvÃ©." });
            }

            // VÃ©rification de sÃ©curitÃ© mÃ©tier : interdiction de supprimer un site ayant des Ã©quipements
            if (site.Equipements != null && site.Equipements.Any())
            {
                return BadRequest(new
                {
                    message = $"Impossible de supprimer le site '{site.NomSite}' car il contient {site.Equipements.Count} Ã©quipement(s) associÃ©(s). Veuillez d'abord rÃ©assigner ou supprimer ces Ã©quipements."
                });
            }

            _context.Sites.Remove(site);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Site supprimÃ© avec succÃ¨s." });
        }
    }
}

