using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class AddRemedialPlanStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalQuizId",
                table: "RemedialPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemedialQuizId",
                table: "Questions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RemedialQuizzes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    PassingScore = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialQuizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemedialLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemedialPlanId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplementaryMaterial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    QuizId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialLessons_RemedialPlans_RemedialPlanId",
                        column: x => x.RemedialPlanId,
                        principalTable: "RemedialPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialLessons_RemedialQuizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "RemedialQuizzes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RemedialLessons_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentRemedialQuizResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemedialQuizId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRemedialQuizResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRemedialQuizResults_RemedialQuizzes_RemedialQuizId",
                        column: x => x.RemedialQuizId,
                        principalTable: "RemedialQuizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentRemedialQuizResults_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialPlans_FinalQuizId",
                table: "RemedialPlans",
                column: "FinalQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_RemedialQuizId",
                table: "Questions",
                column: "RemedialQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialLessons_QuizId",
                table: "RemedialLessons",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialLessons_RemedialPlanId",
                table: "RemedialLessons",
                column: "RemedialPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialLessons_SectionId",
                table: "RemedialLessons",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRemedialQuizResults_RemedialQuizId",
                table: "StudentRemedialQuizResults",
                column: "RemedialQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRemedialQuizResults_StudentId",
                table: "StudentRemedialQuizResults",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_RemedialQuizzes_RemedialQuizId",
                table: "Questions",
                column: "RemedialQuizId",
                principalTable: "RemedialQuizzes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialPlans_RemedialQuizzes_FinalQuizId",
                table: "RemedialPlans",
                column: "FinalQuizId",
                principalTable: "RemedialQuizzes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_RemedialQuizzes_RemedialQuizId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialPlans_RemedialQuizzes_FinalQuizId",
                table: "RemedialPlans");

            migrationBuilder.DropTable(
                name: "RemedialLessons");

            migrationBuilder.DropTable(
                name: "StudentRemedialQuizResults");

            migrationBuilder.DropTable(
                name: "RemedialQuizzes");

            migrationBuilder.DropIndex(
                name: "IX_RemedialPlans_FinalQuizId",
                table: "RemedialPlans");

            migrationBuilder.DropIndex(
                name: "IX_Questions_RemedialQuizId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "FinalQuizId",
                table: "RemedialPlans");

            migrationBuilder.DropColumn(
                name: "RemedialQuizId",
                table: "Questions");
        }
    }
}
