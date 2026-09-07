using System;
using Raven.Models;
using Xunit;

namespace Raven.Tests
{
    public class VisiteStatutTests
    {
        public static string CalculerStatutEffectif(string statut, DateTime datePrevue)
        {
            if (statut == "Planifiée" && datePrevue.Date < DateTime.Today)
            {
                return "En retard";
            }
            return statut;
        }

        [Fact]
        public void VisiteFuture_RestePlanifiee()
        {
            var dateFuture = DateTime.Today.AddDays(5);
            var statut = CalculerStatutEffectif("Planifiée", dateFuture);

            Assert.Equal("Planifiée", statut);
        }

        [Fact]
        public void VisiteDuJour_RestePlanifiee()
        {
            var dateDuJour = DateTime.Today;
            var statut = CalculerStatutEffectif("Planifiée", dateDuJour);

            Assert.Equal("Planifiée", statut);
        }

        [Fact]
        public void VisiteDepassee_DevientEnRetard()
        {
            var dateDepassee = DateTime.Today.AddDays(-2);
            var statut = CalculerStatutEffectif("Planifiée", dateDepassee);

            Assert.Equal("En retard", statut);
        }

        [Fact]
        public void VisiteDejaTermineeDansLePasse_ResteValidee()
        {
            var datePassee = DateTime.Today.AddDays(-10);
            var statut = CalculerStatutEffectif("Validée", datePassee);

            Assert.Equal("Validée", statut);
        }

        [Fact]
        public void VisiteAnnuleeDansLePasse_ResteAnnulee()
        {
            var datePassee = DateTime.Today.AddDays(-15);
            var statut = CalculerStatutEffectif("Annulée", datePassee);

            Assert.Equal("Annulée", statut);
        }
    }
}

