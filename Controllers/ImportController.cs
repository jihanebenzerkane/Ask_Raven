using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using Raven.Services;

namespace Raven.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImportController : ControllerBase
    {
        private readonly StagingService _stagingService;
        private readonly AppDbContext _db;

        public ImportController(StagingService stagingService, AppDbContext db)
        {
            _stagingService = stagingService;
            _db = db;
        }

        public class ProcessImportRequest
        {
            public IFormFile? File { get; set; }
            public string? EntityType { get; set; }
        }

        public class ApproveBatchRequest
        {
            public List<StagingRecordEditDto>? Edits { get; set; }
        }

        public class UpdateRecordRequest
        {
            public string? UserEditsJson { get; set; }
            public string? Status { get; set; }
            public string? SuggestedTechnicienMatricule { get; set; }
        }

        /// <summary>
        /// Accepts a file (PDF, PNG, XLSX, CSV), extracts records into the Staging buffer with confidence scores and domain matching.
        /// </summary>
        [HttpPost("process")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ProcessFile([FromForm] ProcessImportRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { error = "Fichier manquant ou vide." });
            }

            // Max 25 MB
            if (request.File.Length > 25 * 1024 * 1024)
            {
                return BadRequest(new { error = "Le fichier dépasse la limite autorisée de 25 Mo." });
            }

            int? userId = null;
            var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(subClaim, out var uid)) userId = uid;

            using var stream = request.File.OpenReadStream();
            var result = await _stagingService.ProcessFileAsync(stream, request.File.FileName, request.EntityType, userId);

            return Ok(result);
        }

        /// <summary>
        /// Gets all staging records for a specific batch.
        /// </summary>
        [HttpGet("staging/{batchId}")]
        public async Task<IActionResult> GetBatch(string batchId)
        {
            var batch = await _stagingService.GetBatchAsync(batchId);
            if (batch == null)
            {
                return NotFound(new { error = "Lot de staging introuvable." });
            }

            return Ok(batch);
        }

        /// <summary>
        /// Updates a single staging record inline before commit.
        /// </summary>
        [HttpPut("staging/record/{id:int}")]
        public async Task<IActionResult> UpdateRecord(int id, [FromBody] UpdateRecordRequest request)
        {
            var success = await _stagingService.UpdateRecordAsync(id, request.UserEditsJson, request.Status, request.SuggestedTechnicienMatricule);
            if (!success)
            {
                return NotFound(new { error = "Enregistrement de staging introuvable." });
            }

            return Ok(new { success = true, id });
        }

        /// <summary>
        /// Human-in-the-loop approval: Commits batch from staging to production tables.
        /// </summary>
        [HttpPut("staging/{batchId}/approve")]
        public async Task<IActionResult> ApproveBatch(string batchId, [FromBody] ApproveBatchRequest? request)
        {
            int? userId = null;
            var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(subClaim, out var uid)) userId = uid;

            var result = await _stagingService.CommitBatchAsync(batchId, request?.Edits, userId);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Discards a staging batch without committing to production.
        /// </summary>
        [HttpDelete("staging/{batchId}")]
        public async Task<IActionResult> DiscardBatch(string batchId)
        {
            var success = await _stagingService.DiscardBatchAsync(batchId);
            if (!success)
            {
                return NotFound(new { error = "Lot introuvable ou déjà purgé." });
            }

            return Ok(new { success = true, message = "Lot de staging supprimé." });
        }

        /// <summary>
        /// Returns technicians for quick reassignment dropdown in the staging review modal.
        /// </summary>
        [HttpGet("techniciens")]
        public async Task<IActionResult> GetTechniciensForAssignment()
        {
            var techs = await _db.Techniciens
                .Include(t => t.Specialites)
                .Select(t => new
                {
                    t.Id,
                    t.Matricule,
                    NomComplet = t.Prenom + " " + t.Nom,
                    t.Base,
                    t.Statut,
                    Specialites = t.Specialites.Select(s => s.Nom).ToList()
                })
                .ToListAsync();

            return Ok(techs);
        }
    }
}
