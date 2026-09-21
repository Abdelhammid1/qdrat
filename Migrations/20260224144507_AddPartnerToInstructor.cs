using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerToInstructor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPartnerInstructor",
                table: "Instructors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "Instructors",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_PartnerId",
                table: "Instructors",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Instructors_Partners_PartnerId",
                table: "Instructors",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Instructors_Partners_PartnerId",
                table: "Instructors");

            migrationBuilder.DropIndex(
                name: "IX_Instructors_PartnerId",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "IsPartnerInstructor",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Instructors");
        }
    }
}
