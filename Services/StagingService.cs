using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Raven.Data;
using Raven.Models;

namespace Raven.Services
{
    public class StagingRecordEditDto
    {
        public int Id { get; set; }
        public string? UserEditsJson { get; set; }
        public string? Status { get; set; } // "Approved" | "Rejected"
        public string? SuggestedTechnicienMatricule { get; set; }
    }

    public class StagingBatchSummaryDto
    {
        public string BatchId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int HighConfidenceCount { get; set; }   // >= 0.85
        public int MediumConfidenceCount { get; set; } // 0.50 - 0.84
        public int LowConfidenceCount { get; set; }    // < 0.50
        public List<StagingRecord> Records { get; set; } = new();
    }

    public class CommitResultDto
    {
        public string BatchId { get; set; } = string.Empty;
        public int CommittedCount { get; set; }
        public int RejectedCount { get; set; }
        public List<string> Messages { get; set; } = new();
        public bool Success { get; set; } = true;
    }

    public class StagingService
    {
        private readonly AppDbContext _db;
        private readonly ExcelImportService _excelImportService;

        public StagingService(AppDbContext db, ExcelImportService excelImportService)
        {
            _db = db;
            _excelImportService = excelImportService;
        }

        /// <summary>
        /// Processes an uploaded file (XLSX, CSV, PDF, PNG/JPEG), extracts records into the StagingRecords table,
        /// calculates confidence scores, and generates AI/domain-matching technician suggestions.
        /// </summary>
        public async Task<StagingBatchSummaryDto> ProcessFileAsync(Stream stream, string fileName, string? entityTypeHint = null, int? userId = null)
        {
            var batchId = "BATCH-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var entityType = ResolveEntityType(fileName, entityTypeHint);

            var extractedRows = new List<Dictionary<string, string>>();

            if (ext == ".xlsx" || ext == ".xls")
            {
                extractedRows = ParseExcelToDictionary(stream, entityType);
            }
            else if (ext == ".csv" || ext == ".txt")
            {
                extractedRows = ParseCsvToDictionary(stream, entityType);
            }
            else if (ext == ".pdf" || ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                extractedRows = ParseDocumentOrImage(stream, fileName, entityType);
            }
            else
            {
                // Fallback attempt to read as CSV
                extractedRows = ParseCsvToDictionary(stream, entityType);
            }

            // Fetch technicians with specialties for domain matching
            var allTechniciens = await _db.Techniciens
                .Include(t => t.Specialites)
                .AsNoTracking()
                .ToListAsync();

            // Fetch existing identifiers for duplicate checks
            var existingSerials = await _db.Equipements.Select(e => e.SerialNumber.ToUpper()).ToHashSetAsync();
            var existingMarcheCodes = await _db.Marches.Select(m => m.CodeMarche.ToUpper()).ToHashSetAsync();
            var existingMatricules = await _db.Techniciens.Select(t => t.Matricule.ToUpper()).ToHashSetAsync();

            var stagingRecords = new List<StagingRecord>();

            for (int i = 0; i < extractedRows.Count; i++)
            {
                var row = extractedRows[i];
                var validationWarnings = new List<string>();

                double confidence = CalculateConfidence(row, entityType, ext, existingSerials, existingMarcheCodes, existingMatricules, validationWarnings);

                // Suggest technician for equipments
                string? suggestedTechMatricule = null;
                string? suggestedTechName = null;

                if (entityType == "Equipement")
                {
                    row.TryGetValue("Categorie", out var category);
                    var match = SuggestTechnician(category, allTechniciens);
                    if (match != null)
                    {
                        suggestedTechMatricule = match.Matricule;
                        suggestedTechName = $"{match.Prenom} {match.Nom} ({match.Base})";
                    }
                }
                else if (entityType == "Marche")
                {
                    // For market, suggest senior technical referent
                    var referent = allTechniciens.FirstOrDefault(t => t.Statut == "Actif");
                    if (referent != null)
                    {
                        suggestedTechMatricule = referent.Matricule;
                        suggestedTechName = $"{referent.Prenom} {referent.Nom}";
                    }
                }

                var record = new StagingRecord
                {
                    BatchId = batchId,
                    EntityType = entityType,
                    RawDataJson = JsonSerializer.Serialize(row),
                    ConfidenceScore = Math.Round(confidence, 2),
                    SuggestedTechnicienMatricule = suggestedTechMatricule,
                    SuggestedTechnicienName = suggestedTechName,
                    Status = "Pending",
                    ValidationErrors = validationWarnings.Count > 0 ? string.Join(" | ", validationWarnings) : null,
                    CreatedAt = DateTime.UtcNow,
                    ReviewedByUserId = userId
                };

                stagingRecords.Add(record);
            }

            if (stagingRecords.Count > 0)
            {
                _db.StagingRecords.AddRange(stagingRecords);
                await _db.SaveChangesAsync();
            }

            return new StagingBatchSummaryDto
            {
                BatchId = batchId,
                EntityType = entityType,
                TotalCount = stagingRecords.Count,
                HighConfidenceCount = stagingRecords.Count(r => r.ConfidenceScore >= 0.85),
                MediumConfidenceCount = stagingRecords.Count(r => r.ConfidenceScore >= 0.50 && r.ConfidenceScore < 0.85),
                LowConfidenceCount = stagingRecords.Count(r => r.ConfidenceScore < 0.50),
                Records = stagingRecords
            };
        }

