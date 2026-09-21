using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddRemedialSessionRelationToRemedialQuiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemedialSessions_RemedialQuizzes_RemedialQuizId",
                table: "RemedialSessions");

            migrationBuilder.DropIndex(
                name: "IX_RemedialSessions_RemedialQuizId",
                table: "RemedialSessions");

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "StudentRemedialQuizResults",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "RemedialQuizzes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RemedialSessionId",
                table: "RemedialQuizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RemedialQuizzes_RemedialSessionId",
                table: "RemedialQuizzes",
                column: "RemedialSessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialQuizzes_RemedialSessions_RemedialSessionId",
                table: "RemedialQuizzes",
                column: "RemedialSessionId",
                principalTable: "RemedialSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemedialQuizzes_RemedialSessions_RemedialSessionId",
                table: "RemedialQuizzes");

            migrationBuilder.DropIndex(
                name: "IX_RemedialQuizzes_RemedialSessionId",
                table: "RemedialQuizzes");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "StudentRemedialQuizResults");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "RemedialQuizzes");

            migrationBuilder.DropColumn(
                name: "RemedialSessionId",
                table: "RemedialQuizzes");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessions_RemedialQuizId",
                table: "RemedialSessions",
                column: "RemedialQuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialSessions_RemedialQuizzes_RemedialQuizId",
                table: "RemedialSessions",
                column: "RemedialQuizId",
                principalTable: "RemedialQuizzes",
                principalColumn: "Id");
        }
    }
}
