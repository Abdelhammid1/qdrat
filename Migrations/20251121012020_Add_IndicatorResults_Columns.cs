using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_IndicatorResults_Columns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectCount",
                table: "StudentIndicatorResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Guidance",
                table: "StudentIndicatorResults",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SkippedCount",
                table: "StudentIndicatorResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrongCount",
                table: "StudentIndicatorResults",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectCount",
                table: "StudentIndicatorResults");

            migrationBuilder.DropColumn(
                name: "Guidance",
                table: "StudentIndicatorResults");

            migrationBuilder.DropColumn(
                name: "SkippedCount",
                table: "StudentIndicatorResults");

            migrationBuilder.DropColumn(
                name: "WrongCount",
                table: "StudentIndicatorResults");
        }
    }
}
