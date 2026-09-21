using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaToVerbalPassage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "VerbalPassages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "VerbalPassages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireFullListen",
                table: "VerbalPassages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "VerbalPassages",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "VerbalPassages");

            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "VerbalPassages");

            migrationBuilder.DropColumn(
                name: "RequireFullListen",
                table: "VerbalPassages");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "VerbalPassages");
        }
    }
}