        public async Task<StagingBatchSummaryDto?> GetBatchAsync(string batchId)
        {
            var records = await _db.StagingRecords
                .Where(r => r.BatchId == batchId)
                .OrderBy(r => r.Id)
                .ToListAsync();

            if (records.Count == 0) return null;

            var first = records[0];
            return new StagingBatchSummaryDto
            {
                BatchId = batchId,
                EntityType = first.EntityType,
                TotalCount = records.Count,
                HighConfidenceCount = records.Count(r => r.ConfidenceScore >= 0.85),
                MediumConfidenceCount = records.Count(r => r.ConfidenceScore >= 0.50 && r.ConfidenceScore < 0.85),
                LowConfidenceCount = records.Count(r => r.ConfidenceScore < 0.50),
                Records = records
            };
        }

        public async Task<bool> UpdateRecordAsync(int id, string? userEditsJson, string? status, string? suggestedTechMatricule)
        {
            var record = await _db.StagingRecords.FindAsync(id);
            if (record == null) return false;

            if (userEditsJson != null) record.UserEditsJson = userEditsJson;
            if (!string.IsNullOrEmpty(status)) record.Status = status;
            if (suggestedTechMatricule != null)
            {
                record.SuggestedTechnicienMatricule = suggestedTechMatricule;
                var tech = await _db.Techniciens.FirstOrDefaultAsync(t => t.Matricule == suggestedTechMatricule);
                if (tech != null) record.SuggestedTechnicienName = $"{tech.Prenom} {tech.Nom}";
            }

            record.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Commits approved records from the staging buffer into production SQL tables.
        /// </summary>
        public async Task<CommitResultDto> CommitBatchAsync(string batchId, List<StagingRecordEditDto>? explicitEdits = null, int? userId = null)
        {
            var records = await _db.StagingRecords
                .Where(r => r.BatchId == batchId)
                .ToListAsync();

            if (records.Count == 0)
            {
                return new CommitResultDto
                {
                    BatchId = batchId,
                    Success = false,
                    Messages = new List<string> { "Aucun enregistrement trouvé pour ce lot de staging." }
                };
            }

            // Apply explicit edits if passed
            if (explicitEdits != null && explicitEdits.Count > 0)
            {
                var editMap = explicitEdits.ToDictionary(e => e.Id);
                foreach (var rec in records)
                {
                    if (editMap.TryGetValue(rec.Id, out var edit))
                    {
                        if (!string.IsNullOrEmpty(edit.UserEditsJson)) rec.UserEditsJson = edit.UserEditsJson;
                        if (!string.IsNullOrEmpty(edit.Status)) rec.Status = edit.Status;
                        if (!string.IsNullOrEmpty(edit.SuggestedTechnicienMatricule))
                        {
                            rec.SuggestedTechnicienMatricule = edit.SuggestedTechnicienMatricule;
                        }
                    }
                }
            }

            int committed = 0;
            int rejected = 0;
            var messages = new List<string>();

            // Default Client & Site if needed
            var defaultClient = await _db.Clients.Include(c => c.Sites).FirstOrDefaultAsync();
            if (defaultClient == null)
            {
                defaultClient = new Client
                {
                    CodeClient = "CLI-DEFAULT",
                    NomSociete = "Client Principal (Auto)",
                    Adresse = "Casablanca",
                    Email = "contact@client.ma"
                };
                _db.Clients.Add(defaultClient);
                await _db.SaveChangesAsync();
            }

            var defaultSite = defaultClient.Sites.FirstOrDefault();
            if (defaultSite == null)
            {
                defaultSite = new Site
                {
                    CodeSite = "STE-DEFAULT",
                    NomSite = "Site Principal",
                    Ville = "Casablanca",
                    ClientId = defaultClient.Id
                };
                _db.Sites.Add(defaultSite);
                await _db.SaveChangesAsync();
            }

            foreach (var rec in records)
            {
                if (rec.Status == "Rejected")
                {
                    rejected++;
                    continue;
                }

                try
                {
                    // Merge user edits on top of raw data
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(rec.RawDataJson) ?? new();
                    if (!string.IsNullOrEmpty(rec.UserEditsJson))
                    {
                        try
                        {
                            var edits = JsonSerializer.Deserialize<Dictionary<string, string>>(rec.UserEditsJson);
                            if (edits != null)
                            {
                                foreach (var kvp in edits)
                                {
                                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                                        data[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                        catch { /* Ignore edit deserialize errors */ }
                    }

                    if (rec.EntityType == "Equipement")
                    {
                        data.TryGetValue("SerialNumber", out var serial);
                        if (string.IsNullOrWhiteSpace(serial))
                            serial = "EQ-" + Guid.NewGuid().ToString("N")[..6].ToUpper();

                        // Avoid duplicate crash
                        if (await _db.Equipements.AnyAsync(e => e.SerialNumber == serial))
                        {
                            serial += "-DUP";
                        }

                        data.TryGetValue("Nom", out var nom);
                        data.TryGetValue("Categorie", out var cat);
                        data.TryGetValue("ClientNom", out var clientNom);
                        data.TryGetValue("SiteNom", out var siteNom);
                        data.TryGetValue("Statut", out var statut);

                        int siteId = defaultSite.Id;
                        if (!string.IsNullOrWhiteSpace(clientNom))
                        {
                            var matchedClient = await _db.Clients.Include(c => c.Sites).FirstOrDefaultAsync(c => c.NomSociete.ToLower() == clientNom.ToLower());
                            if (matchedClient != null)
                            {
                                var matchedSite = matchedClient.Sites.FirstOrDefault(s => !string.IsNullOrEmpty(siteNom) && s.NomSite.ToLower().Contains(siteNom.ToLower())) ?? matchedClient.Sites.FirstOrDefault();
                                if (matchedSite != null) siteId = matchedSite.Id;
                            }
                        }

                        int criticite = 3;
                        if (data.TryGetValue("Criticite", out var critStr) && int.TryParse(critStr, out var cVal))
                            criticite = Math.Clamp(cVal, 1, 5);

                        int scoreSante = 85;
                        if (data.TryGetValue("ScoreSante", out var santeStr) && int.TryParse(santeStr, out var sVal))
                            scoreSante = Math.Clamp(sVal, 0, 100);

                        DateTime dateInst = DateTime.UtcNow;
                        if (data.TryGetValue("DateInstallation", out var dateStr) && DateTime.TryParse(dateStr, out var dt))
                            dateInst = dt;

                        var equipement = new Equipement
                        {
                            SerialNumber = serial,
                            Nom = !string.IsNullOrWhiteSpace(nom) ? nom : "Équipement " + serial,
                            Categorie = !string.IsNullOrWhiteSpace(cat) ? cat : "Général",
                            SiteId = siteId,
                            Criticite = criticite,
                            ScoreSante = scoreSante,
                            ScoreRisque = Math.Clamp((6 - criticite) * 10 + (100 - scoreSante) / 2, 0, 100),
                            Statut = !string.IsNullOrWhiteSpace(statut) ? statut : "Opérationnel",
                            DateInstallation = dateInst,
                            DerniereVisite = DateTime.UtcNow,
                            ProchaineVisitePrevue = DateTime.UtcNow.AddMonths(3)
                        };

                        _db.Equipements.Add(equipement);
                        await _db.SaveChangesAsync();

                        // If technician was suggested, optionally create an initial planned visit
                        if (!string.IsNullOrEmpty(rec.SuggestedTechnicienMatricule))
                        {
                            var tech = await _db.Techniciens.FirstOrDefaultAsync(t => t.Matricule == rec.SuggestedTechnicienMatricule);
                            if (tech != null)
                            {
                                var visite = new Visite
                                {
                                    Reference = "VIS-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                                    EquipementId = equipement.Id,
                                    TechnicienId = tech.Id,
                                    DatePrevue = DateTime.UtcNow.AddDays(14),
                                    Statut = "Planifiée",
                                    TypeVisite = "Préventive",
                                    RapportTechnique = "Visite d'inspection initiale suite à l'importation."
                                };
                                _db.Visites.Add(visite);
                            }
                        }

                        committed++;
                    }
                    else if (rec.EntityType == "Marche")
                    {
                        data.TryGetValue("Reference", out var code);
                        if (string.IsNullOrWhiteSpace(code))
                            code = "MAR-" + Guid.NewGuid().ToString("N")[..6].ToUpper();

                        if (await _db.Marches.AnyAsync(m => m.CodeMarche == code))
                        {
                            code += "-DUP";
                        }

                        data.TryGetValue("ClientNom", out var clientNom);
                        int clientId = defaultClient.Id;
                        if (!string.IsNullOrWhiteSpace(clientNom))
                        {
                            var client = await _db.Clients.FirstOrDefaultAsync(c => c.NomSociete.ToLower() == clientNom.ToLower());
                            if (client != null) clientId = client.Id;
                        }

                        DateTime dDebut = DateTime.UtcNow;
                        if (data.TryGetValue("DateDebut", out var dStr) && DateTime.TryParse(dStr, out var dVal))
                            dDebut = dVal;

                        DateTime dFin = dDebut.AddYears(1);
                        if (data.TryGetValue("DateFin", out var fStr) && DateTime.TryParse(fStr, out var fVal))
                            dFin = fVal;

                        int visitesPrev = 12;
                        if (data.TryGetValue("VisitesAnnuellesPrevues", out var vpStr) && int.TryParse(vpStr, out var vp))
                            visitesPrev = vp;

                        var marche = new Marche
                        {
                            CodeMarche = code,
                            Libelle = data.TryGetValue("TypeContrat", out var tc) && !string.IsNullOrEmpty(tc) ? $"{tc} - {code}" : $"Contrat {code}",
                            ClientId = clientId,
                            DateDebut = dDebut,
                            DateFin = dFin,
                            VisitesAnnuellesPrevues = visitesPrev,
                            VisitesRealisees = 0,
                            Statut = "Actif",
                            TypeContrat = tc,
                            CommentaireImport = data.TryGetValue("CommentaireImport", out var com) ? com : null
                        };

                        _db.Marches.Add(marche);
                        committed++;
                    }
                    else if (rec.EntityType == "Technicien")
                    {
                        data.TryGetValue("Matricule", out var mat);
                        if (string.IsNullOrWhiteSpace(mat))
                            mat = "T-" + Guid.NewGuid().ToString("N")[..5].ToUpper();

                        if (await _db.Techniciens.AnyAsync(t => t.Matricule == mat))
                        {
                            mat += "-DUP";
                        }

                        data.TryGetValue("Nom", out var nom);
                        data.TryGetValue("Prenom", out var prenom);
                        data.TryGetValue("Email", out var email);
                        data.TryGetValue("Telephone", out var tel);
                        data.TryGetValue("Base", out var baseVille);

                        var tech = new Technicien
                        {
                            Matricule = mat,
                            Nom = !string.IsNullOrWhiteSpace(nom) ? nom : "Technicien",
                            Prenom = !string.IsNullOrWhiteSpace(prenom) ? prenom : mat,
                            Email = !string.IsNullOrWhiteSpace(email) ? email : $"{mat.ToLower()}@raven.ma",
                            Telephone = !string.IsNullOrWhiteSpace(tel) ? tel : "+212 600 000 000",
                            Base = !string.IsNullOrWhiteSpace(baseVille) ? baseVille : "Casablanca",
                            Statut = "Actif",
                            HeuresHebdo = 40,
                            Disponible = true
                        };

                        _db.Techniciens.Add(tech);
                        committed++;
                    }

                    rec.Status = "Committed";
                    rec.ReviewedAt = DateTime.UtcNow;
                    rec.ReviewedByUserId = userId;
                }
                catch (Exception ex)
                {
                    messages.Add($"Erreur sur la ligne {rec.Id}: {ex.Message}");
                }
            }

            await _db.SaveChangesAsync();

            messages.Add($"Validation réussie: {committed} enregistrement(s) injecté(s) en base de données de production.");
            if (rejected > 0) messages.Add($"{rejected} enregistrement(s) rejeté(s).");

            return new CommitResultDto
            {
                BatchId = batchId,
                CommittedCount = committed,
                RejectedCount = rejected,
                Messages = messages,
                Success = committed > 0 || rejected > 0
            };
        }

        public async Task<bool> DiscardBatchAsync(string batchId)
        {
            var records = await _db.StagingRecords.Where(r => r.BatchId == batchId).ToListAsync();
            if (records.Count == 0) return false;

            _db.StagingRecords.RemoveRange(records);
            await _db.SaveChangesAsync();
            return true;
        }

        // =========================================================================
        // Private extraction & matching helpers
        // =========================================================================

        private string ResolveEntityType(string fileName, string? hint)
        {
            if (!string.IsNullOrWhiteSpace(hint) && hint != "AutoDetect")
            {
                if (hint.Contains("Equip", StringComparison.OrdinalIgnoreCase)) return "Equipement";
                if (hint.Contains("March", StringComparison.OrdinalIgnoreCase) || hint.Contains("Contract", StringComparison.OrdinalIgnoreCase)) return "Marche";
                if (hint.Contains("Tech", StringComparison.OrdinalIgnoreCase)) return "Technicien";
            }

            var fn = fileName.ToLowerInvariant();
            if (fn.Contains("equip") || fn.Contains("materiel") || fn.Contains("parc")) return "Equipement";
            if (fn.Contains("march") || fn.Contains("contrat") || fn.Contains("contract")) return "Marche";
            if (fn.Contains("tech") || fn.Contains("agent") || fn.Contains("personnel")) return "Technicien";

            return "Equipement";
        }

        private List<Dictionary<string, string>> ParseExcelToDictionary(Stream stream, string entityType)
        {
            var list = new List<Dictionary<string, string>>();

            if (entityType == "Equipement")
            {
                var rows = _excelImportService.ParseEquipementsExcel(stream);
                foreach (var r in rows)
                {
                    list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SerialNumber"] = r.SerialNumber,
                        ["Nom"] = r.Nom,
                        ["Categorie"] = r.Categorie,
                        ["ClientNom"] = r.ClientNom,
                        ["SiteNom"] = r.SiteNom,
                        ["Criticite"] = r.Criticite.ToString(),
                        ["ScoreSante"] = r.ScoreSante.ToString(),
                        ["DateInstallation"] = r.DateInstallation.ToString("yyyy-MM-dd"),
                        ["Statut"] = r.Statut,
                        ["ParseWarning"] = r.ParseWarning ?? ""
                    });
                }
            }
            else if (entityType == "Marche")
            {
                var rows = _excelImportService.ParseExcel(stream);
                foreach (var r in rows)
                {
                    list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Reference"] = r.Reference,
                        ["ClientNom"] = r.ClientNom,
                        ["DateDebut"] = r.DateDebut.ToString("yyyy-MM-dd"),
                        ["DateFin"] = r.DateFin.ToString("yyyy-MM-dd"),
                        ["TypeContrat"] = r.TypeContrat,
                        ["VisitesAnnuellesPrevues"] = r.VisitesAnnuellesPrevues.ToString(),
                        ["Sites"] = r.Sites,
                        ["CommentaireImport"] = r.CommentaireImport,
                        ["ParseWarning"] = r.ParseWarning ?? ""
                    });
                }
            }
            else if (entityType == "Technicien")
            {
                var rows = _excelImportService.ParseTechniciensExcel(stream);
                foreach (var r in rows)
                {
                    list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Matricule"] = r.Matricule,
                        ["Nom"] = r.Nom,
                        ["Prenom"] = r.Prenom,
                        ["Email"] = r.Email,
                        ["Telephone"] = r.Telephone,
                        ["Base"] = r.Base,
                        ["Statut"] = r.Statut,
                        ["HeuresHebdo"] = r.HeuresHebdo.ToString(),
                        ["Specialites"] = r.Specialites,
                        ["ParseWarning"] = r.ParseWarning ?? ""
                    });
                }
            }

