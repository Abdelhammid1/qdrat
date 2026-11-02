using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class PPL_ExamOwnerPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerPartnerId",
                table: "Exams",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OwnerPartnerId",
                table: "Exams",
                column: "OwnerPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Type_OwnerPartnerId",
                table: "Exams",
                columns: new[] { "Type", "OwnerPartnerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Partners_OwnerPartnerId",
                table: "Exams",
                column: "OwnerPartnerId",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Partners_OwnerPartnerId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_OwnerPartnerId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_Type_OwnerPartnerId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "OwnerPartnerId",
                table: "Exams");
        }
    }
}
