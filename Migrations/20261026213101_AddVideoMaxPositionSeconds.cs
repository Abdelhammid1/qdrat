using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoMaxPositionSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaxPositionSeconds",
                table: "RemedialTrackVideoProgresses",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxPositionSeconds",
                table: "RemedialTrackAddendumProgresses",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPositionSeconds",
                table: "RemedialTrackVideoProgresses");

            migrationBuilder.DropColumn(
                name: "MaxPositionSeconds",
                table: "RemedialTrackAddendumProgresses");
        }
    }
}
