using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class Add_PerformanceIndicatorExamId_ToQuestionAttemptNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformanceIndicatorExamId",
                table: "QuestionAttemptNew",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAttemptNew_PerformanceIndicatorExamId",
                table: "QuestionAttemptNew",
                column: "PerformanceIndicatorExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionAttemptNew_PerformanceIndicatorExams_PerformanceIndicatorExamId",
                table: "QuestionAttemptNew",
                column: "PerformanceIndicatorExamId",
                principalTable: "PerformanceIndicatorExams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionAttemptNew_PerformanceIndicatorExams_PerformanceIndicatorExamId",
                table: "QuestionAttemptNew");

            migrationBuilder.DropIndex(
                name: "IX_QuestionAttemptNew_PerformanceIndicatorExamId",
                table: "QuestionAttemptNew");

            migrationBuilder.DropColumn(
                name: "PerformanceIndicatorExamId",
                table: "QuestionAttemptNew");
        }
    }
}
