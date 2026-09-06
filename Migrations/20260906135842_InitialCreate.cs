using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Raven.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanySlogan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThemeMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultHours = table.Column<int>(type: "int", nullable: false),
                    DefaultSla = table.Column<int>(type: "int", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultVisiteDuration = table.Column<int>(type: "int", nullable: false),
                    AgencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeClient = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomSociete = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactPrincipal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Specialites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Techniciens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Matricule = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateEmbauche = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Base = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeuresHebdo = table.Column<int>(type: "int", nullable: false),
                    HeuresTravaillees = table.Column<int>(type: "int", nullable: false),
                    HeuresPlanifiees = table.Column<int>(type: "int", nullable: false),
                    Disponible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Techniciens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeMarche = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlaHeures = table.Column<int>(type: "int", nullable: false),
                    VisitesAnnuellesPrevues = table.Column<int>(type: "int", nullable: false),
                    VisitesRealisees = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeContrat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PvRequis = table.Column<bool>(type: "bit", nullable: false),
                    FactureRequise = table.Column<bool>(type: "bit", nullable: false),
                    NombrePC = table.Column<int>(type: "int", nullable: false),
                    NombrePCPortable = table.Column<int>(type: "int", nullable: false),
                    NombreImprimante = table.Column<int>(type: "int", nullable: false),
                    NombreServeur = table.Column<int>(type: "int", nullable: false),
                    EquipementsDivers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommentaireImport = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Marches_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeSite = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ville = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodePostal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicienSpecialites",
                columns: table => new
                {
                    SpecialitesId = table.Column<int>(type: "int", nullable: false),
                    TechniciensId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicienSpecialites", x => new { x.SpecialitesId, x.TechniciensId });
                    table.ForeignKey(
                        name: "FK_TechnicienSpecialites_Specialites_SpecialitesId",
                        column: x => x.SpecialitesId,
                        principalTable: "Specialites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicienSpecialites_Techniciens_TechniciensId",
                        column: x => x.TechniciensId,
                        principalTable: "Techniciens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicienId = table.Column<int>(type: "int", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateurs_Techniciens_TechnicienId",
                        column: x => x.TechnicienId,
                        principalTable: "Techniciens",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Equipements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categorie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    DateInstallation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Criticite = table.Column<int>(type: "int", nullable: false),
                    ScoreSante = table.Column<int>(type: "int", nullable: false),
                    ScoreRisque = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DerniereVisite = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProchaineVisitePrevue = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipements_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TempToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtpCodes_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Visites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TypeVisite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeVisiteAutre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquipementId = table.Column<int>(type: "int", nullable: false),
                    TechnicienId = table.Column<int>(type: "int", nullable: true),
                    MarcheId = table.Column<int>(type: "int", nullable: true),
                    DatePrevue = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateRealisee = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DureeEstimeeMinutes = table.Column<int>(type: "int", nullable: false),
                    DureeReelleMinutes = table.Column<int>(type: "int", nullable: true),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScorePriorite = table.Column<double>(type: "float", nullable: false),
                    RapportTechnique = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionsCorrectives = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visites_Equipements_EquipementId",
                        column: x => x.EquipementId,
                        principalTable: "Equipements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visites_Marches_MarcheId",
                        column: x => x.MarcheId,
                        principalTable: "Marches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Visites_Techniciens_TechnicienId",
                        column: x => x.TechnicienId,
                        principalTable: "Techniciens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CodeClient",
                table: "Clients",
                column: "CodeClient",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_SerialNumber",
                table: "Equipements",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_SiteId",
                table: "Equipements",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Marches_ClientId",
                table: "Marches",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Marches_CodeMarche",
                table: "Marches",
                column: "CodeMarche",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_TempToken",
                table: "OtpCodes",
                column: "TempToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_UtilisateurId",
                table: "OtpCodes",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UtilisateurId",
                table: "PasswordResetTokens",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_ClientId",
                table: "Sites",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CodeSite",
                table: "Sites",
                column: "CodeSite",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specialites_Nom",
                table: "Specialites",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Techniciens_Matricule",
                table: "Techniciens",
                column: "Matricule",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicienSpecialites_TechniciensId",
                table: "TechnicienSpecialites",
                column: "TechniciensId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_Email",
                table: "Utilisateurs",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_TechnicienId",
                table: "Utilisateurs",
                column: "TechnicienId");

            migrationBuilder.CreateIndex(
                name: "IX_Visites_EquipementId",
                table: "Visites",
                column: "EquipementId");

            migrationBuilder.CreateIndex(
                name: "IX_Visites_MarcheId",
                table: "Visites",
                column: "MarcheId");

            migrationBuilder.CreateIndex(
                name: "IX_Visites_Reference",
                table: "Visites",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visites_TechnicienId",
                table: "Visites",
                column: "TechnicienId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "TechnicienSpecialites");

            migrationBuilder.DropTable(
                name: "Visites");

            migrationBuilder.DropTable(
                name: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "Specialites");

            migrationBuilder.DropTable(
                name: "Equipements");

            migrationBuilder.DropTable(
                name: "Marches");

            migrationBuilder.DropTable(
                name: "Techniciens");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