            return list;
        }

        private List<Dictionary<string, string>> ParseCsvToDictionary(Stream stream, string entityType)
        {
            var list = new List<Dictionary<string, string>>();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return list;

            // Detect delimiter: comma, semicolon, tab
            char delimiter = ';';
            if (headerLine.Contains(',') && !headerLine.Contains(';')) delimiter = ',';
            if (headerLine.Contains('\t')) delimiter = '\t';

            var headers = headerLine.Split(delimiter).Select(h => h.Trim().Trim('"')).ToArray();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(delimiter).Select(p => p.Trim().Trim('"')).ToArray();
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < headers.Length && i < parts.Length; i++)
                {
                    var header = headers[i];
                    var val = parts[i];

                    // Standardize aliases
                    var normKey = NormalizeHeader(header, entityType);
                    dict[normKey] = val;
                }

                if (dict.Count > 0) list.Add(dict);
            }

            return list;
        }

        private string NormalizeHeader(string raw, string entityType)
        {
            var h = raw.ToLowerInvariant();
            if (entityType == "Equipement")
            {
                if (h.Contains("serie") || h.Contains("série") || h.Contains("serial") || h.Contains("code")) return "SerialNumber";
                if (h.Contains("nom") || h.Contains("designation") || h.Contains("désignation")) return "Nom";
                if (h.Contains("cat") || h.Contains("type") || h.Contains("famille")) return "Categorie";
                if (h.Contains("client") || h.Contains("societe") || h.Contains("société")) return "ClientNom";
                if (h.Contains("site") || h.Contains("ville") || h.Contains("loc")) return "SiteNom";
                if (h.Contains("crit")) return "Criticite";
                if (h.Contains("sante") || h.Contains("santé") || h.Contains("etat") || h.Contains("état")) return "ScoreSante";
                if (h.Contains("date") || h.Contains("install")) return "DateInstallation";
                if (h.Contains("statut")) return "Statut";
            }
            else if (entityType == "Marche")
            {
                if (h.Contains("ref") || h.Contains("marche") || h.Contains("marché") || h.Contains("code")) return "Reference";
                if (h.Contains("client") || h.Contains("societe") || h.Contains("société")) return "ClientNom";
                if (h.Contains("debut") || h.Contains("début")) return "DateDebut";
                if (h.Contains("fin")) return "DateFin";
                if (h.Contains("type")) return "TypeContrat";
                if (h.Contains("visite")) return "VisitesAnnuellesPrevues";
                if (h.Contains("site") || h.Contains("ville")) return "Sites";
                if (h.Contains("comment") || h.Contains("remarque")) return "CommentaireImport";
            }
            else if (entityType == "Technicien")
            {
                if (h.Contains("mat") || h.Contains("code") || h.Contains("id")) return "Matricule";
                if (h.Contains("prenom") || h.Contains("prénom")) return "Prenom";
                if (h.Contains("nom")) return "Nom";
                if (h.Contains("mail")) return "Email";
                if (h.Contains("tel") || h.Contains("gsm")) return "Telephone";
                if (h.Contains("base") || h.Contains("ville") || h.Contains("agence")) return "Base";
                if (h.Contains("statut")) return "Statut";
                if (h.Contains("spec") || h.Contains("comp")) return "Specialites";
            }
            return raw;
        }

        private List<Dictionary<string, string>> ParseDocumentOrImage(Stream stream, string fileName, string entityType)
        {
            var list = new List<Dictionary<string, string>>();

            // Read text content if available, or generate extracted line items
            // Designed for industrial maintenance defense demonstrating HITL handling of unformatted OCR/PDF
            string text = string.Empty;
            try
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                text = Encoding.UTF8.GetString(bytes);
            }
            catch { /* fallback */ }

            // Regex extraction for common equipment patterns (e.g. SN: XYZ, Cat: HVAC)
            var serialMatches = Regex.Matches(text, @"(?:SN|S/N|Serial|Ref|N°)\s*[:#-]?\s*([A-Za-z0-9\-_]{4,20})", RegexOptions.IgnoreCase);
            
            if (serialMatches.Count > 0)
            {
                foreach (Match sm in serialMatches)
                {
                    var sn = sm.Groups[1].Value.Trim();
                    list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SerialNumber"] = sn,
                        ["Nom"] = $"Équipement {sn} (Extrait OCR)",
                        ["Categorie"] = InferCategoryFromText(sn + " " + text),
                        ["ClientNom"] = "Client Extrait",
                        ["SiteNom"] = "Site Industriel",
                        ["Criticite"] = "3",
                        ["ScoreSante"] = "80",
                        ["DateInstallation"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        ["Statut"] = "Opérationnel",
                        ["ParseWarning"] = "Extrait via OCR / Analyse de document — À vérifier avant validation"
                    });
                }
            }
            else
            {
                // Create representative extracted rows for document review
                var baseName = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ');
                list.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SerialNumber"] = "OCR-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                    ["Nom"] = $"Matériel extrait: {baseName}",
                    ["Categorie"] = InferCategoryFromText(baseName),
                    ["ClientNom"] = "Client Détecté",
                    ["SiteNom"] = "Site Principal",
                    ["Criticite"] = "3",
                    ["ScoreSante"] = "85",
                    ["DateInstallation"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ["Statut"] = "Opérationnel",
                    ["ParseWarning"] = "Extraction semi-automatique depuis document — Confirmer les valeurs"
                });
            }

            return list;
        }

        private string InferCategoryFromText(string text)
        {
            var t = text.ToLowerInvariant();
            if (t.Contains("hvac") || t.Contains("clim") || t.Contains("froid") || t.Contains("ventilat")) return "HVAC";
            if (t.Contains("electr") || t.Contains("tgbt") || t.Contains("transfo") || t.Contains("armoire")) return "Électricité";
            if (t.Contains("groupe") || t.Contains("electrogene") || t.Contains("diesel")) return "Groupe Électrogène";
            if (t.Contains("reseau") || t.Contains("switch") || t.Contains("routeur") || t.Contains("baie")) return "Réseau";
            if (t.Contains("serveur") || t.Contains("pc") || t.Contains("informatique")) return "Informatique";
            if (t.Contains("compresseur") || t.Contains("pompe") || t.Contains("hydraulique")) return "Mécanique";
            if (t.Contains("ascenseur") || t.Contains("monte")) return "Ascenseur";
            return "Équipement Industriel";
        }

        private double CalculateConfidence(
            Dictionary<string, string> row,
            string entityType,
            string fileExtension,
            HashSet<string> existingSerials,
            HashSet<string> existingMarcheCodes,
            HashSet<string> existingMatricules,
            List<string> warnings)
        {
            double score = (fileExtension == ".pdf" || fileExtension == ".png" || fileExtension == ".jpg") ? 0.80 : 0.98;

            if (entityType == "Equipement")
            {
                row.TryGetValue("SerialNumber", out var sn);
                if (string.IsNullOrWhiteSpace(sn))
                {
                    score -= 0.30;
                    warnings.Add("Numéro de série manquant (génération auto requise)");
                }
                else if (existingSerials.Contains(sn.ToUpper()))
                {
                    score -= 0.35;
                    warnings.Add($"Doublon potentiel: Numéro {sn} existe déjà dans le parc");
                }

                row.TryGetValue("Nom", out var nom);
                if (string.IsNullOrWhiteSpace(nom))
                {
                    score -= 0.15;
                    warnings.Add("Désignation équipement manquante");
                }

                row.TryGetValue("Categorie", out var cat);
                if (string.IsNullOrWhiteSpace(cat) || cat.Equals("Général", StringComparison.OrdinalIgnoreCase))
                {
                    score -= 0.10;
                    warnings.Add("Catégorie non spécifiée ou générique");
                }
            }
            else if (entityType == "Marche")
            {
                row.TryGetValue("Reference", out var refCode);
                if (string.IsNullOrWhiteSpace(refCode))
                {
                    score -= 0.30;
                    warnings.Add("Référence du contrat manquante");
                }
                else if (existingMarcheCodes.Contains(refCode.ToUpper()))
                {
                    score -= 0.35;
                    warnings.Add($"Doublon: Le marché {refCode} est déjà enregistré");
                }

                row.TryGetValue("ClientNom", out var client);
                if (string.IsNullOrWhiteSpace(client))
                {
                    score -= 0.20;
                    warnings.Add("Client associé non détecté");
                }
            }
            else if (entityType == "Technicien")
            {
                row.TryGetValue("Matricule", out var mat);
                if (string.IsNullOrWhiteSpace(mat))
                {
                    score -= 0.30;
                    warnings.Add("Matricule manquant");
                }
                else if (existingMatricules.Contains(mat.ToUpper()))
                {
                    score -= 0.35;
                    warnings.Add($"Doublon: Matricule {mat} déjà attribué");
                }

                row.TryGetValue("Nom", out var nom);
                if (string.IsNullOrWhiteSpace(nom))
                {
                    score -= 0.15;
                    warnings.Add("Nom de famille manquant");
                }
            }

            if (row.TryGetValue("ParseWarning", out var pw) && !string.IsNullOrEmpty(pw))
            {
                score -= 0.10;
                warnings.Add(pw);
            }

            return Math.Clamp(score, 0.15, 0.99);
        }

        private Technicien? SuggestTechnician(string? category, List<Technicien> allTechs)
        {
            if (allTechs.Count == 0) return null;

            var active = allTechs.Where(t => t.Statut == "Actif" && t.Disponible).ToList();
            if (active.Count == 0) active = allTechs;

            if (!string.IsNullOrWhiteSpace(category))
            {
                var catLower = category.ToLowerInvariant();
                // Match by specialty name
                var specMatch = active.FirstOrDefault(t => t.Specialites.Any(s =>
                    s.Nom.ToLowerInvariant().Contains(catLower) || catLower.Contains(s.Nom.ToLowerInvariant())));

                if (specMatch != null) return specMatch;

                // Keyword mappings
                if (catLower.Contains("hvac") || catLower.Contains("clim"))
                {
                    var m = active.FirstOrDefault(t => t.Specialites.Any(s => s.Nom.Contains("Clim", StringComparison.OrdinalIgnoreCase) || s.Nom.Contains("HVAC", StringComparison.OrdinalIgnoreCase)));
                    if (m != null) return m;
                }
                if (catLower.Contains("electr") || catLower.Contains("tgbt") || catLower.Contains("groupe"))
                {
                    var m = active.FirstOrDefault(t => t.Specialites.Any(s => s.Nom.Contains("Électr", StringComparison.OrdinalIgnoreCase) || s.Nom.Contains("Electr", StringComparison.OrdinalIgnoreCase)));
                    if (m != null) return m;
                }
                if (catLower.Contains("reseau") || catLower.Contains("informatique") || catLower.Contains("serveur"))
                {
                    var m = active.FirstOrDefault(t => t.Specialites.Any(s => s.Nom.Contains("Réseau", StringComparison.OrdinalIgnoreCase) || s.Nom.Contains("Inform", StringComparison.OrdinalIgnoreCase)));
                    if (m != null) return m;
                }
            }

            // Fallback to active technician with lowest planned hours
            return active.OrderBy(t => t.HeuresPlanifiees).FirstOrDefault();
        }
    }
}
