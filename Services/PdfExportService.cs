using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Raven.Models;
using Microsoft.AspNetCore.Hosting;

namespace Raven.Services
{
    public class PdfExportService
    {
        private readonly IWebHostEnvironment _env;

        public PdfExportService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GeneratePvPdf(Visite visite)
        {
            var primaryColor = "#0d9488";
            var primaryDark = "#0f766e";
            var textDark = "#1e293b";
            var textMuted = "#64748b";
            var bgLight = "#f8fafc";
            var borderLight = "#e2e8f0";

            var techName = visite.Technicien != null
                ? $"{visite.Technicien.Prenom} {visite.Technicien.Nom}".Trim()
                : "Non assignÃ©";
            var techMatricule = visite.Technicien?.Matricule ?? "â€”";
            var techBase = visite.Technicien?.Base ?? "â€”";

            var clientName = visite.Equipement?.Site?.Client?.NomSociete ?? "Client N/A";
            var clientCode = visite.Equipement?.Site?.Client?.CodeClient ?? "";
            var siteName = visite.Equipement?.Site?.NomSite ?? "Site N/A";
            var siteVille = visite.Equipement?.Site?.Ville ?? "";
            var siteAdresse = visite.Equipement?.Site?.Adresse ?? "";

            var eqNom = visite.Equipement?.Nom ?? "Ã‰quipement N/A";
            var eqSerial = visite.Equipement?.SerialNumber ?? "N/A";
            var eqCat = visite.Equipement?.Categorie ?? "GÃ©nÃ©ral";
            var eqCriticite = visite.Equipement?.Criticite ?? 3;

            var typeAffiche = visite.TypeVisite;
            if (visite.TypeVisite == "Autre" && !string.IsNullOrWhiteSpace(visite.TypeVisiteAutre))
            {
                typeAffiche = $"Autre ({visite.TypeVisiteAutre})";
            }

            var dateRealiseeStr = visite.DateRealisee?.ToString("dd/MM/yyyy Ã  HH:mm")
                ?? visite.DatePrevue.ToString("dd/MM/yyyy Ã  HH:mm");
            var dateEditionStr = DateTime.Now.ToString("dd/MM/yyyy Ã  HH:mm");

            var dureeEstimee = visite.DureeEstimeeMinutes > 0 ? $"{visite.DureeEstimeeMinutes} min" : "120 min";
            var dureeReelle = (visite.DureeReelleMinutes.HasValue && visite.DureeReelleMinutes.Value > 0)
                ? $"{visite.DureeReelleMinutes.Value} min"
                : dureeEstimee;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Arial).FontColor(textDark));

