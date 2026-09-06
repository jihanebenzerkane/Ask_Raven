using System;
using Raven.Models;
using Xunit;

namespace Raven.Tests
{
    public class VisiteStatutTests
    {
        public static string CalculerStatutEffectif(string statut, DateTime datePrevue)
        {
            if (statut == "PlanifiÃ©e" && datePrevue.Date < DateTime.Today)
            {
                return "En retard";
            }
            return statut;
        }

        [Fact]
        public void VisiteFuture_RestePlanifiee()
        {
            var dateFuture = DateTime.Today.AddDays(5);
            var statut = CalculerStatutEffectif("PlanifiÃ©e", dateFuture);

            Assert.Equal("PlanifiÃ©e", statut);
        }

        [Fact]
        public void VisiteDuJour_RestePlanifiee()
        {
            var dateDuJour = DateTime.Today;
            var statut = CalculerStatutEffectif("PlanifiÃ©e", dateDuJour);

            Assert.Equal("PlanifiÃ©e", statut);
        }

        [Fact]
        public void VisiteDepassee_DevientEnRetard()
        {
            var dateDepassee = DateTime.Today.AddDays(-2);
            var statut = CalculerStatutEffectif("PlanifiÃ©e", dateDepassee);

            Assert.Equal("En retard", statut);
        }

        [Fact]
        public void VisiteDejaTermineeDansLePasse_ResteValidee()
        {
            var datePassee = DateTime.Today.AddDays(-10);
            var statut = CalculerStatutEffectif("ValidÃ©e", datePassee);

            Assert.Equal("ValidÃ©e", statut);
        }

        [Fact]
        public void VisiteAnnuleeDansLePasse_ResteAnnulee()
        {
            var datePassee = DateTime.Today.AddDays(-15);
            var statut = CalculerStatutEffectif("AnnulÃ©e", datePassee);

            Assert.Equal("AnnulÃ©e", statut);
        }
    }
}

