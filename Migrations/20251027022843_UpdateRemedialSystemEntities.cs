using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QdratNew.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRemedialSystemEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemedialPlans_RemedialQuizzes_FinalQuizId",
                table: "RemedialPlans");

            migrationBuilder.DropIndex(
                name: "IX_RemedialPlans_FinalQuizId",
                table: "RemedialPlans");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "RemedialSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "RemedialSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "RemedialSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemedialQuizId",
                table: "RemedialSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RemedialQuizzes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "LessonId",
                table: "RemedialQuizzes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemedialPlanId",
                table: "RemedialQuizzes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "RemedialPlans",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RemedialQuizQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemedialQuizId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialQuizQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialQuizQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialQuizQuestions_RemedialQuizzes_RemedialQuizId",
                        column: x => x.RemedialQuizId,
                        principalTable: "RemedialQuizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VimeoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialVideos_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemedialVideoQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemedialVideoId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemedialVideoQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemedialVideoQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemedialVideoQuestions_RemedialVideos_RemedialVideoId",
                        column: x => x.RemedialVideoId,
                        principalTable: "RemedialVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessions_LessonId",
                table: "RemedialSessions",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialSessions_RemedialQuizId",
                table: "RemedialSessions",
                column: "RemedialQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialQuizzes_LessonId",
                table: "RemedialQuizzes",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialQuizzes_RemedialPlanId",
                table: "RemedialQuizzes",
                column: "RemedialPlanId",
                unique: true,
                filter: "[RemedialPlanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialPlans_SectionId",
                table: "RemedialPlans",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialQuizQuestions_QuestionId",
                table: "RemedialQuizQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialQuizQuestions_RemedialQuizId",
                table: "RemedialQuizQuestions",
                column: "RemedialQuizId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialVideoQuestions_QuestionId",
                table: "RemedialVideoQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialVideoQuestions_RemedialVideoId",
                table: "RemedialVideoQuestions",
                column: "RemedialVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialVideos_LessonId",
                table: "RemedialVideos",
                column: "LessonId");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialPlans_Sections_SectionId",
                table: "RemedialPlans",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialQuizzes_Lessons_LessonId",
                table: "RemedialQuizzes",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialQuizzes_RemedialPlans_RemedialPlanId",
                table: "RemedialQuizzes",
                column: "RemedialPlanId",
                principalTable: "RemedialPlans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialSessions_Lessons_LessonId",
                table: "RemedialSessions",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialSessions_RemedialQuizzes_RemedialQuizId",
                table: "RemedialSessions",
                column: "RemedialQuizId",
                principalTable: "RemedialQuizzes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RemedialPlans_Sections_SectionId",
                table: "RemedialPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialQuizzes_Lessons_LessonId",
                table: "RemedialQuizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialQuizzes_RemedialPlans_RemedialPlanId",
                table: "RemedialQuizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialSessions_Lessons_LessonId",
                table: "RemedialSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_RemedialSessions_RemedialQuizzes_RemedialQuizId",
                table: "RemedialSessions");

            migrationBuilder.DropTable(
                name: "RemedialQuizQuestions");

            migrationBuilder.DropTable(
                name: "RemedialVideoQuestions");

            migrationBuilder.DropTable(
                name: "RemedialVideos");

            migrationBuilder.DropIndex(
                name: "IX_RemedialSessions_LessonId",
                table: "RemedialSessions");

            migrationBuilder.DropIndex(
                name: "IX_RemedialSessions_RemedialQuizId",
                table: "RemedialSessions");

            migrationBuilder.DropIndex(
                name: "IX_RemedialQuizzes_LessonId",
                table: "RemedialQuizzes");

            migrationBuilder.DropIndex(
                name: "IX_RemedialQuizzes_RemedialPlanId",
                table: "RemedialQuizzes");

            migrationBuilder.DropIndex(
                name: "IX_RemedialPlans_SectionId",
                table: "RemedialPlans");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "RemedialSessions");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "RemedialSessions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "RemedialSessions");

            migrationBuilder.DropColumn(
                name: "RemedialQuizId",
                table: "RemedialSessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RemedialQuizzes");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "RemedialQuizzes");

            migrationBuilder.DropColumn(
                name: "RemedialPlanId",
                table: "RemedialQuizzes");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "RemedialPlans");

            migrationBuilder.CreateIndex(
                name: "IX_RemedialPlans_FinalQuizId",
                table: "RemedialPlans",
                column: "FinalQuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_RemedialPlans_RemedialQuizzes_FinalQuizId",
                table: "RemedialPlans",
                column: "FinalQuizId",
                principalTable: "RemedialQuizzes",
                principalColumn: "Id");
        }
    }
}