                    // â”€â”€ HEADER â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Raven").FontSize(22).Bold().FontColor(primaryColor);
                                c.Item().Text("Plateforme de Maintenance Industrielle & Gestion d'Ã‰quipements").FontSize(8.5f).FontColor(textMuted);
                            });

                            row.ConstantItem(220).AlignRight().Column(c =>
                            {
                                c.Item().Background(primaryColor).PaddingVertical(4).PaddingHorizontal(8).Text("PROCES-VERBAL D'INTERVENTION").FontSize(10).Bold().FontColor(Colors.White).AlignCenter();
                                c.Item().PaddingTop(2).Text($"RÃ©f : {visite.Reference}").FontSize(9).Bold().FontColor(textDark).AlignRight();
                                c.Item().Text($"Ã‰ditÃ© le : {dateEditionStr}").FontSize(7.5f).FontColor(textMuted).AlignRight();
                            });
                        });

                        col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(primaryColor);
                    });

                    // â”€â”€ CONTENT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(12);

                        // Statut & SynthÃ¨se Strip
                        col.Item().Background(bgLight).Border(1).BorderColor(borderLight).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("TYPE D'INTERVENTION").FontSize(7.5f).Bold().FontColor(textMuted);
                                c.Item().Text(typeAffiche).FontSize(10).Bold().FontColor(primaryColor);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("STATUT").FontSize(7.5f).Bold().FontColor(textMuted);
                                c.Item().Text("VALIDÃ‰E & TERMINÃ‰E").FontSize(10).Bold().FontColor(Colors.Green.Darken2);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DATE RÃ‰ALISATION").FontSize(7.5f).Bold().FontColor(textMuted);
                                c.Item().Text(dateRealiseeStr).FontSize(9.5f).SemiBold();
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DURÃ‰E EFFECTIVE").FontSize(7.5f).Bold().FontColor(textMuted);
                                c.Item().Text(dureeReelle).FontSize(9.5f).Bold().FontColor(primaryDark);
                            });
                        });

                        // Grille 2 colonnes : Client/Site & Ã‰quipement
                        col.Item().Row(row =>
                        {
                            // Colonne Gauche : Client & Site
                            row.RelativeItem().Border(1).BorderColor(borderLight).Column(c =>
                            {
                                c.Item().Background(bgLight).Padding(6).Text("ðŸ“ CLIENT & LOCALISATION").FontSize(9).Bold().FontColor(primaryDark);
                                c.Item().Padding(8).Column(inner =>
                                {
                                    inner.Spacing(4);
                                    inner.Item().Text(t => { t.Span("Client : ").Bold(); t.Span(clientName); if (!string.IsNullOrEmpty(clientCode)) t.Span($" ({clientCode})").FontColor(textMuted); });
                                    inner.Item().Text(t => { t.Span("Site : ").Bold(); t.Span(siteName); });
                                    inner.Item().Text(t => { t.Span("Ville : ").Bold(); t.Span(string.IsNullOrEmpty(siteVille) ? "Non renseignÃ©e" : siteVille); });
                                    if (!string.IsNullOrEmpty(siteAdresse))
                                        inner.Item().Text(t => { t.Span("Adresse : ").Bold(); t.Span(siteAdresse).FontColor(textMuted); });
                                });
                            });

                            row.ConstantItem(12); // Espace

                            // Colonne Droite : Ã‰quipement & Intervenant
                            row.RelativeItem().Border(1).BorderColor(borderLight).Column(c =>
                            {
                                c.Item().Background(bgLight).Padding(6).Text("âš™ï¸ Ã‰QUIPEMENT & TECHNICIEN").FontSize(9).Bold().FontColor(primaryDark);
                                c.Item().Padding(8).Column(inner =>
                                {
                                    inner.Spacing(4);
                                    inner.Item().Text(t => { t.Span("Ã‰quipement : ").Bold(); t.Span(eqNom); });
                                    inner.Item().Text(t => { t.Span("NÂ° SÃ©rie : ").Bold(); t.Span(eqSerial).FontColor(primaryDark); });
                                    inner.Item().Text(t => { t.Span("CatÃ©gorie : ").Bold(); t.Span(eqCat); t.Span($" | CriticitÃ© : {eqCriticite}/5").FontColor(textMuted); });
                                    inner.Item().Text(t => { t.Span("Technicien : ").Bold(); t.Span(techName); t.Span($" ({techMatricule} - {techBase})").FontColor(textMuted); });
                                });
                            });
                        });

                        // Rapport Technique & Constats
                        col.Item().Border(1).BorderColor(borderLight).Column(c =>
                        {
                            c.Item().Background(bgLight).Padding(6).Text("ðŸ“‹ RAPPORT TECHNIQUE & CONSTATS D'INSPECTION").FontSize(9).Bold().FontColor(primaryDark);
                            c.Item().Padding(10).MinHeight(60).Text(
                                string.IsNullOrWhiteSpace(visite.RapportTechnique)
                                    ? "Inspection et points de contrÃ´le rÃ©alisÃ©s conformÃ©ment aux procÃ©dures standards de maintenance. Aucun dysfonctionnement majeur constatÃ©."
                                    : visite.RapportTechnique
                            ).FontSize(9).LineHeight(1.3f);
                        });

                        // Actions Correctives & PiÃ¨ces
                        col.Item().Border(1).BorderColor(borderLight).Column(c =>
                        {
                            c.Item().Background(bgLight).Padding(6).Text("ðŸ”§ ACTIONS CORRECTIVES & PIÃˆCES REMPLACÃ‰ES").FontSize(9).Bold().FontColor(primaryDark);
                            c.Item().Padding(10).MinHeight(45).Text(
                                string.IsNullOrWhiteSpace(visite.ActionsCorrectives)
                                    ? "ContrÃ´les pÃ©riodiques et serrages effectuÃ©s. Nettoyage et vÃ©rification des paramÃ¨tres nominaux. Ã‰quipement opÃ©rationnel."
                                    : visite.ActionsCorrectives
                            ).FontSize(9).LineHeight(1.3f);
                        });

                        // Signatures & Visa
                        col.Item().PaddingTop(8).Border(1).BorderColor(borderLight).Column(c =>
                        {
                            c.Item().Background(bgLight).Padding(6).Text("âœï¸ VALIDATION & SIGNATURES").FontSize(9).Bold().FontColor(primaryDark);
                            c.Item().Padding(10).Row(r =>
                            {
                                r.RelativeItem().Column(sig =>
                                {
                                    sig.Item().Text("Pour le Client / RÃ©ceptionnaire :").Bold().FontSize(8.5f);
                                    sig.Item().Text("Nom & PrÃ©nom : ____________________").FontSize(8).FontColor(textMuted);
                                    sig.Item().Text("Mention Â« Bon pour rÃ©ception des travaux Â»").FontSize(7.5f).Italic().FontColor(textMuted);
                                    sig.Item().PaddingTop(25).Text("Cachet & Signature").FontSize(8).FontColor(textMuted).AlignCenter();
                                });

                                r.ConstantItem(20);

                                r.RelativeItem().Column(sig =>
                                {
                                    sig.Item().Text("Pour le Prestataire / Technicien :").Bold().FontSize(8.5f);
                                    sig.Item().Text($"Technicien : {techName} ({techMatricule})").FontSize(8).FontColor(textDark);
                                    sig.Item().Text($"Date : {dateRealiseeStr}").FontSize(7.5f).FontColor(textMuted);
                                    sig.Item().PaddingTop(25).Text("Visa & Signature du Technicien").FontSize(8).FontColor(textMuted).AlignCenter();
                                });
                            });
                        });
                    });

                    // â”€â”€ FOOTER â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(borderLight);
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Raven â€” Document officiel de maintenance industrielle").FontSize(7.5f).FontColor(textMuted);
                            row.ConstantItem(120).AlignRight().Text(t =>
                            {
                                t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(textMuted));
                                t.Span("Page ");
                                t.CurrentPageNumber();
                                t.Span(" sur ");
                                t.TotalPages();
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateTablePdf(string title, string[] headers, string[][] data)
        {
            var primaryColor = "#0d9488";
            var textDark = "#1e293b";
            var textMuted = "#64748b";
            var bgLight = "#f8fafc";
            var borderLight = "#e2e8f0";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily(Fonts.Arial).FontColor(textDark));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(title).FontSize(18).Bold().FontColor(primaryColor);
                                c.Item().Text($"Rapport exportÃ© le {DateTime.Now:dd/MM/yyyy Ã  HH:mm} â€” Total : {data.Length} ligne(s)").FontSize(8).FontColor(textMuted);
                            });

                            row.ConstantItem(120).AlignRight().Column(c =>
                            {
                                c.Item().Text("Raven").FontSize(14).Bold().FontColor(primaryColor);
                                c.Item().Text("Export Officiel").FontSize(7.5f).FontColor(textMuted);
                            });
                        });

                        col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(primaryColor);
                    });

                    // Content Table
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < headers.Length; i++)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                            {
                                header.Cell().Background(primaryColor).Padding(5).Text(h).FontSize(8.5f).Bold().FontColor(Colors.White);
                            }
                        });

                        for (int r = 0; r < data.Length; r++)
                        {
                            var row = data[r];
                            var currentBg = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            foreach (var cell in row)
                            {
                                table.Cell().Background(currentBg).BorderBottom(0.5f).BorderColor(borderLight).Padding(4).Text(cell ?? "â€”").FontSize(8);
                            }
                        }
                    });

                    // Footer
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(0.5f).LineColor(borderLight);
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Raven â€” SystÃ¨me de Planification & Maintenance Industrielle").FontSize(7.5f).FontColor(textMuted);
                            row.ConstantItem(100).AlignRight().Text(t =>
                            {
                                t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(textMuted));
                                t.Span("Page ");
                                t.CurrentPageNumber();
                                t.Span(" / ");
                                t.TotalPages();
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}

