using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddExamTypeAndExamIdToStudentPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExamId",
                table: "StudentPerformances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamType",
                table: "StudentPerformances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPerformances_ExamId",
                table: "StudentPerformances",
                column: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentPerformances_Exams_ExamId",
                table: "StudentPerformances",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentPerformances_Exams_ExamId",
                table: "StudentPerformances");

            migrationBuilder.DropIndex(
                name: "IX_StudentPerformances_ExamId",
                table: "StudentPerformances");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "StudentPerformances");

            migrationBuilder.DropColumn(
                name: "ExamType",
                table: "StudentPerformances");
        }
    }
}
