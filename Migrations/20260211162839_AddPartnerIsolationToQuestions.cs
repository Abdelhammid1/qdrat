using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerIsolationToQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_PartnerId",
                table: "Questions",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Partners_PartnerId",
                table: "Questions",
                column: "PartnerId",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Partners_PartnerId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_PartnerId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Questions");
        }
    }
}
