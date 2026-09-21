using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalModelPartnerAndPeriodVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "ProfessionalModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProfessionalModelPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessionalModelId = table.Column<int>(type: "int", nullable: false),
                    PartnerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalModelPartners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalModelPartners_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessionalModelPartners_ProfessionalModels_ProfessionalModelId",
                        column: x => x.ProfessionalModelId,
                        principalTable: "ProfessionalModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalModelSubscriptionPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessionalModelId = table.Column<int>(type: "int", nullable: false),
                    PartnerSubscriptionPeriodId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionalModelSubscriptionPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionalModelSubscriptionPeriods_PartnerSubscriptionPeriods_PartnerSubscriptionPeriodId",
                        column: x => x.PartnerSubscriptionPeriodId,
                        principalTable: "PartnerSubscriptionPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessionalModelSubscriptionPeriods_ProfessionalModels_ProfessionalModelId",
                        column: x => x.ProfessionalModelId,
                        principalTable: "ProfessionalModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalModelPartners_PartnerId",
                table: "ProfessionalModelPartners",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalModelPartners_ProfessionalModelId_PartnerId",
                table: "ProfessionalModelPartners",
                columns: new[] { "ProfessionalModelId", "PartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalModelSubscriptionPeriods_PartnerSubscriptionPeriodId",
                table: "ProfessionalModelSubscriptionPeriods",
                column: "PartnerSubscriptionPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalModelSubscriptionPeriods_ProfessionalModelId_PartnerSubscriptionPeriodId",
                table: "ProfessionalModelSubscriptionPeriods",
                columns: new[] { "ProfessionalModelId", "PartnerSubscriptionPeriodId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessionalModelPartners");

            migrationBuilder.DropTable(
                name: "ProfessionalModelSubscriptionPeriods");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "ProfessionalModels");
        }
    }
}
