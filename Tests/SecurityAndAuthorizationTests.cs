using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Reflection;
using Raven.Controllers;
using Xunit;

namespace Raven.Tests
{
    public class SecurityAndAuthorizationTests
    {
        [Fact]
        public void DashboardController_EstProtegeParRoleResponsable()
        {
            // Vérifie que le contrôleur Dashboard complet exige le rôle Responsable
            var authAttr = typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Responsable", authAttr.Roles);
        }

        [Fact]
        public void ResetData_ActionEstProtegeeEtInterditeAuxTechniciensEtAnonymes()
        {
            var method = typeof(DashboardController).GetMethod(nameof(DashboardController.ResetData));
            Assert.NotNull(method);

            // Vérifie qu'il n'y a pas d'échappement anonyme [AllowAnonymous]
            var allowAnon = method.GetCustomAttribute<AllowAnonymousAttribute>();
            Assert.Null(allowAnon);

            // Vérifie que la classe exige bien le rôle Responsable
            var classAuth = typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(classAuth);
            Assert.Contains("Responsable", classAuth.Roles);
            Assert.DoesNotContain("Technicien", classAuth.Roles);
        }

        [Fact]
        public void EndpointsDestructifs_ExigentStrictementRoleResponsable()
        {
            var controllers = new Type[]
            {
                typeof(EquipementsController),
                typeof(TechniciensController),
                typeof(MarchesController)
            };

            foreach (var controller in controllers)
            {
                var authAttr = controller.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(authAttr);
                Assert.Equal("Responsable", authAttr.Roles);
            }
        }

        [Fact]
        public void RegisterUtilisateur_ExigeRoleResponsable()
        {
            var method = typeof(AuthController).GetMethod(nameof(AuthController.Register));
            Assert.NotNull(method);

            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Responsable", authAttr.Roles);
        }

        [Fact]
        public void CreateVisite_ExigeRoleResponsable()
        {
            var method = typeof(VisitesController).GetMethod(nameof(VisitesController.CreateVisite));
            Assert.NotNull(method);

            var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttr);
            Assert.Equal("Responsable", authAttr.Roles);
        }
    }
}

