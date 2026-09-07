using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.Data;
using Raven.Models;

namespace Raven.Controllers
{
    public class ClientDto
    {
        public string CodeClient { get; set; } = string.Empty;
        public string NomSociete { get; set; } = string.Empty;
        public string ContactPrincipal { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string Adresse { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Responsable")]
    public class ClientsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            var clients = await _context.Clients
                .AsNoTracking()
                .Select(c => new
                {
                    c.Id,
                    c.CodeClient,
                    c.NomSociete,
                    c.ContactPrincipal,
                    c.Email,
                    c.Telephone,
                    c.Adresse,
                    TotalSites = c.Sites.Count,
                    TotalMarches = c.Marches.Count,
                    Sites = c.Sites.Select(s => new { s.Id, s.CodeSite, s.NomSite, s.Ville }).ToList()
                })
                .ToListAsync();
            return Ok(clients);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Include(c => c.Sites)
                .Include(c => c.Marches)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (client == null) return NotFound(new { message = "Client introuvable." });
            return Ok(client);
        }

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] ClientDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NomSociete))
            {
                return BadRequest(new { message = "Le nom de la société est obligatoire." });
            }

            var codeClient = string.IsNullOrWhiteSpace(dto.CodeClient)
                ? $"CLI-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
                : dto.CodeClient.Trim().ToUpper();

            if (await _context.Clients.AnyAsync(c => c.CodeClient == codeClient))
            {
                return BadRequest(new { message = "Un client avec ce code existe déjÃ ." });
            }

            var client = new Client
            {
                CodeClient = codeClient,
                NomSociete = dto.NomSociete.Trim(),
                ContactPrincipal = dto.ContactPrincipal?.Trim() ?? string.Empty,
                Email = dto.Email?.Trim() ?? string.Empty,
                Telephone = dto.Telephone?.Trim() ?? string.Empty,
                Adresse = dto.Adresse?.Trim() ?? string.Empty
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(int id, [FromBody] ClientDto dto)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound(new { message = "Client introuvable." });
            }

            if (!string.IsNullOrWhiteSpace(dto.NomSociete)) client.NomSociete = dto.NomSociete.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ContactPrincipal)) client.ContactPrincipal = dto.ContactPrincipal.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email)) client.Email = dto.Email.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Telephone)) client.Telephone = dto.Telephone.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Adresse)) client.Adresse = dto.Adresse.Trim();

            await _context.SaveChangesAsync();
            return Ok(client);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients
                .Include(c => c.Sites)
                .Include(c => c.Marches)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound(new { message = "Client introuvable." });
            }

            // Protection des dépendances
            int nbSites = client.Sites?.Count ?? 0;
            int nbMarches = client.Marches?.Count ?? 0;

            if (nbSites > 0 || nbMarches > 0)
            {
                return BadRequest(new
                {
                    message = $"Impossible de supprimer le client '{client.NomSociete}' car il possède {nbSites} site(s) et {nbMarches} marché(s) associé(s). Veuillez d'abord supprimer ou réassigner ces éléments."
                });
            }

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Client supprimé avec succès." });
        }
    }
}

