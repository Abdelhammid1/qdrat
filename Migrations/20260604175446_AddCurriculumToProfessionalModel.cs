using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumToProfessionalModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurriculumId",
                table: "ProfessionalModels",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionalModels_CurriculumId",
                table: "ProfessionalModels",
                column: "CurriculumId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfessionalModels_Curriculums_CurriculumId",
                table: "ProfessionalModels",
                column: "CurriculumId",
                principalTable: "Curriculums",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfessionalModels_Curriculums_CurriculumId",
                table: "ProfessionalModels");

            migrationBuilder.DropIndex(
                name: "IX_ProfessionalModels_CurriculumId",
                table: "ProfessionalModels");

            migrationBuilder.DropColumn(
                name: "CurriculumId",
                table: "ProfessionalModels");
        }
    }
}
