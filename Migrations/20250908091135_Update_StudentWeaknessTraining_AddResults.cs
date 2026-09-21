using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Update_StudentWeaknessTraining_AddResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectAnswers",
                table: "StudentWeaknessTrainings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ScorePercentage",
                table: "StudentWeaknessTrainings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "StudentWeaknessTrainings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectAnswers",
                table: "StudentWeaknessTrainings");

            migrationBuilder.DropColumn(
                name: "ScorePercentage",
                table: "StudentWeaknessTrainings");

            migrationBuilder.DropColumn(
                name: "TotalQuestions",
                table: "StudentWeaknessTrainings");
        }
    }
}
