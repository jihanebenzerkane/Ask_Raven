using ClosedXML.Excel;
using System;
using System.IO;
using System.Linq;
using Raven.Services;
using Xunit;

namespace Raven.Tests
{
    public class ExcelImportServiceTests
    {
        private readonly ExcelImportService _importService = new();

        [Fact]
        public void ParseExcel_Marches_FichierValide_RetourneLignesCorrectes()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Marches");
                ws.Cell(1, 1).Value = "Marché";
                ws.Cell(1, 2).Value = "Client";
                ws.Cell(1, 3).Value = "Date début";
                ws.Cell(1, 4).Value = "Date fin";
                ws.Cell(1, 5).Value = "Type de contrat";
                ws.Cell(1, 6).Value = "Visites prévues";
                ws.Cell(1, 7).Value = "Sites";

                ws.Cell(2, 1).Value = "MCH-2026-001";
                ws.Cell(2, 2).Value = "Maroc Telecom";
                ws.Cell(2, 3).Value = new DateTime(2026, 1, 1);
                ws.Cell(2, 4).Value = new DateTime(2026, 12, 31);
                ws.Cell(2, 5).Value = "Maintenance Préventive";
                ws.Cell(2, 6).Value = 12;
                ws.Cell(2, 7).Value = "Casablanca, Rabat";

                wb.SaveAs(ms);
            }

            ms.Position = 0;
            var rows = _importService.ParseExcel(ms);

            Assert.Single(rows);
            var row = rows.First();
            Assert.Equal("MCH-2026-001", row.Reference);
            Assert.Equal("Maroc Telecom", row.ClientNom);
            Assert.Equal(12, row.VisitesAnnuellesPrevues);
            Assert.Contains("Casablanca", row.Sites);
        }

        [Fact]
        public void ParseExcel_FichierVide_RetourneListeVide()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                wb.Worksheets.Add("Vide");
                wb.SaveAs(ms);
            }

            ms.Position = 0;
            var rows = _importService.ParseExcel(ms);

            Assert.Empty(rows);
        }

        [Fact]
        public void ParseEquipementsExcel_FichierValide_ExtraitEquipementsEtSites()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Equipements");
                ws.Cell(1, 1).Value = "Numéro Série";
                ws.Cell(1, 2).Value = "Nom";
                ws.Cell(1, 3).Value = "Catégorie";
                ws.Cell(1, 4).Value = "Client";
                ws.Cell(1, 5).Value = "Site";
                ws.Cell(1, 6).Value = "Criticité";

                ws.Cell(2, 1).Value = "SN-HVAC-999";
                ws.Cell(2, 2).Value = "Groupe Froid Central";
                ws.Cell(2, 3).Value = "HVAC";
                ws.Cell(2, 4).Value = "OCP Group";
                ws.Cell(2, 5).Value = "Site Jorf Lasfar";
                ws.Cell(2, 6).Value = 5;

                wb.SaveAs(ms);
            }

            ms.Position = 0;
            var rows = _importService.ParseEquipementsExcel(ms);

            Assert.Single(rows);
            var row = rows.First();
            Assert.Equal("SN-HVAC-999", row.SerialNumber);
            Assert.Equal("Groupe Froid Central", row.Nom);
            Assert.Equal("HVAC", row.Categorie);
            Assert.Equal(5, row.Criticite);
        }

        [Fact]
        public void ParseTechniciensExcel_FichierValide_ExtraitSpecialitesEtCoordonnees()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Techniciens");
                ws.Cell(1, 1).Value = "Matricule";
                ws.Cell(1, 2).Value = "Nom";
                ws.Cell(1, 3).Value = "Prénom";
                ws.Cell(1, 4).Value = "Email";
                ws.Cell(1, 5).Value = "Base";
                ws.Cell(1, 6).Value = "Spécialités";

                ws.Cell(2, 1).Value = "TECH-4001";
                ws.Cell(2, 2).Value = "Bennani";
                ws.Cell(2, 3).Value = "Youssef";
                ws.Cell(2, 4).Value = "youssef.bennani@ecs.ma";
                ws.Cell(2, 5).Value = "Tanger";
                ws.Cell(2, 6).Value = "HVAC, Haute Tension";

                wb.SaveAs(ms);
            }

            ms.Position = 0;
            var rows = _importService.ParseTechniciensExcel(ms);

            Assert.Single(rows);
            var row = rows.First();
            Assert.Equal("TECH-4001", row.Matricule);
            Assert.Equal("Bennani", row.Nom);
            Assert.Equal("Youssef", row.Prenom);
            Assert.Equal("Tanger", row.Base);
            Assert.Contains("HVAC", row.Specialites);
        }

        [Fact]
        public void ParseTechniciensExcel_LignesVides_IgnoreCorrectement()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Techniciens");
                ws.Cell(1, 1).Value = "Matricule";
                ws.Cell(1, 2).Value = "Nom";
                // Ligne 2 vide
                ws.Cell(3, 1).Value = "";
                ws.Cell(3, 2).Value = "";

                wb.SaveAs(ms);
            }

            ms.Position = 0;
            var rows = _importService.ParseTechniciensExcel(ms);

            Assert.Empty(rows);
        }
    }
}

