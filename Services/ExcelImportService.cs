using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace Raven.Services
{
    /// <summary>
    /// Represents a single row parsed from the Excel import file for Markets.
    /// </summary>
    public class MarcheImportRow
    {
        public int RowIndex { get; set; }          // 1-based row number in Excel
        public string Reference { get; set; } = string.Empty;
        public string ClientNom { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public string TypeContrat { get; set; } = string.Empty;
        public int VisitesAnnuellesPrevues { get; set; }
        public int VisitesRealisees { get; set; }
        public int NbVisiteGlobal { get; set; }
        public string Sites { get; set; } = string.Empty; // title-cased, comma-separated city names
        public bool PvRequis { get; set; }
        public bool FactureRequise { get; set; }
        public int NombrePC { get; set; }
        public int NombrePCPortable { get; set; }
        public int NombreImprimante { get; set; }
        public int NombreServeur { get; set; }
        public string EquipementsDivers { get; set; } = string.Empty;
        public string CommentaireImport { get; set; } = string.Empty;
        public string? ParseWarning { get; set; }  // non-fatal issues recorded for display
    }

    /// <summary>
    /// Represents a single row parsed from the Excel import file for Equipments.
    /// </summary>
    public class EquipementImportRow
    {
        public int RowIndex { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Categorie { get; set; } = string.Empty;
        public string ClientNom { get; set; } = string.Empty;
        public string SiteNom { get; set; } = string.Empty;
        public int Criticite { get; set; } = 3;
        public int ScoreSante { get; set; } = 85;
        public DateTime DateInstallation { get; set; }
        public string Statut { get; set; } = "OpÃ©rationnel";
        public string? ParseWarning { get; set; }
    }

    /// <summary>
    /// Represents a single row parsed from the Excel import file for Technicians.
    /// </summary>
    public class TechnicienImportRow
    {
        public int RowIndex { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string Base { get; set; } = "Casablanca";
        public string Statut { get; set; } = "Actif";
        public int HeuresHebdo { get; set; } = 40;
        public string Specialites { get; set; } = string.Empty;
        public string? ParseWarning { get; set; }
    }

    public class ExcelImportService
    {
        // French month names â†’ month number
        private static readonly Dictionary<string, int> FrenchMonths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "janvier",   1 }, { "jan",  1 },
            { "fÃ©vrier",   2 }, { "fev",  2 }, { "fevrier", 2 },
            { "mars",      3 },
            { "avril",     4 }, { "avr",  4 },
            { "mai",       5 },
            { "juin",      6 },
            { "juillet",   7 }, { "juil", 7 },
            { "aoÃ»t",      8 }, { "aout", 8 },
            { "septembre", 9 }, { "sep",  9 }, { "sept", 9 },
            { "octobre",  10 }, { "oct", 10 },
            { "novembre", 11 }, { "nov", 11 },
            { "dÃ©cembre", 12 }, { "dec", 12 }, { "decembre", 12 }
        };

        /// <summary>
        /// Parses the given .xlsx stream and returns a list of MarcheImportRow.
        /// </summary>
        public List<MarcheImportRow> ParseExcel(Stream stream)
        {
            var result = new List<MarcheImportRow>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow < 2) return result;

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = ws.Row(1);
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
                    headerMap[header] = cell.Address.ColumnNumber;
            }

            int Col(params string[] aliases)
            {
                foreach (var a in aliases)
                {
                    if (headerMap.TryGetValue(a, out var c)) return c;
                }
                return -1;
            }

            int colRef = Col("marchÃ©", "marche", "rÃ©fÃ©rence", "reference", "ref", "code");
            int colClient = Col("clients", "client", "sociÃ©tÃ©", "societe", "nom client");
            int colDebut = Col("date debut", "date dÃ©but", "debut", "dÃ©but");
            int colFin = Col("date fin", "fin");
            int colType = Col("type de contrat", "type contrat", "type");
            int colVisites = Col("nb visite / an", "nb visite", "visites/an", "visites annuelles");
            int colRealisees = Col("nb visite rÃ©alisÃ©", "visites rÃ©alisÃ©es", "visites realisees");
            int colGlobal = Col("nb visite global", "visites global");
            int colSites = Col("site", "sites", "ville", "villes", "localisation");
            int colPv = Col("pv", "pv requis");
            int colFacture = Col("facture", "facture requise");
            int colPc = Col("nombre pc", "pc");
            int colPcPort = Col("nombre pc portable", "pc portable", "portables");
            int colImp = Col("nombre imprimante", "imprimante", "imprimantes");
            int colServ = Col("nombre serveur", "serveur", "serveurs");
            int colDiv = Col("equipement divers", "divers", "autres equipements");
            int colComment = Col("commentaire", "commentaires", "remarques", "obs");

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.IsEmpty()) continue;

                var warnings = new List<string>();

                string GetStr(int col) => col > 0 ? row.Cell(col).GetString().Trim() : string.Empty;
                int GetInt(int col, int def = 0)
                {
                    if (col < 1) return def;
                    var s = row.Cell(col).GetString().Trim();
                    return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
                }

                var reference = GetStr(colRef);
                var clientNom = GetStr(colClient);
                if (string.IsNullOrEmpty(reference) && string.IsNullOrEmpty(clientNom))
                    continue;

                if (string.IsNullOrEmpty(reference))
                    reference = $"MAR-{r:D4}";

                var dateDebut = ParseFrenchDate(row, colDebut, warnings, "Date dÃ©but");
                var dateFin = ParseFrenchDate(row, colFin, warnings, "Date fin");
                if (dateFin < dateDebut)
                {
                    warnings.Add($"Date fin ({dateFin:dd/MM/yyyy}) antÃ©rieure Ã  la date dÃ©but ({dateDebut:dd/MM/yyyy}) â€” ajustÃ©e Ã  dÃ©but + 1 an");
                    dateFin = dateDebut.AddYears(1);
                }

                var sitesRaw = GetStr(colSites);
                var sitesClean = CleanSites(sitesRaw);

                result.Add(new MarcheImportRow
                {
                    RowIndex = r,
                    Reference = reference,
                    ClientNom = clientNom,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    TypeContrat = GetStr(colType),
                    VisitesAnnuellesPrevues = GetInt(colVisites, 12),
                    VisitesRealisees = GetInt(colRealisees, 0),
                    NbVisiteGlobal = GetInt(colGlobal, 0),
                    Sites = sitesClean,
                    PvRequis = IsBoolOui(GetStr(colPv)),
                    FactureRequise = IsBoolOui(GetStr(colFacture)),
                    NombrePC = GetInt(colPc),
                    NombrePCPortable = GetInt(colPcPort),
                    NombreImprimante = GetInt(colImp),
                    NombreServeur = GetInt(colServ),
                    EquipementsDivers = GetStr(colDiv),
                    CommentaireImport = GetStr(colComment),
                    ParseWarning = warnings.Count > 0 ? string.Join(" | ", warnings) : null
                });
            }

            return result;
        }

        /// <summary>
        /// Parses the given .xlsx stream and returns a list of EquipementImportRow.
        /// </summary>
        public List<EquipementImportRow> ParseEquipementsExcel(Stream stream)
        {
            var result = new List<EquipementImportRow>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow < 2) return result;

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = ws.Row(1);
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
                    headerMap[header] = cell.Address.ColumnNumber;
            }

            int Col(params string[] aliases)
            {
                foreach (var a in aliases)
                {
                    if (headerMap.TryGetValue(a, out var c)) return c;
                }
                return -1;
            }

            int colSerial = Col("numÃ©ro de sÃ©rie", "numero de serie", "numÃ©ro sÃ©rie", "numero serie", "nÂ° sÃ©rie", "nÂ° serie", "nÂ° de sÃ©rie", "serial", "serialnumber", "code", "matricule");
            int colNom = Col("nom Ã©quipement", "nom equipement", "Ã©quipement", "equipement", "dÃ©signation", "designation", "nom");
            int colCat = Col("catÃ©gorie", "categorie", "type Ã©quipement", "type equipement", "famille");
            int colClient = Col("client", "sociÃ©tÃ©", "societe", "nom client");
            int colSite = Col("site", "site client", "nom site", "ville", "localisation");
            int colCrit = Col("criticitÃ©", "criticite", "niveau criticitÃ©", "poids");
            int colSante = Col("santÃ©", "sante", "score santÃ©", "score sante", "Ã©tat", "etat");
            int colDate = Col("date installation", "date mise en service", "installation", "mise en service");
            int colStatut = Col("statut", "Ã©tat opÃ©rationnel", "etat operationnel");

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.IsEmpty()) continue;

                var warnings = new List<string>();

                string GetStr(int col) => col > 0 ? row.Cell(col).GetString().Trim() : string.Empty;
                int GetInt(int col, int def = 0)
                {
                    if (col < 1) return def;
                    var s = row.Cell(col).GetString().Trim();
                    return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
                }

                var serialNumber = GetStr(colSerial);
                var nom = GetStr(colNom);
                var clientNom = GetStr(colClient);
                var siteNom = GetStr(colSite);

                if (string.IsNullOrEmpty(serialNumber) && string.IsNullOrEmpty(nom))
                    continue;

                if (string.IsNullOrEmpty(serialNumber))
                {
                    serialNumber = $"EQ-{r:D4}";
                    warnings.Add("NÂ° de sÃ©rie gÃ©nÃ©rÃ© automatiquement");
                }

                if (string.IsNullOrEmpty(nom))
                    nom = $"Ã‰quipement {serialNumber}";

                var categorie = GetStr(colCat);
                if (string.IsNullOrEmpty(categorie)) categorie = "GÃ©nÃ©ral";

                var criticite = GetInt(colCrit, 3);
                if (criticite < 1) criticite = 1;
                if (criticite > 5) criticite = 5;

                var scoreSante = GetInt(colSante, 85);
                if (scoreSante < 0) scoreSante = 0;
                if (scoreSante > 100) scoreSante = 100;

                var dateInstallation = ParseFrenchDate(row, colDate, warnings, "Date installation");
                var statut = GetStr(colStatut);
                if (string.IsNullOrEmpty(statut)) statut = "OpÃ©rationnel";

                result.Add(new EquipementImportRow
                {
                    RowIndex = r,
                    SerialNumber = serialNumber,
                    Nom = nom,
                    Categorie = categorie,
                    ClientNom = clientNom,
                    SiteNom = siteNom,
                    Criticite = criticite,
                    ScoreSante = scoreSante,
                    DateInstallation = dateInstallation,
                    Statut = statut,
                    ParseWarning = warnings.Count > 0 ? string.Join(" | ", warnings) : null
                });
            }

            return result;
        }

        /// <summary>
        /// Parses the given .xlsx stream and returns a list of TechnicienImportRow.
        /// </summary>
        public List<TechnicienImportRow> ParseTechniciensExcel(Stream stream)
        {
            var result = new List<TechnicienImportRow>();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow < 2) return result;

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = ws.Row(1);
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
                    headerMap[header] = cell.Address.ColumnNumber;
            }

            int Col(params string[] aliases)
            {
                foreach (var a in aliases)
                {
                    if (headerMap.TryGetValue(a, out var c)) return c;
                }
                return -1;
            }

            int colMatricule = Col("matricule", "code", "id", "identifiant", "code technicien");
            int colNom = Col("nom", "nom de famille");
            int colPrenom = Col("prÃ©nom", "prenom");
            int colNomComplet = Col("nom complet", "nom et prÃ©nom", "nom et prenom", "technicien");
            int colEmail = Col("email", "courriel", "mail", "e-mail");
            int colTel = Col("tÃ©lÃ©phone", "telephone", "tel", "gsm", "mobile");
            int colBase = Col("base", "agence", "ville", "localisation", "site de rattachement");
            int colStatut = Col("statut", "Ã©tat", "etat", "disponibilitÃ©", "disponibilite");
            int colHeures = Col("heures hebdo", "heures", "capacitÃ©", "capacite", "heures/semaine");
            int colSpecs = Col("spÃ©cialitÃ©s", "specialites", "spÃ©cialitÃ©", "specialite", "compÃ©tences", "competences");

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.IsEmpty()) continue;

                var warnings = new List<string>();

                string GetStr(int col) => col > 0 ? row.Cell(col).GetString().Trim() : string.Empty;
                int GetInt(int col, int def = 0)
                {
                    if (col < 1) return def;
                    var s = row.Cell(col).GetString().Trim();
                    return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
                }

                var matricule = GetStr(colMatricule);
                var nom = GetStr(colNom);
                var prenom = GetStr(colPrenom);

                if (string.IsNullOrEmpty(nom) && string.IsNullOrEmpty(prenom))
                {
                    var nomComplet = GetStr(colNomComplet);
                    if (!string.IsNullOrEmpty(nomComplet))
                    {
                        var parts = nomComplet.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        prenom = parts.Length > 0 ? parts[0] : "";
                        nom = parts.Length > 1 ? parts[1] : prenom;
                    }
                }

                if (string.IsNullOrEmpty(matricule) && string.IsNullOrEmpty(nom))
                    continue;

                if (string.IsNullOrEmpty(matricule))
                {
                    matricule = $"TECH-{r:D4}";
                    warnings.Add("Matricule gÃ©nÃ©rÃ© automatiquement");
                }

                var email = GetStr(colEmail);
                if (string.IsNullOrEmpty(email))
                {
                    var cleanPrenom = Regex.Replace(prenom.ToLower(), @"[^a-z0-9]", "");
                    var cleanNom = Regex.Replace(nom.ToLower(), @"[^a-z0-9]", "");
                    email = $"{cleanPrenom}.{cleanNom}@Raven.ma";
                }

                var tel = GetStr(colTel);
                var baseLoc = GetStr(colBase);
                if (string.IsNullOrEmpty(baseLoc)) baseLoc = "Casablanca";

                var statut = GetStr(colStatut);
                if (string.IsNullOrEmpty(statut)) statut = "Actif";

                var heures = GetInt(colHeures, 40);
                if (heures <= 0) heures = 40;

                var specs = GetStr(colSpecs);

                result.Add(new TechnicienImportRow
                {
                    RowIndex = r,
                    Matricule = matricule,
                    Nom = nom,
                    Prenom = prenom,
                    Email = email,
                    Telephone = tel,
                    Base = baseLoc,
                    Statut = statut,
                    HeuresHebdo = heures,
                    Specialites = specs,
                    ParseWarning = warnings.Count > 0 ? string.Join(" | ", warnings) : null
                });
            }

            return result;
        }

        private static string CleanSites(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var parts = raw.Split(new[] { ',', ';', '/', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>();
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    list.Add(ToTitleCase(trimmed));
            }
            return string.Join(", ", list);
        }

        private static DateTime ParseFrenchDate(IXLRow row, int col, List<string> warnings, string colName)
        {
            if (col < 1) return DateTime.Today;
            var cell = row.Cell(col);

            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime().Date;

            var raw = cell.GetString().Trim();
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
            {
                try { return DateTime.FromOADate(serial).Date; } catch { }
            }

            if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var dtFr))
                return dtFr.Date;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtInv))
                return dtInv.Date;

            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    foreach (var part in parts)
                    {
                        if (FrenchMonths.TryGetValue(part, out var month))
                        {
                            foreach (var p in parts)
                            {
                                if (int.TryParse(p, out var year) && year > 1900 && year < 2100)
                                {
                                    return new DateTime(year, month, 1);
                                }
                            }
                        }
                    }
                }
                warnings.Add($"{colName}: date non reconnue '{raw}', date du jour utilisÃ©e");
            }

            return DateTime.Today;
        }

        private static bool IsBoolOui(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var v = raw.Trim().ToUpperInvariant();
            return v == "O" || v == "OUI" || v == "YES" || v == "Y" || v == "1" || v == "VRAI" || v == "TRUE";
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower().Trim());
        }
    }
}

