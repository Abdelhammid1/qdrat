using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class PPM_PartnerModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForPartners",
                table: "ProfessionalModels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // يحافظ على ما يراه الشركاء اليوم: كل نموذج له جمهور يصبح موجّهًا للشركاء
            migrationBuilder.Sql(@"
UPDATE pm SET IsForPartners = 1
FROM ProfessionalModels pm
WHERE pm.IsGlobal = 1
   OR EXISTS (SELECT 1 FROM ProfessionalModelPartners p WHERE p.ProfessionalModelId = pm.Id)
   OR EXISTS (SELECT 1 FROM ProfessionalModelSubscriptionPeriods s WHERE s.ProfessionalModelId = pm.Id);");

            migrationBuilder.AddColumn<int>(
                name: "SourceProfessionalModelId",
                table: "HomeworkSets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceProfessionalModelId",
                table: "Exams",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkSets_SourceProfessionalModelId_BatchId",
                table: "HomeworkSets",
                columns: new[] { "SourceProfessionalModelId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_SourceProfessionalModelId",
                table: "Exams",
                column: "SourceProfessionalModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_ProfessionalModels_SourceProfessionalModelId",
                table: "Exams",
                column: "SourceProfessionalModelId",
                principalTable: "ProfessionalModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeworkSets_ProfessionalModels_SourceProfessionalModelId",
                table: "HomeworkSets",
                column: "SourceProfessionalModelId",
                principalTable: "ProfessionalModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_ProfessionalModels_SourceProfessionalModelId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_HomeworkSets_ProfessionalModels_SourceProfessionalModelId",
                table: "HomeworkSets");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkSets_SourceProfessionalModelId_BatchId",
                table: "HomeworkSets");

            migrationBuilder.DropIndex(
                name: "IX_Exams_SourceProfessionalModelId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsForPartners",
                table: "ProfessionalModels");

            migrationBuilder.DropColumn(
                name: "SourceProfessionalModelId",
                table: "HomeworkSets");

            migrationBuilder.DropColumn(
                name: "SourceProfessionalModelId",
                table: "Exams");
        }
    }
}
