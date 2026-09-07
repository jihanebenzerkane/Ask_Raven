using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Raven.Migrations
{
    /// <inheritdoc />
    public partial class AddStagingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StagingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    SuggestedTechnicienMatricule = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedTechnicienName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ValidationErrors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserEditsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagingRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StagingRecords_BatchId_Status",
                table: "StagingRecords",
                columns: new[] { "BatchId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StagingRecords");
        }
    }
}
