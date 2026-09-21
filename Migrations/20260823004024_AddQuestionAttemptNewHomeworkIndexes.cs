using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAttemptNewHomeworkIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_QuestionAttemptNew_HomeworkSetId_StudentId",
                table: "QuestionAttemptNew",
                columns: new[] { "HomeworkSetId", "StudentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestionAttemptNew_HomeworkSetId_StudentId",
                table: "QuestionAttemptNew");
        }
    }
}
